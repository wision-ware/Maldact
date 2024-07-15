import multiprocessing
import multiprocessing as mp
import numpy as np
import os
import sys
import time
import traceback

import PyQt5.QtCore as qtc

import state_enums as enums


class AsyncManager:

    process: multiprocessing.Process

    def __init__(self) -> None:
        self.polling_timer = qtc.QTimer(100)
        self.workload_queue = multiprocessing.Queue()
        self.data_queue = multiprocessing.Queue()

        self.payload = []
        self.callbacks = []

    def __enter__(self) -> None:
        self.process = multiprocessing.Process(target=process_target, args=(self.workload_queue, ))

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.workload_queue.put(enums.SubprocessFlags.STOP)

    def asynchronous(self, func):
        """
        A decorator used by child classes to mark methods to be executed asynchronously.

        :param func: decorated function
        :return: the return value of the decorated function
        """
        def wrapper(*args, callback=None, **kwargs):

            self.payload.append(lambda: func(*args, **kwargs))
            self.callbacks.append(callback)

            return func(*args, **kwargs)

        return wrapper


class SimplexQueue(multiprocessing.Queue):
    def __init__(self, sender_process_name, receiver_process_name, *args,  **kwargs):
        super().__init__(*args, **kwargs)
        self.sender_process_name = sender_process_name
        self.receiver_process_name = receiver_process_name

    @staticmethod
    def __authorised(func, process_name, err_msg="", *args, **kwargs):
        current_process = multiprocessing.current_process()
        if current_process.name == process_name:
            return func(*args, **kwargs)
        else:
            raise UnauthorisedAccessError(err_msg)

    @staticmethod
    def err_msg(func_name):
        return f"The name of the process attempting to use `{func_name}()` is different from the sender!"

    def get(self, *args, **kwargs):
        return SimplexQueue.__authorised(super().get, self.receiver_process_name,
            SimplexQueue.err_msg("get"),
            *args, **kwargs)

    def get_nowait(self, *args, **kwargs):
        return SimplexQueue.__authorised(super().get_nowait, self.receiver_process_name,
             SimplexQueue.err_msg("get_nowait"),
             *args, **kwargs)

    def put(self, *args, **kwargs):
        return SimplexQueue.__authorised(super().get, self.receiver_process_name,
            SimplexQueue.err_msg("put"),
            *args, **kwargs)

    def put_nowait(self, *args, **kwargs):
        return SimplexQueue.__authorised(super().put_nowait, self.receiver_process_name,
             SimplexQueue.err_msg("put_nowait"),
             *args, **kwargs)


class UnauthorisedAccessError(Exception):
    pass


def process_target(workload_queue: multiprocessing.Queue, result_queue: multiprocessing.Queue):

    while True:
        # parse the queue entry
        flag, *params = workload_queue.get(block=True)
        match flag:
            case enums.SubprocessFlags.STOP:
                break
            case enums.SubprocessFlags.SUSPEND:
                wait = params[0]
                time.sleep(wait)
                continue
            case enums.SubprocessFlags.EXEC:
                func = params[0]
            case _:
                continue

        try:

            result = func()

        except Exception as e:

            exc_type = type(e).__name__
            exc_message = str(e)
            exc_traceback = e.__traceback__

            exception_info = {
                'type': exc_type,
                'message': exc_message,
                'traceback': traceback.format_tb(exc_traceback)
            }

            result_queue((enums.ProcessReport.CRASHED, exception_info))

