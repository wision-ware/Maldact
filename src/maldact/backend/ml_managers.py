from backend.learn_network import LearnNetwork
from backend.network import Network
from backend.async_workers import training_executor, sorting_executor
from event_bus import EventBus as eb
from state_enums import ProcessReport as Term

import numpy as np
# import cupy as cp
import multiprocessing as mp
import inspect
import os
import queue
import PyQt5.QtCore as qtc
import traceback
from glob import glob
import re


class MLManager:

    pass


class TrainingManager:

    # __slots__ = (
    #     "id", "network", "N", "GPU", "GD", "time_limit", "save_params", "data_file",
    #     "model_dir", "model_name", "training_process", "term_queue", "check_timer",
    #     "threshold", "batch_size", "fixed_iter", "eta", "live_monitor", "as_text",
    #     "dia_data", "overwrite", "default_model_name"
    # )
    instance_id = 1

    def __init__(self, event=None, **kwargs) -> None:

        # ID assignment
        self.id = TrainingManager.instance_id
        TrainingManager.instance_id += 1

        # predefining `LearnNetwork.learn()` arguments as instance attributes
        self.GD: str
        self.time_limit: int
        self.eta: float
        self.threshold: float
        self.batch_size: int
        self.fixed_iter: int
        self.save_params: bool
        self.live_monitor: bool
        self.as_text: bool
        self.dia_data: bool
        self.overwrite: bool

        # make `LearnNetwork.learn()` default arguments into attributes of TrainingManager
        self.network = LearnNetwork([1, 1], GPU=False)
        learn_signature = inspect.signature(self.network.learn)
        default_args = {param.name: param.default for param in learn_signature.parameters.values() if
                        param.default != inspect.Parameter.empty}
        for key, value in default_args.items():
            setattr(self, key, value)

        # additional attributes
        self.N = None
        self.GPU: bool = False
        self.data_file = None
        self.model_dir = None
        self.model_name = None
        self.training_process = None
        self.term_queue = mp.Queue()
        self.check_timer = qtc.QTimer()
        self.default_model_name: str = "model<number>"

        # overwrite the default values, TODO converting to `__slots__`
        for key, value in kwargs.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)
            else:
                raise AttributeError(f"TrainingManager object has no attribute {key}")

        if event:
            eb.subscribe(event, self.start_training)

    def update_params(self, update_dict: dict) -> None:

        for key, value in update_dict.items():
            try: setattr(self, key, value)
            except AttributeError: pass

    def start_training(self) -> None:

        try:
            if self.training_process.is_alive():
                return None
        except AttributeError:
            pass

        # adjusting attributes to fit `LearnNetworks` interface --------------------------------------------------------
        self.N = [
            int(num) for num in self.N.replace("]", " ").replace("[", " ").replace(" ", "").replace(",", " ").split()
        ]
        self.time_limit = int(self.time_limit)

        gd_mapping = {
            "Mini batch": "mini_b",
            "Batch": "batch",
            "Stochastic": "stochastic"
        }
        self.GD = gd_mapping[self.GD]

        self.network.__init__(self.N, GPU=self.GPU)  # reinitialize network object
        training_data = np.load(self.data_file, allow_pickle=True).item()
        inp = training_data["input"]
        labels = training_data["labels"]
        # setting the default model name
        suffix = ".model"
        if self.model_name == self.default_model_name:  # whether to find the minimum unused number
            existing_defaults = set()
            for path in glob(os.path.join(self.model_dir, f"model*{suffix}")):
                wildcard_match = os.path.basename(path).replace(suffix, "").replace("model", "")
                try: existing_defaults.add(int(wildcard_match))
                except ValueError: pass
            num = 0
            while num in existing_defaults:
                num += 1
            self.model_name = f"model{num}{suffix}"
        elif not self.model_name.endswith(suffix):
            self.model_name += suffix

        # --------------------------------------------------------------------------------------------------------------

        # packing the arguments for the subprocesses
        exec_args = (
            inp,
            labels,
            self.network,
            self.term_queue,
            os.path.join(self.model_dir, self.model_name)
        )

        kwarg_names = (
            "threshold",
            "time_limit",
            "GD",
            "batch_size",
            "fixed_iter"
        )
        exec_kwargs = dict()
        for name in kwarg_names:
            if (value := getattr(self, name)) is not None:
                exec_kwargs[name] = value

        self.training_process = mp.Process(
            target=training_executor,
            args=exec_args,
            kwargs=exec_kwargs
        )

        self.training_process.start()
        self.check_timer.timeout.connect(self.check_queue)
        self.check_timer.start(100)

    def exit_training(self) -> None:
        pass  # TODO

    def check_queue(self) -> None:
        try:
            message = self.term_queue.get_nowait()

        except queue.Empty:
            return

        match message[0]:

            case Term.DONE:
                eb.emit(f"training_done_{self.id}", self.id)
                self.check_timer.stop()
                if self.training_process.is_alive():
                    self.training_process.terminate()

            case Term.CRASHED:
                eb.emit(f"training_crashed_{self.id}", message[1], self.id)
                self.check_timer.stop()
                if self.training_process.is_alive():
                    self.training_process.terminate()


class SortingManager:

    instance_id = 1

    def __init__(self, **kwargs) -> None:

        # ID assignment
        self.id = TrainingManager.instance_id
        TrainingManager.instance_id += 1

        # other attribute declarations
        self.network = Network(skip_init=True)
        self.model_file = None
        self.data_file = None
        self.sort_dir = None
        self.dir_name = None
        self.sorting_process = None
        self.term_queue = mp.Queue()
        self.check_timer = qtc.QTimer()

        # overwrite the default values
        for key, value in kwargs.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)
            else:
                raise AttributeError(f"SortingManager object has no attribute {key}")

    def update_params(self, update_dict) -> None:

        for key, value in update_dict.items():
            if key in self.__dict__.keys():
                setattr(self, key, value)

    def start_sorting(self) -> None:
        def try_mkdir(dir_path) -> int:
            try:
                os.mkdir(dir_path)
                return 0
            except FileExistsError:
                return 1

        try:
            if self.sorting_process.is_alive() is True:
                return None
        except AttributeError:
            pass

        suffix = ".model"

        self.network.load_params(self.model_file)
        self.dir_name = f"sorted_by_{os.path.basename(self.model_file).replace(suffix, '')}"
        path = os.path.join(self.sort_dir, self.dir_name)

        dir_differentiator = 0
        while try_mkdir(f"{path}_{dir_differentiator}"):
            dir_differentiator += 1
        path = f"{path}_{dir_differentiator}"

        data = np.load(self.data_file, allow_pickle=True).item()["input"]  # TODO
        self.term_queue = mp.Queue()
        for i in range(self.network.N[-1]):
            os.mkdir(os.path.join(path, str(i)))
        exec_args = (
            data,
            self.network,
            self.id,
            self.term_queue,
            path
        )
        self.sorting_process = mp.Process(
            target=sorting_executor,
            args=exec_args
        )
        self.sorting_process.start()
        self.check_timer.timeout.connect(self.check_queue)
        self.check_timer.start(100)

    def check_queue(self) -> None:
        try:
            message = self.term_queue.get_nowait()

        except queue.Empty:
            return

        match message[0]:

            case Term.DONE:

                eb.emit(f"sorting_done_{self.id}")
                self.check_timer.stop()
                if self.sorting_process.is_alive():
                    self.sorting_process.terminate()

            case Term.CRASHED:

                eb.emit(f"sorting_crashed_{self.id}", message[1], self.id)
                self.check_timer.stop()
                if self.sorting_process.is_alive():
                    self.sorting_process.terminate()

