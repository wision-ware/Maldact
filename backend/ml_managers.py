from backend.learn_network import LearnNetwork
from backend.network import Network
from backend.async_workers import training_executor
from event_bus import EventBus as eb
from state_enums import ProcessTerminationState as Term

import numpy as np
# import cupy as cp
import multiprocessing as mp
import inspect
import os
import queue
import PyQt5.QtCore as qtc
import traceback


class TrainingManager:

    __slots__ = (
        "id", "network", "N", "GPU", "GD", "time_limit", "save_params", "data_file",
        "model_dir", "model_name", "training_process", "term_queue", "check_timer",
        "threshold", "batch_size", "fixed_iter", "eta", "live_monitor", "as_text",
        "dia_data", "overwrite"
    )
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

        # overwrite the default values
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
        # --------------------------------------------------------------------------------------------------------------

        # packing the arguments for the subprocess
        exec_args = (
            inp,
            labels,
            self.network,
            self.id,
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
            if value := self.__dict__[name] is not None:
                exec_kwargs[name] = value

        self.training_process = mp.Process(
            target=training_executor,
            args=exec_args,
            kwargs=exec_kwargs
        )

        self.training_process.start()
        self.check_timer.timeout.connect(self.check_queue)
        self.check_timer.start(100)

    def executor(self, inp, labels) -> None:

        arg_filter = lambda x: not callable(x) and not isinstance(x, LearnNetwork)
        kwargs = {key: value for key, value in self.__dict__.items() if arg_filter(value)}
        try:

            meta = self.network.learn(
                inp,
                labels,
                **kwargs
            )

        except Exception as e:

            exc_type = type(e).__name__
            exc_message = str(e)
            exc_traceback = traceback.format_exc()

            exception_info = {
                'type': exc_type,
                'message': exc_message,
                'traceback': exc_traceback
            }

            self.term_queue.put(("crashed", exception_info, self.id))
            return None

        np.save(os.path.join(self.model_dir, self.model_name), meta, allow_pickle=True)
        self.term_queue.put(("done",))

    def exit_training(self) -> None:
        pass  # TODO

    def check_queue(self) -> None:
        try:
            message = self.term_queue.get()
            match message[0]:

                case Term.DONE:
                    eb.emit(f"training_done_{self.id}")
                    self.check_timer.stop()
                    if self.training_process.is_alive():
                        self.training_process.terminate()

                case Term.CRASHED:
                    eb.emit(f"training_crashed_{self.id}", message[1])
                    self.check_timer.stop()
                    if self.training_process.is_alive():
                        self.training_process.terminate()

        except queue.Empty:
            pass


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

        try:
            if self.sorting_process.is_alive() is True:
                return None
        except AttributeError:
            pass

        self.network.load_params(self.model_file)
        self.dir_name = f"sorted_by_{self.model_file.os.path.basename(self.model_file)}"
        os.mkdir(os.path.join(self.sort_dir, self.dir_name))
        data = np.load(self.data_file)
        term_queue = mp.Queue()
        for i in range(self.network.N[-1]):
            os.mkdir(os.path.join(self.dir_name, str(i)))
        exec_args = (
            data,
            term_queue
        )
        self.sorting_process = mp.Process(
            target=self.executor,
            args=exec_args
        )
        self.sorting_process.start()

    def executor(self, data, queue) -> None:

        try:

            out = self.network.get_output(data)

        except Exception as e:

            exc_type = type(e).__name__
            exc_message = str(e)
            exc_traceback = traceback.format_exc()

            exception_info = {
                'type': exc_type,
                'message': exc_message,
                'traceback': exc_traceback
            }

            self.term_queue.put(("crashed", exception_info))
            return

        dim = out.shape[0]
        for i in range(dim):
            np.save(os.path.join(self.dir_name, str(np.argmax(out[i, :]))), out[i, :])
        queue.put("done")

    def check_queue(self) -> None:
        try:
            message = self.term_queue.get()
            match message[0]:

                case Term.DONE:

                    eb.emit(f"sorting_done_{self.id}")
                    self.check_timer.stop()
                    if self.sorting_process.is_alive():
                        self.sorting_process.terminate()

                case Term.CRASHED:

                    eb.emit(f"sorting_crashed_{self.id}", message[1])
                    self.check_timer.stop()
                    if self.sorting_process.is_alive():
                        self.sorting_process.terminate()

        except queue.Empty:
            pass

