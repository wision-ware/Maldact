import os
import multiprocessing as mp
from maldact.backend.server.workers import main_worker
import zmq
import yaml


class ServerManager:

    @classmethod
    def initialize(cls) -> None:
        pass

    @classmethod
    def get_pid(cls, port) -> int:
        pass

    @classmethod
    def start_server(cls, **kwargs) -> int:

        command = kwargs.get('action', '')
        static_config = os.path.join('..', '..', 'config', 'config_server.yaml')

        with open(static_config) as scf:
            pass

        if 'from_config_file' in kwargs:
            try:
                from yaml import CLoader as Loader
            except ImportError:
                from yaml import Loader
            with open(kwargs.get('from_config_file')) as icf:
                loaded_config = yaml.load_all(icf, Loader)

        server_kwargs = {}

        server_proc = mp.Process(target=main_worker, kwargs=server_kwargs)
        server_proc.start()

    @classmethod
    def shut_down(cls, pid) -> None:
        pass
