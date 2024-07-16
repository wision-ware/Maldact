import os

# Read requirements.txt
with open(os.path.join('..', 'requirements.txt'), 'r') as file:
    requirements = file.readlines()

    # Format the dependencies for setup.cfg
    install_requires = ""
    for req in requirements:
        install_requires += f"    {req.strip()}\n"

# Update setup.cfg
setup_cfg_content = f"""
[metadata]
name = Maldact
version = 0.1.0
author = Vavrinec Kavan
author_email = vavrinec.kavan@gmail.com
description = Machine learning data classification tool
long_description = file: README.md
long_description_content_type = text/markdown
url = https://github.com/wision-ware/Maldact
project_urls =
    Bug Tracker = https://github.com/wision-ware/Maldact/issues
classifiers =
    Programming Language :: Python :: 3
    License :: OSI Approved :: MIT License
    Operating System :: OS Independent

[options]
packages = find:
python_requires = >=3.6
install_requires =
{install_requires}

[options.extras_require]
dev =
    pytest
    black

[options.package_data]
* = *.txt, *.rst
my_package = example/*.dat

[options.entry_points]
console_scripts =
    maldact = main:main
"""

# Write the updated setup.cfg
with open(os.path.join('..', 'setup.cfg'), 'w') as file:
    file.write(setup_cfg_content)

print("setup.cfg file has been updated with dependencies.")