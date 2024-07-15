import subprocess
import re
import os


def get_conda_packages():
    # Get the list of installed packages in the conda environment
    result = subprocess.run(['conda', 'list', '--export'], stdout=subprocess.PIPE)
    conda_packages = result.stdout.decode('utf-8').splitlines()

    # Extract only package names and versions
    conda_packages = [re.split(r'=|==', line)[0] for line in conda_packages if not line.startswith('#')]
    return conda_packages


def get_pipreqs_packages():
    # Run pipreqs to generate the requirements.txt
    subprocess.run(['pipreqs', '.', '--force'])

    # Read the generated requirements.txt
    with open(os.path.join('..', 'requirements.txt'), 'r') as file:
        pipreqs_packages = file.readlines()

    # Extract only package names
    pipreqs_packages = [re.split(r'=|==', line.strip())[0] for line in pipreqs_packages]
    return pipreqs_packages


def filter_used_packages(conda_packages, pipreqs_packages):
    # Filter out only the packages used in the project
    used_packages = [pkg for pkg in conda_packages if pkg in pipreqs_packages]
    return used_packages


def write_requirements_txt(packages):
    # Write the used packages to requirements.txt
    with open(os.path.join('..', 'requirements.txt'), 'w') as file:
        for pkg in packages:
            file.write(f"{pkg}\n")


if __name__ == "__main__":
    conda_packages = get_conda_packages()
    pipreqs_packages = get_pipreqs_packages()
    used_packages = filter_used_packages(conda_packages, pipreqs_packages)
    write_requirements_txt(used_packages)
    print("requirements.txt has been generated with used packages.")