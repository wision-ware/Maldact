
# Maldact

**MAchine Learning DAta Classification Tool**

## Overview

Maldact is a cross-platform client-server architecture designed for machine learning inference. Built as a distributed system, it allows users to train, package, and deploy ML models for time-series data processing—whether piping in live streaming telemetry over a network or processing local files for batch classification.

Engineered with a strict focus on memory management, network throughput, and low-allocation processing, Maldact utilizes a dual-engine machine learning backend:

- **TorchSharp** for sequential deep learning architectures (1D-CNNs, GRUs).
- **ML.NET** for tabular and tree-based ensembles (Random Forests, XGBoost).

To maintain modularity and separation of concerns, the system is driven by four primary JSON configuration schemas: the **Preprocessing Contract**, **Model Specification**, **Training Configuration**, and **Server Configuration**.

## 1. The System Lifecycle

Deploying a model in Maldact follows a step-by-step workflow from raw data to live inference:

1. **Assess & Design:** Evaluate the raw data and available compute infrastructure. Based on this, define a **Preprocessing Contract** (handling operations like imputation and reshaping) and a **Model Specification** (defining the neural topology or ensemble parameters).
2. **Format the Dataset:** Construct the raw data and manifests to match the Maldact dataset structure.
3. **Compile the Data:** Run the `dataset process` command. The CLI applies the Preprocessing Contract to the raw data and compiles it into a processed, training-ready dataset.
4. **Train & Package:** Define a **Training Configuration** (setting epochs, batch sizes, and hardware targets) and execute the training pipeline using the `model train` command. The system trains the model, tunes the consolidation process, and bundles the trained weights, the Model Specification, and the Preprocessing Contract into a single deployment artifact (`.zip`).
5. **Deploy the Server:** Define a **Server Configuration** (user and admin keys, and resource limits) and boot the Maldact server, injecting the deployment artifact.
6. **Execute Inference:** Connect the Maldact client to the server to begin processing. Data can be submitted by streaming live binary telemetry directly into the network port or by reading local files.

## 2. Quick Start

Maldact is operated via a Command-Line Interface (CLI). A standard method to initialize a model is to use the global configuration manager to bind the JSON files, process the data, and start the server.

*Note: The following examples utilize standard Bash syntax. The Maldact commands are identical across Linux and Windows environments, with the exception of standard input piping in PowerShell (addressed in Step 5).*

**Prerequisites:** 
* **If using the compiled Release Binaries:** No dependencies required. Maldact is distributed compiled for each OS.
* **If building from source:** The .NET 10.0 SDK must be installed.
* A raw dataset directory containing the `.bin` files and a `dataset.json` manifest.

### Step 1: Bind Your Configurations

Maldact relies on four core JSON configuration files. While these can be passed directly to commands, using the `config set` commands is recommended for local development.

> **Note:**
> The `config set` commands save paths to a local index on the machine. Do not use these commands in automated CI/CD pipelines; instead, pass the file paths directly using command flags (e.g., `--config`).

```bash
# Register configuration files to the global index
maldact config set preprocessing-contract ./configs/contract.json
maldact config set model-specification ./configs/modelSpec.json
maldact config set training-configuration ./configs/training.json
maldact config set server-configuration ./configs/server.json

# Verify the registered paths
maldact config list
```

### Step 2: Process the Dataset

Maldact requires raw binary telemetry to be compiled into a strictly formatted, tensor-ready structure. Execute the CLI against the folder containing the data to apply the Preprocessing Contract and output a compiled training dataset.

```bash
# Compiles the raw data into a 'processed' subdirectory by default
maldact dataset process ./my-raw-dataset
```

### Step 3: Train and Package

Once the data is processed, trigger the training engine. This command reads the Training Configuration, trains the model, and packages it alongside the Preprocessing Contract and Model Specification into a single deployment `.zip` artifact.

```bash
# Syntax: maldact model train [datasetDir] [modelZip]
maldact model train ./my-raw-dataset/processed ./artifact.zip
```

### Step 4: Boot the Server

Start the Maldact server, allocate a port for control commands, another for streaming data, and inject the deployment artifact.

```bash
# Syntax: maldact server start [controlPort] [streamingPort] [deploymentArtifactZip]
maldact server start 5000 5001 ./artifact.zip
```

### Step 5: Connect and Infer

Open a separate terminal to act as the client. Connect securely to the server's control channel, start streaming binary data into the pipeline, and query the results.

> **Warning:**
> Windows PowerShell implicitly treats the standard pipe operator (`|`) as text-based, which corrupts raw binary data. PowerShell users must use the `-f` flag to stream directly from a file to bypass this encoding layer.

```bash
# Connect using the control port and the server authentication token
maldact connect 127.0.0.1 5000 admin-token-xyz

# Linux/Bash: Pipe data into the streaming channel via standard input
cat live_telemetry.bin | maldact stream

# Windows/PowerShell: Stream directly from the file to bypass text encoding
maldact stream -f live_telemetry.bin

# Fetch the most recent inference logs
maldact results latest
```

## 3. Dataset

To process data, Maldact expects a flat directory containing raw binary data files and a single master manifest named `dataset.json`.

### The Raw Binary Files (`.bin`)

Maldact avoids text-parsing overhead by reading data as raw, contiguous **IEEE 32-bit floating-point arrays**.

If the data has 4 sensor channels (e.g., Acceleration X, Y, Z, and Temperature), the `.bin` file must be a flat array of floats where every 4 values represent a single temporal frame.

### The Manifest (`dataset.json`)

The manifest specifies how to interpret the binary files, their recording frequency, and where labeled events occur in time (overlapping events are supported for both training and inference targets). All keys are case-sensitive and require `camelCase` formatting.

**Required Fields:**

- `datasetName`: A string identifier for the records.
- `globalSampleRateHz`: A float representing the frequency of the data (e.g., `100.0` for 100 samples per second).
- `classes`: An array of strings defining the possible output targets.
- `trainingStreams` / `crossValidationStreams`: Arrays defining which binary files to load, their length, and the labeled events within them. Both arrays must be non-empty.

**Example `dataset.json` Structure:**

```json
{
  "datasetName": "Signal_Analysis_v1",
  "globalSampleRateHz": 100.0,
  "classes": [ "Active", "Anomaly" ],
  "trainingStreams": [
    {
      "fileName": "monday_recording.bin",
      "durationMs": 120000,
      "events": [
        { "class": "Active", "startMs": 5000, "endMs": 15000 },
        { "class": "Active", "startMs": 17000, "endMs": 48000 },
        { "class": "Anomaly", "startMs": 42000, "endMs": 43500 }
      ]
    }
  ],
  "crossValidationStreams": [
    {
      "fileName": "tuesday_recording.bin",
      "durationMs": 60000,
      "events": [
        { "class": "Active", "startMs": 5000, "endMs": 50000 },
        { "class": "Anomaly", "startMs": 42000, "endMs": 43500 }
      ]
    }
  ]
}
```

## 4. Configuration File Reference

To maintain isolation between data processing, neural topology, training execution, and server deployment, Maldact is driven by four distinct JSON configuration schemas. All keys are case-insensitive but follow standard `camelCase` conventions.

### A. Model Specification

The **Model Specification** defines the architectural structure, the algorithm engine, and the time-series window parameters used during sliding inference operations.

```json
{
  "name": "Classifier_GRU_v1",
  "algorithm": "gru",
  "inputDimension": 4,
  "outputDimension": 2,
  "windowSize": 50,
  "windowStride": 25,
  "overlapPoolingMethod": "max",
  "gru": {
    "hiddenSize": 64,
    "numLayers": 2,
    "dropout": 0.2
  }
}
```

> **Tip:**
> For sequential deep learning models, setting larger window sizes can improve performance by allowing higher strides (the window overlap should remain constant to serve as a clearance margin), thereby reducing recomputed frames. However, this requires more system memory and may impact the accuracy of recurrent models like GRUs.

#### Field Details

|**Field**|**Type**|**Description**|
|---|---|---|
|`name`|String|**Required.** Unique human-readable identifier for the model.|
|`algorithm`|String|**Required.** Core ML engine. Allowed values: `"gru"`, `"cnn"`, `"treeEnsemble"`.|
|`inputDimension`|Integer|**Required.** Number of raw features per timestep ($\ge 1$).|
|`outputDimension`|Integer|**Required.** Total number of target classification classes ($\ge 1$).|
|`windowSize`|Integer|**Required.** Number of consecutive timesteps evaluated per forward pass.|
|`windowStride`|Integer|**Required.** Step size used to advance the sliding window across the time series.|
|`overlapPoolingMethod`|String|Aggregation rule for overlapping window predictions. Options: `"max"`, `"average"`. Defaults to `"max"`.|

#### Algorithm-Specific Parameters

Depending on the chosen `algorithm`, exactly one of the following sub-objects must be supplied:

##### 1. `gru` Parameters

- `hiddenSize` (Integer): Dimensionality of the hidden state sequence ($\ge 1$).
- `numLayers` (Integer): Number of vertically stacked recurrent layers ($\ge 1$).
- `dropout` (Float): Dropout probability applied to the output layers ($[0.0, 1.0]$).

##### 2. `cnn` Parameters

- `channelSizes` (Array of Integers): Sequence of output channel sizes for each consecutive 1D-convolutional layer. Must contain at least 1 element.
- `kernelSize` (Integer): Spatial size of the 1D convolving kernel ($\ge 1$).
- `stride` (Integer): Stride of the convolution along the timeline ($\ge 1$).

##### 3. `tree` Parameters

- `ensembleType` (String): The tree variant deployed. Options: `"randomForest"`, `"xgBoost"`.
- `numberOfTrees` (Integer): Count of decision trees constructed within the ensemble. Defaults to 100.
- `maxDepth` (Integer): Maximum allowed traversal depth for any individual tree. Defaults to 6.

### B. Preprocessing Contract

The **Preprocessing Contract** outlines an ordered, sequential Digital Signal Processing (DSP) pipeline that converts raw telemetry into formatted features matching the model's structure.

```json
{
  "inputDimension": 2,
  "inputSampleRate": 100.0,
  "pipeline": [
    {
      "type": "imputation",
      "imputation": { "method": "forwardFill" }
    },
    {
      "type": "smoothing",
      "smoothing": { "method": "movingAverage", "windowSize": 5 }
    }
  ],
  "finalReshaping": {
    "method": "strict",
    "targetDimension": 2
  }
}
```

#### Field Details

| **Field** | **Type** | **Description** |
| ----------------- | -------- | ---------------------------------------------------------------------------------------- |
| `inputDimension`  | Integer  | **Required.** Features per frame in the raw data files.                                  |
| `inputSampleRate` | Float    | **Required.** Sampling frequency in Hertz (Hz) of the incoming telemetry.                |
| `pipeline`        | Array    | **Required.** Ordered transformation chain containing `step` objects.                    |
| `finalReshaping`  | Object   | **Required.** Final dimensionality enforcement schema applied after pipeline completion. |

#### Pipeline Step Options (`pipeline[]`)

Each step within the array must declare a `type`. Depending on the type selected, the corresponding options block must be populated:

- **`imputation`**: Cleans missing or invalid data values.
    - `method` (String): `"forwardFill"` (carries forward the last valid sample) or `"zeroFill"` (pads with zeroes).
- **`smoothing`**: Mitigates high-frequency operational noise.
    - `method` (String): `"movingAverage"` or `"exponentialSmoothing"`.
    - `windowSize` (Integer): Step window count ($\ge 2$).
    - `gamma` (Float, Optional): Decay parameter for exponential calculations ($[0.0001, 1.0]$).
- **`resampling`**: Alters the time-domain sampling frequency.
    - `targetHz` (Float): Target frequency rate.
    - `aggregationFunctionUsed` (String): Metric used when downsampling. Options: `"mean"`, `"min"`, `"max"`, `"first"`, `"last"`. Defaults to `"mean"`.
- **`normalization`**: Adjusts numeric scaling layouts.
    - `method` (String): `"minMax"` or `"zScore"`.
    - Fixed bounds (if omitted, bounds are calculated dynamically per data chunk): `minBound`, `maxBound`, `stdDev`, `mean`.
- **`fourierTransform`**: Computes spectral transformations into the frequency domain.
    - `windowSize` (Integer): Sample count needed prior to running FFT ($\ge 2$).
    - `numberOfFrequenciesToKeep` (Integer): Top low-end frequency bins kept ($\ge 1$).
    - `includePhase` (Boolean): Flag to include phase angles alongside magnitudes. Defaults to `false`.
- **`reshaping`** / **`finalReshaping`**: Structural dimension modification tool.
    - `method` (String): `"interpolation"`, `"strict"`, `"truncateOrZeroFill"`.
    - `targetDimension` (Integer): Expected number of layout channels.
- **`rescaling`**: Element-wise scaling manipulations.
    - `method` (String): `"linear"`, `"logarithmic"`, `"exponential"`.
    - `multiplier` / `offset` (Float): Used in linear scaling formulas ($Y = \text{old} \times \text{multiplier} + \text{offset}$).

### C. Training Configuration

The **Training Configuration** determines the optimization execution limits, device acceleration settings, and the hyperparameter tuning targets used during training cycles.

```json
{
  "consolidationAlgorithms": "all",
  "maxTuningCycles": 50,
  "device": "cpu",
  "maxEpochs": 100,
  "batchSize": 32,
  "batchesPerEpoch": 500,
  "learningRate": 0.001,
  "patience": 10,
  "randomSeed": 42
}
```

#### Field Details

| **Field** | **Type** | **Description** |
| ------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `consolidationAlgorithms` | String   | Target tuning models optimized to filter output spikes. Values: `"none"`, `"basicAttention"`, `"exponentialHysteresis"`, `"slidingWindowHysteresis"`, `"all"`. Defaults to `"all"`. |
| `maxTuningCycles`         | Integer  | Boundary limit for tuning cycles executed during consolidator hyperparameter grid search. Defaults to maximum integer limits.                                       |
| `device`                  | String   | Hardware calculation component targeted. Options: `"cpu"`, `"cuda"`. Defaults to `"cpu"`.                                                                           |
| `maxEpochs`               | Integer  | **Required.** Boundary limit for full data collection traversals ($\ge 1$).                                                                                         |
| `batchSize`               | Integer  | **Required.** Collection sequences evaluated per single backpropagation step ($\ge 1$).                                                                             |
| `batchesPerEpoch`         | Integer  | Optional number of discrete batches evaluated before completing a technical training epoch.                                                                         |
| `learningRate`            | Float    | Step increment used by loss optimization algorithms. Range: $[0.000001, 1.0]$. Defaults to 0.001.                                                                   |
| `patience`                | Integer  | Early-stopping epoch count allowed before training terminates if no validation loss gains are seen ($\ge 1$). Defaults to 10.                                       |
| `randomSeed`              | Integer  | Optional seed utilized to force identical repeatable sequence setups.                                                                                               |

> **Warning:**
> Using a fixed seed can introduce a performance penalty as it disables parallelism in certain parts of the system. It is recommended only where reproducibility is strictly required.

### D. Server Configuration

The **Server Configuration** establishes network parameters, access keys, and in-memory log retention bounds for running the server instance.

```json
{
  "serverId": "node-prague-01",
  "adminKeys": [
    "secure-admin-secret-xyz"
  ],
  "userKeys": [
    "standard-user-token-abc",
    "standard-user-token-def"
  ],
  
  // Note: Eviction engine is pending implementation. 
  // It is recommended to run regular `query delete` commands during sustained server operation.
  "maxResultBacklogCount": 5000,
  "maxResultRetentionMinutes": 120
}
```

#### Field Details

| **Field** | **Type** | **Description** |
| --------------------------- | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `serverId`                  | String           | **Required.** Globally unique node identifier token used across system records. Cannot be empty.                                                               |
| `adminKeys`                 | Array of Strings | **Required.** List of secret key tokens authorized to access system administrative routines (`server stop`, configuration updates, etc.).                      |
| `userKeys`                  | Array of Strings | **Required.** List of secret key tokens authorized to access standard commands (`connect`, `stream`, `results`).                                               |
| `maxResultBacklogCount`     | Integer          | Upper storage limit tracking raw processed logs in volatile cache arrays before removing old entries ($\ge 1$). Defaults to 10000.                             |
| `maxResultRetentionMinutes` | Integer          | Maximum age cutoff in minutes for individual output records kept in cache memories before automatic eviction occurs ($\ge 1$). Defaults to 60.                 |

## 5. Command-Line Interface Reference

The Maldact CLI is structured into logical branches. Append `-h` or `--help` to any command or branch to view context-specific documentation directly in the terminal.

### Global Configuration Management (`config`)

These commands manage the local configuration index, allowing users to bind default settings for local development without repeatedly passing file paths.

- **`maldact config list`**
    Prints the state of the global configuration index, verifying which file paths are bound to the system and validating their integrity.
- **`maldact config set <type> [configFilePath]`**
    Binds a JSON configuration file to the global index.
    - *Supported Types:* `model-specification`, `preprocessing-contract`, `training-configuration`, `server-configuration`.
    - *Tip:* Passing the explicit string `"null"` as the file path will purge that configuration slot from the index.
- **`maldact config get <type>`**
    Prints the currently bound file path for the specified configuration type.

### Data & Training (`dataset`, `model`)

Commands for transforming raw binary telemetry and generating deployment artifacts.

- **`maldact dataset process [datasetDirectory]`**
    Ingests raw binary streams and a `dataset.json` manifest, applying the Preprocessing Contract to generate compiled training tensors.
    - `-c, --contract <path>`: Override the active Preprocessing Contract.
    - `-o, --output <path>`: Custom destination for the compiled dataset (defaults to a `processed` subfolder).
    - `-p, --parallelism <int>`: Max concurrent file streams to process (defaults to the CPU core count).
- **`maldact model train [datasetDir] [modelZip]`**
    Executes the training loop against a processed dataset and produces a `.zip` deployment artifact containing the weights, Preprocessing Contract, and Model Specification.
    - `--train-config <path>`: Override the active Training Configuration.
    - `--model-spec <path>`: Override the active Model Specification.
    - `--metrics-log <path>`: Optional file path to export detailed epoch training metrics.

### Server Node Management (`server`)

Commands for booting and monitoring the inference engine.

- **`maldact server start [controlPort] [streamingPort] [deploymentArtifactZip]`**
    Initializes the server, binding listeners and loading the compiled neural topology.
    - `[controlPort]`: TCP port for authenticated administrative/query commands.
    - `[streamingPort]`: TCP port dedicated to receiving continuous raw telemetry.
    - `--config <path>`: Override the active Server Configuration.
- **`maldact server status`**
    Fetches a local diagnostic snapshot of the running server instance.
    - `-o, --output <path>`: Export the raw JSON diagnostics to a specific file.
- **`maldact server stop`**
    Gracefully tears down the network listeners, flushes the result buffers, and shuts down the connected server node. **Requires an admin key to execute.**

### Client Connections & Streaming (`connect`, `stream`)

Commands to establish secure channels to a running Maldact server and submit data.

- **`maldact connect [host] [port] [authToken]`**
    Authenticates the client against the server's control port and stores the routing state locally.
- **`maldact status`**
    Checks the health and latency of the active connection to the remote server and prints a dashboard frame.
- **`maldact stream`**
    Negotiates a secure streaming slot and begins reading raw binary data continuously, piping it directly to the server's inference engine.
    - *Example Linux/Bash usage:* `cat my_data.bin | maldact stream`
    - `-f, --file <path>`: Bypasses standard input and streams raw binary data directly from the specified file. **Mandatory for Windows/PowerShell users** to prevent text-encoding data corruption inherent to the PowerShell pipe operator.
- **`maldact disconnect`**
    Severs the control channel and purges the active connection state from the local machine.

### Inference Results Querying (`results`)

Tools to retrieve, filter, and purge temporal classification events processed by the server. Returned result entries are formatted as follows:

```
[S<start-time> - E<end-time> | C<centroid-time>] ID <result-id> SCORE <result-score> RESULT <result-class>
```

- **`maldact results latest`**
    Fetches the most recent chronological inference events currently held in the server's volatile memory.
- **`maldact results get [result-ids...]`**
    Retrieves specific event logs using their unique string IDs (space-separated).
- **`maldact results delete [result-ids...]`**
    Forcefully deletes specific event logs from the server's memory.
- **`maldact results query`**
    Executes a filter query against the server's internal memory backlog. Time parameters can be provided as standard ISO-8601 date-time strings or as bare integers, which the system implicitly evaluates as absolute milliseconds.
    - `-m, --min, --min-timestamp <time>`: Isolate events occurring *after* this timestamp.
    - `-x, --max, --max-timestamp <time>`: Isolate events occurring *before* this timestamp.
    - `-f, --filter, --filter-classes <classes>`: Isolate explicitly named target classes (space-separated).
- **`maldact results query delete`**
    Applies the exact same filtering flags (`-m`, `-x`, `-f`) but **deletes** the matching records from the server rather than retrieving them.

## Academic Context

Maldact was developed as a Bachelor's thesis project at the **Faculty of Mathematics and Physics (Matfyz), Charles University**, specifically for use by scientists from the **Department of Space Physics, Institute of Atmospheric Physics, CAS**. Its primary objective is bridging the gap between enterprise ML serving frameworks and an accessible, easily automated CLI experience.