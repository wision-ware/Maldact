import numpy as np


class SlidingWindowSampler:

    def __init__(self, sampling_rate: int, frame_width: int, frame_height: int = 0) -> None:
        self.sampling_rate = sampling_rate
        self.frame_width = frame_width
        self.frame_height = frame_height

    # @asynchronous
    def create_samples(self, file_name: str, save_location: str, callback=None) -> None:
        pass

    # @asynchronous
    def create_from_object(self, array_object: np.ndarray, callback=None) -> None:
        pass

