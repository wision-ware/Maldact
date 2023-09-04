import multiprocessing

from backend.Learn_network import Learn_network
import numpy as np
import cupy as cp
import multiprocessing as mp
import inspect


class TrainingManager:

    def __init__(self, **kwargs):

        # make Learn_network.learn() default arguments into attributes of TrainingManager
        self.network = Learn_network([1, 1])
        learn_signature = inspect.signature(self.network.learn)
        default_args = {param.name: param.default for param in learn_signature.parameters.values() if\
                        param.default != inspect.Parameter.empty}
        for key, value in default_args:
            setattr(self, key, value)

        # additional attributes
        self.save_params = False
        self.data_dir = None
        self.model_dir = None

        # overwrite the default values
        for key, value in kwargs.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)
            else:
                raise AttributeError(f"TrainingManager object has no attribute {key}")

    def update_params(self, update_dict):
        for key, value in update_dict.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)

    def start_training(self):

        self.network.__init__(self.N, GPU=self.GPU)
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
        np.save(meta, self.model_dir)

    def exit_training(self):
        pass
