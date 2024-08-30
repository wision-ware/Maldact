import argparse
import sys
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import PyQt5.QtGui as qtg
from maldact.ui_manager import UIManager as um
from maldact.backend.server import server_main
from maldact.backend.client import client_main


class MainWindow(qtw.QMainWindow):

    def __init__(self) -> None:

        super().__init__()

        self.setWindowTitle("Maldact")
        self.resize(960, 540)

        um.initialize(self)


def print_version() -> None:
    # TODO
    print('<version_here>')


def print_help() -> None:
    # TODO
    print(':)')


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
        parser.add_argument('-i', '--interface', action='store_true',
                            help='Open Maldacts GUI')

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
                                 help='Checks for a local server on a given PID')
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
                                             help='Path to the file containing labeled training data')
        training_request_parser.add_argument('--local-port', type=int,
                                             help='Send the request to a locally running server')
        training_request_parser.add_argument('--socket', type=str,
                                             help='Specify the exact communication socket')

        # Sorting requests parser configuration
        sorting_request_parser = client_subparsers.add_parser('sort-request')
        training_request_parser.add_argument('sorted-dataset', type=str,
                                             help='Path to the file containing the data to be sorted')
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
        print_version()
        return

    if args.interface:
        run_gui_app()
        return

    if args.command == 'server':

        server_script_params: dict = {}

        # fill the parameters for server script
        if args.subcommand == 'start':
            server_script_params['action'] = args.subcommand
            server_script_params['port'] = args.port

        elif args.subcommand == 'stop':
            server_script_params['action'] = args.subcommand
            if args.port:
                server_script_params['running_server_port'] = args.port
            if args.pid:
                server_script_params['running_server_pid'] = args.pid
            if args.all:
                server_script_params['stop_all'] = True
            if args.hard:
                server_script_params['hard_stop'] = True

        elif args.subcommand == 'config':
            server_script_params['action'] = args.subcommand
            if args.running_port:
                server_script_params['running_server_port'] = args.running_port
            if args.config_file:
                server_script_params['from_config_file'] = args.config_file

        elif args.status:
            server_script_params['show_status'] = True

        else:
            print_help()
            return

        # run the server script
        ServerManager.process_cli_command(**server_script_params)

    elif args.command == 'client':

        client_script_params: dict = {}

        # fill the parameters for client script
        if args.subcommand == 'train-request':
            client_script_params['action'] = args.subcommand
            if args.training_dataset:
                # TODO convert to optionally multiple argument
                client_script_params['training_dataset_file'] = args.training_dataset
            if args.local_port:
                client_script_params['local_port'] = args.local_port
            elif args.socket:
                client_script_params['socket'] = args.socket

        elif args.subcommand == 'sort-request':
            client_script_params['action'] = args.subcommand
            if args.sorting_dataset:
                # TODO convert to optionally multiple argument
                client_script_params['sorting_dataset_file'] = args.sorting_dataset
            if args.local_port:
                client_script_params['local_port'] = args.local_port
            elif args.socket:
                client_script_params['socket'] = args.socket

        else:
            print_help()
            return

        # run the server script
        ClientManager.process_cli_command(**client_script_params)

    else:
        print_help()


if __name__ == "__main__":
    main()
