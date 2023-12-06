import pytest

from backend.learn_network import LearnNetwork
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
        test_data[str(file[:-4])] = np.load(file, allow_pickle=True)

    @pytest.mark.parametrize("data", test_data.values())
    @pytest.mark.parametrize("hidden_layers", [])
    def test_training_execution(self, data, hidden_layers):

        N = [data["input"].shape[1]]
        for _ in range(hidden_layers[0]):
            N.append(hidden_layers[1])
        net = LearnNetwork(N=N)

