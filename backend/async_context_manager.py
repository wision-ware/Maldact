import multiprocessing as mp
import numpy as np
import os
import sys
import PyQt5.QtCore as qtc


class AsyncManager:

    polling_timer = qtc.QTimer()

    def __init__(self):
        self.process = mp.Process(target=process_target)

    def __enter__(self):
        self.process.start()

    def __exit__(self, exc_type, exc_val, exc_tb):
        pass  # TODO

    def asynchronous(self, func):
        """
        A decorator used by child classes to mark methods to be executed asynchronously.

        :param func:
        :return:
        """
        def wrapper(*args, **kwargs):
            # TODO
            return func(*args, **kwargs)

        return wrapper


def process_target():
    pass
