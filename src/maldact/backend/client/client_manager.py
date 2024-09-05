import os
import zmq
import time


class ClientManager:

    @classmethod
    def ping_server(cls, ip, port) -> dict:
        """
        Check the accessibility of any Maldact server on a given socket

        :param ip: server machine IP
        :param port: server comm port
        :return: dict containing the success acknowledgement and latency
        """
        context = zmq.Context()
        socket = context.socket(zmq.REQ)
        socket.connect(f"tcp://{ip}:{port}")

        timeout = 5000  # Timeout in milliseconds (5 seconds)
        start_time = time.time()

        # Send ping
        socket.send_string("ping")

        while True:
            try:
                # Nonblocking check to poll for an answer
                pong = socket.recv_string(flags=zmq.NOBLOCK)
                # Terminate polling after successfully receiving the 'pong' answer
                if pong == "pong":
                    return {"success": True, "latency": (time.time() - start_time) * 1000}
            except zmq.Again:
                # No message received yet
                if (time.time() - start_time) * 1000 > timeout:
                    break
                time.sleep(0.01)  # Briefly sleep to avoid busy-waiting

        return {"success": False, "latency": None}

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
