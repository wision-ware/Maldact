# Maldact documentation

## Overview

Maldact is a versatile tool designed to assist scientists in classifying large datasets quickly and efficiently. It emphasizes simplicity, maintainability, modularity, and automation. At its core, Maldact features a custom-built feedforward artificial neural network (ANN) backend, allowing users to train custom models and apply them to actual data. This project is funded and led by the Department of Space Physics, Institute of Atmospheric Physics, CAS, and developed by Vavřinec Kavan, a Computer Science bachelors student at the Faculty of Mathematics and Physics, Charles University (CUNI).

## Installation

### Step 1. - Install Anaconda

To install the app, you will firstly need to install Anaconda, preferably the newest version possible.

- [Download Anaconda](https://www.anaconda.com/products/individual)

### Step 2. - Create and activate a Virtual Environment

Then you create a new environment by running: 

```bash
conda create -n myenv
```

inside the `Anaconda Prompt`. Then activate it by running 

```bash
conda activate myenv
```

Alternatively you can install it into the `base` environment, but that's generally not recommended.

### Step 3. - Download a relase

You need to download a `.tar.bz` file from the `dist/` directory of the repository to a known location.

### Step 4. - Install the package

Now simply run 

```bash
conda install path/to/your/build
```

then you can verify the installation by running

```bash
maldact
```

## Usage

#TODO