import zmq
import yaml
import argparse

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

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run the server with the specified configuration.")
    parser.add_argument('--config', type=str, default="config.yaml", help="Path to the configuration file")
    
    args = parser.parse_args()
    config = load_config(args.config)
    
    run_server(config['server']['host'], config['server']['port'])