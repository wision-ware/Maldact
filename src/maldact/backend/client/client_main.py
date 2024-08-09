import zmq
import argparse


def send_request(host, port, message):
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")

    socket.send_string(message)
    response = socket.recv_string()
    print(f"Received reply: {response}")


def main(**kwargs):

    command = kwargs.get('action', '')
    match kwargs['action']:
        case 'sort-request':
            path = kwargs.get('sorting_dataset_file', '.')
            message = f'sort:{path}:'

