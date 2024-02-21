
from ...backend.learn_network import LearnNetwork
import pytest as pt
import numpy as np
import os


class TestLearnNetwork:

    test_data = {}
    data_dir = os.path.join(
        "tests",
        "test_data",
        "training"
    )
    for file in os.listdir(data_dir):
        test_data[str(file[:-4])] = np.load(os.path.join(data_dir, file), allow_pickle=True).item()

    result_cache = []

    @staticmethod
    def initialize_network(data: dict, hidden_layers: tuple) -> LearnNetwork:
        N = [data["input"].shape[1]]
        for _ in range(hidden_layers[0]):
            N.append(hidden_layers[1])
        N.append(data["labels"].shape[1])
        net = LearnNetwork(N=N, GPU=False)
        return net

    @pt.mark.parametrize("data", test_data.values())
    @pt.mark.parametrize("hidden_layers", [(1, 2), (2, 1), (5, 3), (6, 6)])
    def test_network_init(self, data: dict, hidden_layers: tuple) -> None:
        _ = TestLearnNetwork.initialize_network(data, hidden_layers)

    @pt.mark.parametrize("data", test_data.values())
    @pt.mark.parametrize("hidden_layers", [(1, 2), (2, 1), (5, 3), (6, 6)])
    def test_training_execution(self, data: dict, hidden_layers: tuple) -> None:
        """
        tests the execution of the `learn` method of `LearnNetwork` class
        """
        net = TestLearnNetwork.initialize_network(data, hidden_layers)
        return_dict = net.learn(
            data["input"],
            data["labels"],
            save_params=False,
            time_limit=1
        )
        self.result_cache.append(return_dict)

    def test_training_results(self):
        pass
