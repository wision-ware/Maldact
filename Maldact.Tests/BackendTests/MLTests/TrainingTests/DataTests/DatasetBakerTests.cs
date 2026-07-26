using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.DataTests;

/// <summary>
/// Verifies path validation constraints, multi-threaded worker limits, and atomic serialization states within the dataset baking engine.
/// </summary>
public sealed class DatasetBakerTests : IDisposable
{
    private readonly string _testWorkspaceRoot;
    private readonly string _sourceDirectory;
    private readonly string _targetDirectory;
    private readonly PreprocessingContract _validContract;

    public DatasetBakerTests()
    {
        // isolate disk operations securely per execution thread pass
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"Maldact_BakerTests_{Guid.NewGuid():N}");
        _sourceDirectory = Path.Combine(_testWorkspaceRoot, "raw");
        _targetDirectory = Path.Combine(_testWorkspaceRoot, "baked");

        Directory.CreateDirectory(_sourceDirectory);

        _validContract = new PreprocessingContract
        {
            InputDimension = 4,
            InputSampleRate = 100.0,
            FinalReshaping = new ReshapingOptions { Method = ReshapingOptions.ReshapeMethod.Strict, TargetDimension = 4 }
        };
    }

    /// <summary>
    /// Disposes of any leftover localized test workspace files to maintain continuous test runner environment hygiene.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testWorkspaceRoot))
        {
            try { Directory.Delete(_testWorkspaceRoot, recursive: true); } catch { /* sweep file locks silently */ }
        }
    }

    [Fact]
    public async Task ProcessDatasetAsync_MissingSourceManifest_ThrowsFileNotFoundException()
    {
        // arrange
        var baker = new DatasetBaker();

        // act & assert
        await FluentActions.Awaiting(() => baker.ProcessDatasetAsync(_sourceDirectory, _targetDirectory, _validContract, 2))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ProcessDatasetAsync_ValidWorkspace_ExecutesBakingAndOutputsStampedManifest()
    {
        // arrange
        var trainFile = "train_session_01.dat";
        var cvFile = "cv_session_01.dat";
        
        var rawTrainPath = Path.Combine(_sourceDirectory, trainFile);
        var rawCvPath = Path.Combine(_sourceDirectory, cvFile);

        // write out separate binary source streams to prevent file-lock collisions
        float[] dummyData = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f];
        byte[] rawBytes = new byte[dummyData.Length * sizeof(float)];
        Buffer.BlockCopy(dummyData, 0, rawBytes, 0, rawBytes.Length);
        
        await File.WriteAllBytesAsync(rawTrainPath, rawBytes);
        await File.WriteAllBytesAsync(rawCvPath, rawBytes);

        var manifestPayload = new DatasetManifest
        {
            DatasetName = "Alpha_Test_Set",
            GlobalSampleRateHz = 100.0,
            Classes = ["nominal", "anomaly"],
            TrainingStreams = [new StreamManifest { FileName = trainFile, DurationMs = 80.0 }],
            CrossValidationStreams = [new StreamManifest { FileName = cvFile, DurationMs = 80.0 }]
        };

        string manifestJson = JsonSerializer.Serialize(manifestPayload, DatasetJsonContext.Default.DatasetManifest);
        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "dataset.json"), manifestJson);

        var baker = new DatasetBaker();

        // act
        await baker.ProcessDatasetAsync(_sourceDirectory, _targetDirectory, _validContract, maxDegreeOfParallelism: 4);

        // assert
        File.Exists(Path.Combine(_targetDirectory, trainFile)).Should().BeTrue("the training worker stream must output target binary arrays.");
        File.Exists(Path.Combine(_targetDirectory, cvFile)).Should().BeTrue("the cross-validation worker stream must output target binary arrays.");
        
        string resultManifestPath = Path.Combine(_targetDirectory, "dataset.processed.json");
        File.Exists(resultManifestPath).Should().BeTrue("the orchestrator must serialize the final processed output configuration manifest.");

        string processedJson = await File.ReadAllTextAsync(resultManifestPath);
        var validatedResult = JsonSerializer.Deserialize(processedJson, DatasetJsonContext.Default.DatasetManifest);
        
        validatedResult.Should().NotBeNull();
        validatedResult!.TrainingReady.Should().BeTrue("the preprocessing contract metadata stamp must clear readiness evaluation checks.");
        validatedResult.Preprocessing!.InputDimension.Should().Be(4);
    }

    [Fact]
    public async Task ProcessDatasetAsync_UndefinedContractInputDimension_ThrowsInvalidOperationException()
    {
        // arrange
        var invalidContract = _validContract with { InputDimension = null };
        var baker = new DatasetBaker();

        // act & assert
        await FluentActions.Awaiting(() => baker.ProcessDatasetAsync(_sourceDirectory, _targetDirectory, invalidContract, 2))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}