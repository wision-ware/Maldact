# Maldact documentation

## Overview

This project is an attempt to crate a universal tool to aid scientists with classifying large amounts of data quickly and efficiently. Focusing on simplicity, maintainability, modularity and automation. It features a from scratch custom built feedforward ANN backend that allows for training custom models and then using them on actual data. The project is funded and led by the Department of Space Physics, Institute of Atmospheric Physics, CAS and developed by Vavřinec Kavan, a university student of Computer Science on the Faculty of Mathematics and Physics, CUNI.

## Installation

### Step 1. - Install Anaconda

To install the app, you will firstly need to install Anaconda, preferably the newest version possible.

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

### Step 3. - Download a wheel

You need to download a `.wheel` file from the `dist/` directory of the repository to a known location.

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