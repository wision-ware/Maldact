# Maldact documentation

## Overview

Maldact is a versatile tool designed to assist scientists in classifying large datasets quickly and efficiently. It emphasizes simplicity, maintainability, modularity, and automation. At its core, Maldact features a custom-built feedforward artificial neural network (ANN) backend, allowing users to train custom models and apply them to actual data. This project is funded and led by the Department of Space Physics, Institute of Atmospheric Physics, CAS, and developed by Vavřinec Kavan, a Computer Science bachelors student at the Faculty of Mathematics and Physics, Charles University (CUNI) in Prague.

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

### Step 3. - Clone the repository

You can download the repo directly from the GitHub website or run 

```bash
git clone https://github.com/wision-ware/Maldact.git
```

### Step 4. - Build the package and install it

Now simply navigate to the repository on your machine and run 

```bash
conda build .
conda install --use-local maldact
```

You can then verify your installation by running

```bash
maldact
```

## Usage

#TODO