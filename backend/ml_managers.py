import multiprocessing

from backend.Learn_network import Learn_network
import numpy as np
import cupy as cp
import multiprocessing as mp
import inspect
from event_bus import EventBus as eb
import os


class TrainingManager:

    def __init__(self, event=None, **kwargs):

        # make Learn_network.learn() default arguments into attributes of TrainingManager
        self.network = Learn_network([1, 1])
        learn_signature = inspect.signature(self.network.learn)
        default_args = {param.name: param.default for param in learn_signature.parameters.values() if
                        param.default != inspect.Parameter.empty}
        for key, value in default_args.items():
            setattr(self, key, value)

        # additional attributes
        self.save_params = False
        self.data_dir = None
        self.model_dir = None
        self.model_name = None

        # overwrite the default values
        for key, value in kwargs.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)
            else:
                raise AttributeError(f"TrainingManager object has no attribute {key}")

        if event:
            eb.subscribe(event, self.start_training)

    def update_params(self, update_dict):

        for key, value in update_dict.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)

    def start_training(self):

        self.network.__init__(self.N, GPU=self.GPU)  # reinitialize network object
        training_data = np.load(self.data_dir)
        inp = training_data["input"]
        labels = training_data["labels"]
        exec_args = (
            self.network,
            inp,
            labels
        )
        compute_process = mp.Process(
            target=self.executor,
            args=exec_args
        )
        compute_process.start()

    def executor(self, network, inp, labels):

        arg_filter = lambda x: not callable(x) and not isinstance(x, Learn_network)
        kwargs = {key: value for key, value in self.__dict__.items() if arg_filter(value)}
        meta = network.learn(
            inp,
            labels,
            **kwargs
        )
        np.save(meta, os.path.join(self.model_dir, self.model_name))

    def exit_training(self):
        pass  #TODO


class SortingManager:
    pass
