from backend.learn_network import LearnNetwork
from state_enums import ProcessTerminationState as Term

import numpy as np
import traceback
import multiprocessing as mp
import os


def training_executor(
        inp: np.ndarray,
        labels: np.ndarray,
        class_dict: dict,
        network_object: LearnNetwork,
        manager_id: int,
        termination_queue: mp.Queue,
        save_location: str
):

    arg_filter = lambda x: not callable(x) and not isinstance(x, LearnNetwork)
    kwargs = {key: value for key, value in class_dict.items() if arg_filter(value)}
    try:

        meta = network_object.learn(
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

        termination_queue.put((Term.CRASHED, exception_info, manager_id))
        return None

    np.save(save_location, meta, allow_pickle=True)
    termination_queue.put((Term.DONE, manager_id))


def sorting_executor(
        data: np.ndarray,
        network_object: LearnNetwork,
        manager_id: int,
        termination_queue: mp.Queue,
        dir_name: str
):
    try:

        out = network_object.get_output(data)

    except Exception as e:

        exc_type = type(e).__name__
        exc_message = str(e)
        exc_traceback = traceback.format_exc()

        exception_info = {
            'type': exc_type,
            'message': exc_message,
            'traceback': exc_traceback
        }

        termination_queue.put((Term.CRASHED, exception_info, manager_id))
        return

    dim = out.shape[0]
    for i in range(dim):
        np.save(os.path.join(dir_name, str(np.argmax(out[i, :]))), out[i, :])
    termination_queue.put((Term.DONE, manager_id))
