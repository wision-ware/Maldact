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
    def update_records(cls, file) -> None:
        """
        Verifies all recorded instances and updates the file and variables

        :param file: opened file containing the server instance records
        :return: None
        """
        servers = json.load(file)

        pass

    @classmethod
    def initialize(cls) -> None:
        """
        Runs a background check on the running servers an updates the central tracking file. Parses the file and loads
        the contents into memory

        :return: None
        """

        with portalocker.Lock(ServerManager.sif_path, 'r+', timeout=10) as sif:
            servers = json.load(sif)

            for instance in servers['instances']:
                # Example check
                if ServerManager.ping_local_server(instance['port']):
                    instance['status'] = "active"
                else:
                    instance['status'] = "inactive"
                instance['last_checked'] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())

    @classmethod
    def get_pid(cls, port) -> int:
        """
        Finds the PID of the running server that listens on a specific port

        :param port: selected port
        :return: process ID
        """
        # future implementation here ---

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
                ServerManager.start_server(**kwargs)
            case 'stop':
                hard = kwargs.get('hard_stop', False)
                if kwargs.get('stop_all', False):
                    with portalocker.Lock(ServerManager.sif_path, 'r+', timeout=10) as sif:
                        servers = json.load(sif)
                        pids = []
                        for instance in servers['instances']:
                            pids.append(instance['pid'])
                    for pid in pids:
                        ServerManager.shut_down(pid, force=hard)
                    return
                pid = kwargs.get('running_server_pid', None)
                if not pid:
                    pid = ServerManager.get_pid(kwargs.get('running_server_port'))

                ServerManager.shut_down(pid)
            case 'config':
                pass  # TODO

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
            with open(ServerManager.static_config) as scf:
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
