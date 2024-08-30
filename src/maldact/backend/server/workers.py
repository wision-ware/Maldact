import os
import multiprocessing as mp
import zmq
import platform
import json


def main_worker(config_file) -> None:

    def parse_message(msg: str) -> tuple:
        pass

    def detach():
        # Platform-specific detachment
        if platform.system() == 'Windows':
            # Do nothing specific, already detached by the subprocess call
            pass
        else:
            # For Unix-like systems
            os.setsid()  # Detach from parent process
        print(f"Worker process started with PID: {os.getpid()}")

    with open(config_file, 'r') as cf:
        config = json.load(cf)



    detach()  # If not already detached from the main process, this is the time

    HOST = '127.0.01'
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind(f"tcp://{HOST}:{kwargs.get('port')}")

    while True:
        message = socket.recv_string()

        # Message handling logic
        pass