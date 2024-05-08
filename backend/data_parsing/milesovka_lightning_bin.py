import numpy as np
import scipy.integrate as integrate
import re

from matplotlib import pyplot as plt


class MilBinParser:

    metadata: dict

    def __init__(self, file_name: str) -> None:
        self.metadata = MilBinParser.__extract_metadata(file_name)

    @staticmethod
    def __extract_metadata(file_name: str) -> dict:

        meta_patterns = {
            "sampling_rate": r"^[\t\s]+Sampling[\t\s]+freq:[\t\s]+\d+[\s\t]*$",
            "sample_count": r"^[\t\s]+Sample[\t\s]+count:[\t\s]+\d+[\s\t]*$",
            "channels": r"^[\t\s]*Mode:[\t\s]+\d+[\s\t]*$",
            "resolution": r"^[\t\s]+Resolution:[\t\s]+\d+[\s\t]*$"
        }
        metadata = {}

        with open(file_name, "r") as f:
            for line in f:
                for key in meta_patterns.keys():
                    if re.match(meta_patterns[key], line):
                        metadata[key] = line.split(":")[1]

        return metadata

    def read_binary(self, file_name: str, raw: bool = False) -> np.ndarray:

        channels = int(self.metadata["channels"])
        sample_count = int(self.metadata["sample_count"])
        resolution = int(self.metadata["resolution"])

        value_bias = 1 << (resolution - 1)
        data = np.zeros((channels, sample_count))

        with open(file_name, "rb") as f:
            for i in range(channels):
                for j in range(sample_count):
                    value = int.from_bytes(f.read(2), byteorder="little", signed=False) - value_bias
                    data[i, j] = value

        if raw: return data
        else: return integrate.cumulative_simpson(data, axis=1)


# just for testing ----------------------------------------------------------------------------------

def main():
    config_file = r"C:\Users\vavri\Documents\OKF\Testing\parse_data\20190731_191113.txt"
    data_file = r"C:\Users\vavri\Documents\OKF\Testing\parse_data\20190731_191113.bin"

    parser = MilBinParser(config_file)
    output = parser.read_binary(data_file)

    correction0 = np.linspace(output[0, 0], output[0, -1], output.shape[1])
    correction1 = np.linspace(output[1, 0], output[1, -1], output.shape[1])

    x_ax = np.arange(output.shape[1])

    fig, ax = plt.subplots(2, 1)
    # ax[0].plot(x_ax, output[0, :] - correction0)
    # ax[1].plot(x_ax, output[1, :] - correction1)
    ax[0].plot(x_ax, output[0, :])
    ax[1].plot(x_ax, output[1, :])

    plt.show()


if __name__ == "__main__":
    main()
