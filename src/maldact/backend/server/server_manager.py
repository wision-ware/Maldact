import os
import multiprocessing as mp
from maldact.backend.server.workers import main_worker
from maldact.backend.client.clinet_manager import ClientManager
import zmq
import sys
import yaml
import signal
import platform
import subprocess
import tempfile
import json
import portalocker
import time


class ServerManager:

    sif_path = os.path.join('..', '..', 'data', 'server_instances.json')
    static_config = os.path.join('..', '..', 'config', 'config_server.yaml')
    max_inactive = 5
    server_records: dict

    @classmethod
    def ping_local_server(cls, port) -> dict:
        """
        Used to check if a recorded server instance is still running or responsive/accessible

        :return: dict containing the success acknowledgement and latency
        """
        context = zmq.Context()
        socket = context.socket(zmq.REQ)
        socket.connect(f"tcp://localhost:{port}")

        timeout = 5000  # Timeout in milliseconds (5 seconds)
        start_time = time.time()

        # Send ping
        socket.send_string("ping")

        while True:
            try:
                # Nonblocking check to poll for an answer
                pong = socket.recv_string(flags=zmq.NOBLOCK)
                # Terminate polling after successfully receiving the 'pong' answer
                if pong == "pong":
                    return {"success": True, "latency": (time.time() - start_time) * 1000}
            except zmq.Again:
                # No message received yet
                if (time.time() - start_time) * 1000 > timeout:
                    break
                time.sleep(0.01)  # Briefly sleep to avoid busy-waiting

        return {"success": False, "latency": None}

    @classmethod
    def update_records(cls) -> None:
        """
        Verifies all recorded instances and updates the file and variables

        :return: None
        """

        for i, instance in enumerate(cls.server_records['instances']):
            # Check for activity by pinging
            if cls.ping_local_server(instance['port']):
                instance['status'] = 'active'
                instance['inactive_counter'] = 0  # reset the inactivity counter
            else:
                instance['status'] = 'inactive'
                instance['inactive_counter'] += 1  # increment counter
                # if the counter exceeds a threshold, server gets disposed
                if instance['inactive_counter'] >= cls.max_inactive:
                    # force kill the process just to be sure
                    cls.shut_down(instance['pid'], force=True)
                    cls.server_records['instances'].pop(i)  # remove from the records
                    print(f"Disposed an inactive server on tcp://localhost:{instance['port']}"
                          f" and stopped tracking")
                    continue
            instance['last_checked'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())

    @classmethod
    def flush_records(cls) -> None:
        """
        Flushes the server record dictionary in memory into the file

        :return: None
        """
        with portalocker.Lock(cls.sif_path, 'w', timeout=10) as sif:
            json.dump(cls.server_records, sif)
            sif.truncate()

    @classmethod
    def initialize(cls) -> None:
        """
        Runs a background check on the running servers an updates the central tracking file. Parses the file and loads
        the contents into memory

        :return: None
        """

        with portalocker.Lock(cls.sif_path, 'r', timeout=10) as sif:
            cls.server_records = json.load(sif)

        cls.update_records()
        cls.flush_records()

    @classmethod
    def get_pid(cls, port) -> int:
        """
        Finds the PID of the running server that listens on a specific port. If no such server exists, 0 gets returned.

        :param port: selected port
        :return: process ID
        """

        for instance in cls.server_records:
            if instance["port"] == port:
                return instance["pid"]

        return 0

    @classmethod
    def process_cli_command(cls, **kwargs) -> None:
        """
        Processes the server CLI command. Accepts already parsed arguments as keyword arguments.

        :param kwargs: keyword arguments of the command
        :return: None
        """
        # extract the command
        command = kwargs.get('action', '')

        match command:
            case 'start':
                cls.start_server(**kwargs)
            case 'stop':
                hard = kwargs.get('hard_stop', False)
                # manage the halt of all servers if the `--all` option is used
                if kwargs.get('stop_all', False):
                    with portalocker.Lock(cls.sif_path, 'r+', timeout=10) as sif:
                        servers = json.load(sif)
                        pids = []
                        for instance in servers['instances']:
                            pids.append(instance['pid'])
                    for pid in pids:
                        cls.shut_down(pid, force=hard)
                    return
                pid = kwargs.get('running_server_pid', None)
                if not pid:
                    pid = cls.get_pid(kwargs.get('running_server_port'))

                cls.shut_down(pid)
            case 'config':
                pass  # TODO

    @classmethod
    def record_new(cls, ip, port, pid) -> None:
        """
        Create a new server record about a newly launched server

        :param ip: IP address of the new server
        :param port: comm port
        :param pid: Process ID of the server's main process
        :return: None
        """
        current_time = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
        instance = {
            'ip': ip,
            'port': port,
            'pid': pid,
            'status': 'active',
            'inactive_counter': 0,
            'created': current_time,
            'last_checked': current_time
        }
        cls.server_records['instances'].append(instance)

    @classmethod
    def start_server(cls, **kwargs) -> int:
        """
        Manages the startup sequence of a newly configured server instance

        :param kwargs: server configuration
        :return: server process PID
        """

        try:  # load the correct yaml loader
            from yaml import CLoader as Loader
        except ImportError:
            from yaml import Loader

        # if requested, the configuratioin will be read from a supplied .yaml file
        if 'from_config_file' in kwargs:
            with open(kwargs.get('from_config_file')) as icf:
                server_kwargs = yaml.load_all(icf, Loader)

        else:
            with open(cls.static_config) as scf:
                server_kwargs = yaml.load_all(scf, Loader)

        # pack the configuration inside a temporary file to pass the starting arguments for the server
        with tempfile.NamedTemporaryFile('w', delete=False) as temp_file:
            json.dump(server_kwargs, temp_file)
            temp_file_name = temp_file.name

        if platform.system() == 'Windows':
            # Windows-specific: start a detached process using subprocess
            DETACHED_PROCESS = 0x00000008
            process = subprocess.Popen(
                [sys.executable, __file__, 'main_worker', temp_file_name],
                close_fds=True,
                creationflags=DETACHED_PROCESS
            )
            pid = process.pid
        else:
            # Unix-like systems: use multiprocessing and detach with `setsid` after a fork
            p = mp.Process(target=main_worker, args=(temp_file_name,))
            p.start()
            pid = p.pid

        cls.record_new()

        return pid

    @classmethod
    def shut_down(cls, pid, force=False) -> None:
        """
        Shuts down a server with a given PID. Does so by waiting until the request queue gets empty
        (and ignoring new requests) or if `force` is set to True, they get killed immediately

        :param pid: PID of the servers main process
        :param force: whether to forcefully kill the process
        :return: None
        """

        # platform specific forceful termination of the server's process tree
        if force:
            # os.kill with `SIGTERM` works the same way as `SIGKILL` on Unix
            # (Windows doesn't actually have signals, it's just a weird encapsulation quirk of the os module)
            if platform.system() == 'Windows':
                try:
                    os.kill(pid, signal.SIGTERM)  # Send the SIGTERM signal to the process
                    print(f"Process with PID {pid} has been terminated.")
                except ProcessLookupError:
                    print(f"No process found with PID {pid}.")
                except PermissionError:
                    print(f"Permission denied to terminate process with PID {pid}.")
            else:
                try:
                    os.kill(pid, signal.SIGKILL)  # Send the SIGKILL signal to the process
                    print(f"Process with PID {pid} has been terminated.")
                except ProcessLookupError:
                    print(f"No process found with PID {pid}.")
                except PermissionError:
                    print(f"Permission denied to terminate process with PID {pid}.")
        else:
            # terminate the process using a request
            request = {
                "action": "shutdown"
            }
            ClientManager.send_request(pid, request)

        for instance in cls.server_records:
            if instance["pid"] == pid:
                instance["inactive_counter"] = cls.max_inactive
        cls.flush_records()
