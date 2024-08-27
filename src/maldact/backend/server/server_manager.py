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


class ServerManager:

    sif_path = os.path.join('..', '..', 'data', 'server_instances.json')

    @classmethod
    def ping_server(cls) -> bool:
        """
        Used to check if a recorded server instance is still running or responsive/accessible

        :return: success of the action
        """
        # ping implementation here ---

        return False

    @classmethod
    def update_records(cls, file) -> None:
        """
        Verifies all recorded instances and updates the file and variables

        :param file: opened file containing the server instance records
        :return: None
        """

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

    @classmethod
    def get_pids(cls, port) -> list[int]:
        """
        Finds the PIDs of the running servers that listen on a specific port

        :param port: selected port
        :return: list of PIDs
        """
        # future implementation here ---

        return []

    @classmethod
    def start_server(cls, **kwargs) -> int:
        """
        Manages the startup sequence of a newly configured server instance

        :param kwargs: server configuration
        :return: PID of the server
        """

        command = kwargs.get('action', '')
        static_config = os.path.join('..', '..', 'config', 'config_server.yaml')

        try:
            from yaml import CLoader as Loader
        except ImportError:
            from yaml import Loader

        with open(static_config) as scf:
            loaded_static_config = yaml.load_all(scf, Loader)

        if 'from_config_file' in kwargs:

            with open(kwargs.get('from_config_file')) as icf:
                loaded_config = yaml.load_all(icf, Loader)

        server_kwargs = {}

        # argument parsing

        with tempfile.NamedTemporaryFile('w', delete=False) as temp_file:
            json.dump(server_kwargs, temp_file)
            temp_file_name = temp_file.name
            temp_file_name = f""  # TODO server identification

        if platform.system() == 'Windows':
            # Windows-specific: start a detached process using subprocess
            DETACHED_PROCESS = 0x00000008
            subprocess.Popen([sys.executable, __file__, 'main_worker'], close_fds=True, creationflags=DETACHED_PROCESS)
        else:
            # Unix-like systems: use multiprocessing and detach with `setsid` after fork
            p = mp.Process(target=main_worker)
            p.start()

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
