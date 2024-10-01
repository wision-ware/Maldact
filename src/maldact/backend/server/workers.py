import os
import multiprocessing as mp
import zmq
import platform
import json
import time
from maldact.backend import ml_managers


def main_worker(config_file) -> None:

    wrong_key_message = f'''The provided API key is incorrect or nonexistent! 

Please try again with a correct key.'''

    def create_answer(answer_type, **kwargs) -> str:
        """
        Bundles all request datat into a .JSON request with a creation timestamp

        :param answer_type: type of the answer is the only mandatory parameter
        :param kwargs: all the other answer parameters
        :return: request in form of a .json string
        """
        current_time = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
        kwargs['request_type'] = answer_type
        kwargs['time'] = current_time
        return json.dumps(kwargs)

    def prepare_training_manager(**kwargs) -> ml_managers.TrainingManager:
        """
        Prepares and configures a training manager to be used for repeated training tasks

        :param kwargs: config parameters
        :return: manager object
        """
        pass

    def prepare_sorting_manager(**kwargs) -> ml_managers.SortingManager:
        """
        Prepares and configures a sorting manager to be used for repeated sorting tasks

        :param kwargs: config parameters
        :return: manager object
        """
        pass

    def create_answer(answer_type: str, **kwargs) -> dict:
        pass

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

    api_key = config.get('api_key')  # Store the key inside memory, its single use
    mode = config.get('mode')

    HOST = '127.0.01'
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind(f"tcp://{HOST}:{config.get('port')}")



    while True:

        message = socket.recv_string()  # blocking call, no need for delays
        message = json.loads(message)
        if message.get('key') != api_key:
            error_message = create_answer(answer_type='ErrorMessage', error_type='IncorrectKey', text=wrong_key_message)
            socket.send = json.dumps(error_message)
            continue

        # Message handling logic
