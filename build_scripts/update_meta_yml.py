import subprocess
import re
import os
from pathlib import Path

conda_executable = 'conda'


def get_conda_dependencies():
    # Get the list of installed packages in the current conda environment
    result = subprocess.run([conda_executable, 'list', '--export'], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode != 0:
        print("Error retrieving Conda dependencies:", result.stderr.decode('utf-8'))
        return []

    conda_packages = result.stdout.decode('utf-8').splitlines()

    # Extract package names and versions
    packages = []
    for line in conda_packages:

        line = re.sub(r'\x1b\[[0-9;]*[mK]', '', line)
        if not line.startswith('#'):
            parts = re.split(r'=|==', line.strip())
            package_name = parts[0].strip()
            if len(parts) > 1:
                package_version = parts[1].strip()
            else:
                package_version = ''
            if parts[0] != '':
                packages.append((package_name, package_version))

    return packages


def write_meta_yaml(packages, meta_yaml_path):
    run_requirements = [f"{name}={version}" for name, version in packages]
    requirement_string = '\n    - '.join(run_requirements)

    meta_yaml_content = f"""
package:
  name: Maldact
  version: "0.1.0"

source:
  path: ..

build:
  script: "{{{{ PYTHON }}}} -m pip install . --no-deps"
  noarch: python

requirements:
  build:
    - python
    - pip
  run:
    - {requirement_string}

test:
  imports:
    - pytest

about:
  home: https://github.com/wision-ware/Maldact
  license: MIT
  license_file: LICENSE
  summary: "Machine learning data classification tool"
  description: |
    A GUI and CLI tool used to train and use simple ANN models for classifying discrete and timeseries data.
  doc_url: https://github.com/wision-ware/Maldact
  dev_url: https://github.com/wision-ware/Maldact

extra:
  recipe-maintainers:
    - Vavrinec Kavan
"""
    with open(meta_yaml_path, 'w') as file:
        file.write(meta_yaml_content)


if __name__ == "__main__":
    conda_packages = get_conda_dependencies()
    meta_yaml_path = Path(os.path.join('..', 'meta.yaml'))
    meta_yaml_path.parent.mkdir(parents=True, exist_ok=True)
    write_meta_yaml(conda_packages, meta_yaml_path)
    print(f"meta.yaml has been generated with Conda dependencies at {meta_yaml_path}")