import argparse
import sys
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import PyQt5.QtGui as qtg
from maldact.ui_manager import UIManager as um


class MainWindow(qtw.QMainWindow):

    def __init__(self) -> None:

        super().__init__()

        self.setWindowTitle("Maldact")
        self.resize(960, 540)

        um.initialize(self)


def write_version() -> None:
    print('<version_here>')


def run_gui_app() -> None:

    app = qtw.QApplication(sys.argv)
    default_font = qtg.QFont("Arial", 10)

    app.setAttribute(qtc.Qt.AA_EnableHighDpiScaling)
    app.setFont(default_font)

    window = MainWindow()
    window.show()
    sys.exit(app.exec_())


def run_server(config) -> None:

    pass


def get_parser() -> argparse.ArgumentParser:

    parser = argparse.ArgumentParser()

    parser.add_argument('--version', action='store_true',
                        help='Get the version information of the application')
    parser.add_argument('-s', '--local-server', action='store_true',
                        help='Launch a sorting server that gets configured and then waits for requests')
    parser.add_argument('-i', '--interface', action='store_true',
                        help='Open a GUI configurator for the daemon')
    parser.add_argument('-c', '--config-file', type=str,
                        help='Read the configuration from a config file')
    parser.add_argument('-o', '--output-file', type=str,
                        help='File to store the classification results')

    return parser


def main() -> None:

    parser = get_parser()
    parser.parse_args()

    if parser.version:
        write_version()
        return

    if parser.local_server:

        if parser.interface:
            return

        elif parser.config_file:
            return

    if parser.output_file:
        return

    run_gui_app()


if __name__ == "__main__":
    main()
