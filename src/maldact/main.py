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

    def configure_main_command(parser):
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

    def configure_server_subcommand(subparsers):

        # Server subcommand parsing configuration
        server_parser = subparsers.add_parser('server', help='Handles local server management')

        server_parser.add_argument('--status', action='store_true',
                                   help='Reports the status of the locally running servers')

        server_subparsers = server_parser.add_subparsers(dest='subcommand', help='Subcommand to run')

        # Start sub subcommand parsing config
        start_parser = server_subparsers.add_parser('start', help='Starts the server')
        start_parser.add_argument('port', type=int, help='Port for the communication')

        # Config sub subcommand parsing config
        config_parser = server_subparsers.add_parser('config',
                                                     help='Edits the starting configuration for a newly started server')
        config_parser.add_argument('--config-file', '-f', type=str,
                                   help='whether to pull the config from a yaml file')
        config_parser.add_argument('--running-port', '-p', type=int,
                                   help='Reconfigure a locally running server')

        # Stop sub subcommand parsing config
        stop_parser = server_subparsers.add_parser('stop',
                                                   help='Stops a running server')
        stop_parser.add_argument('--port', type=int,
                                 help='Attempts to identify a locally running server by its corresponding port')
        stop_parser.add_argument('--pid', type=int,
                                 help='Checks for a server on a given PID')
        stop_parser.add_argument('--all', '-a', action='store_true',
                                 help='Stops all known locally running servers')
        stop_parser.add_argument('--hard', '-h', action='store_true',
                                 help='Doesnt wait for finishing pending requests')

    def configure_client_subcommand(subparsers):
        # Client subcommand parsing configuration
        client_parser = subparsers.add_parser('client', help='Handles all server requests')

        client_subparsers = client_parser.add_subparsers(dest='subcommand', help='Subcommand to run')

        # Training requests parser configuration
        training_request_parser = client_subparsers.add_parser('train-request')
        training_request_parser.add_argument('training-dataset', type=str,
                                             help='')
        training_request_parser.add_argument('--local-port', type=int,
                                             help='Send the request to a locally running server')
        training_request_parser.add_argument('--socket', type=str,
                                             help='Specify the exact communication socket')

        # Sorting requests parser configuration
        sorting_request_parser = client_subparsers.add_parser('sort-request')
        sorting_request_parser.add_argument('--local-port', type=int,
                                            help='Send the request to a locally running server')
        sorting_request_parser.add_argument('--socket', type=str,
                                            help='Specify the exact communication socket')

    parser = argparse.ArgumentParser()

    configure_main_command(parser)

    subparsers = parser.add_subparsers(dest='command', help='Subcommand to run')

    configure_server_subcommand(subparsers)

    configure_client_subcommand(subparsers)

    return parser


def main() -> None:

    parser = get_parser()
    args = parser.parse_args()

    if args.version:
        write_version()
        return

    if args.local_server:

        if args.interface:
            return

        elif args.config_file:
            return

    if args.output_file:
        return

    run_gui_app()

    if args.command == 'server':

        if args.subcommand == 'start':
            pass
        elif args.subcommand == 'stop':
            pass
        else:
            pass

    if args.command == 'client':

        if args.subcommand == 'train-request':
            pass
        elif args.subcommand == 'sort-request':
            pass
        else:
            pass



if __name__ == "__main__":
    main()
