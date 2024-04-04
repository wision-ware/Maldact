from backend.learn_network import LearnNetwork
from state_enums import ProcessTerminationState as Term

import numpy as np
import traceback
import multiprocessing as mp
import os


def training_executor(
        inp: np.ndarray,
        labels: np.ndarray,
        network_object: LearnNetwork,
        termination_queue: mp.Queue,
        save_location: str,
        **kwargs
):

    try:

        meta = network_object.learn(
            inp,
            labels,
            **kwargs
        )

    except Exception as e:

        exc_type = type(e).__name__
        exc_message = str(e)
        exc_traceback = e.__traceback__

        exception_info = {
            'type': exc_type,
            'message': exc_message,
            'traceback': traceback.format_tb(exc_traceback)
        }
        termination_queue.put((Term.CRASHED, exception_info))
        return 1

    np.save(save_location, meta, allow_pickle=True)
    termination_queue.put((Term.DONE,))


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
        return 1

    dim = out.shape[0]
    for i in range(dim):
        np.save(os.path.join(dir_name, str(np.argmax(out[i, :]))), out[i, :])
    termination_queue.put((Term.DONE, manager_id))
