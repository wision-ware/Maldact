import zmq
import yaml
import json
import os
import argparse
import multiprocessing as mp


def load_config(config_path):
    with open(config_path, "r") as f:
        return yaml.safe_load(f)


def run_server(host, port):
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind(f"tcp://{host}:{port}")
    
    print(f"Server running on {host}:{port}")
    
    while True:
        message = socket.recv_string()
        print(f"Received request: {message}")
        # Process the request (example: echo the message)
        response = f"Processed: {message}"
        socket.send_string(response)


def main(**kwargs):



    with open(static_config) as scf:
        pass

    if 'from_config_file' in kwargs:
        with open(kwargs.get('from_config_file')) as icf:
            pass

    run_server(config['server']['host'], config['server']['port'])

    server_kwargs = {}

    server_proc = mp.Process(target=server_worker, kwargs=server_kwargs)


def server_worker():

