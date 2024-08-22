import os
import multiprocessing as mp
import zmq
import platform


def main_worker(**kwargs) -> None:

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

    detach()

    HOST = '127.0.01'
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind(f"tcp://{HOST}:{kwargs.get('port')}")



    while True:
        message = socket.recv_string()

        # message handling logic
        pass