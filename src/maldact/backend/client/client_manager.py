

class ClientManager:

    @classmethod
    def ping_server(cls, ip, port) -> dict:
        """
        Pings any Maldact server on a given socket

        :param ip:
        :param port:
        :return:
        """
        return {}

    @classmethod
    def initialize(cls) -> None:
        """
        Loads necessary configuration into memory.

        :return: None
        """
        pass

    @classmethod
    def send_request(cls, port, message) -> None:
        """
        Sends a preprocessed request (.json format) to a server on a given socket.

        :param port: assigned port of the server
        :param message: request to the server containing a .json message
        :return:
        """
        pass

    @classmethod
    def process_cli_command(cls, **kwargs) -> None:
        """
        Processes the server CLI command. Accepts already parsed arguments as keyword arguments.

        :param kwargs: keyword arguments of the command
        :return: None
        """
        pass
