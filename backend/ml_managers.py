import multiprocessing

from backend.learn_network import LearnNetwork
from backend.network import Network
import numpy as np
import cupy as cp
import multiprocessing as mp
import inspect
from event_bus import EventBus as eb
import os


class TrainingManager:

    def __init__(self, event=None, **kwargs):

        # make Learn_network.learn() default arguments into attributes of TrainingManager
        self.network = LearnNetwork([1, 1])
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
            inp,
            labels
        )
        compute_process = mp.Process(
            target=self.executor,
            args=exec_args
        )
        compute_process.start()

    def executor(self, inp, labels):

        arg_filter = lambda x: not callable(x) and not isinstance(x, LearnNetwork)
        kwargs = {key: value for key, value in self.__dict__.items() if arg_filter(value)}
        meta = self.network.learn(
            inp,
            labels,
            **kwargs
        )
        np.save(meta, os.path.join(self.model_dir, self.model_name))

    def exit_training(self):
        pass  # TODO


class SortingManager:

    def __init__(self, **kwargs):

        self.network = Network('')
        self.model_file = None
        self.sort_dir = None
        self.dir_path = None

        # overwrite the default values
        for key, value in kwargs.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)
            else:
                raise AttributeError(f"SortingManager object has no attribute {key}")

    def update_params(self, update_dict):

        for key, value in update_dict.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)

    def start_sorting(self):

        self.network.load_params(self.model_file)
        data = np.load(self.sort_dir)
        self.dir_path = f"sorted_by_{self.model_file.os.path.basename(self.model_file)}"
        os.mkdir(os.path.join(self.sort_dir, self.dir_path))
        for i in range(self.network.N[-1]):
            os.mkdir(os.path.join(self.dir_path, str(i)))

    def executor(self, data):

        out = self.network.get_output(data)
        dim = out.shape[0]
        for i in range(dim):
            np.save(os.path.join(self.dir_path, str(np.argmax(out[i, :]))), out[i, :])
