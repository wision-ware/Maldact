using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Packaging;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Tests.BackendTests.MLTests.PackagingTests;

/// <summary>
/// Verifies the zero-disk streaming, compression, polymorphic deserialization, and deep validation mechanics of the ArtifactManager.
/// </summary>
public class ArtifactManagerTests
{
    [Fact]
    public async Task CreateAndLoad_ValidArtifact_MaintainsDataIntegrity()
    {
        // arrange
        string zipPath = Path.Combine(Path.GetTempPath(), $"test_artifact_{Guid.NewGuid()}.zip");
        var originalArtifact = CreateValidDummyArtifact();

        try
        {
            // act: export
            await ArtifactManager.CreateDeploymentZipAsync(originalArtifact, zipPath);

            // assert: export physical presence
            File.Exists(zipPath).Should().BeTrue();
            new FileInfo(zipPath).Length.Should().BeGreaterThan(0);

            // act: import
            var finishedArtifact = await ArtifactManager.LoadDeploymentZipAsync(zipPath);

            // assert: import integrity and polymorphic restoration
            finishedArtifact.Should().NotBeNull();
            finishedArtifact.Specification.Name.Should().Be(originalArtifact.Specification.Name);
            finishedArtifact.Specification.Algorithm.Should().Be(ModelSpecification.AlgorithmType.Cnn);
            
            finishedArtifact.ConsolidatorConfiguration.Should().BeOfType<BasicConfiguration>();
            ((BasicConfiguration)finishedArtifact.ConsolidatorConfiguration).Threshold.Should().Be(0.8f);

            // verify native tensor boundaries survived zero-copy binary streaming
            var originalWeights = originalArtifact.Parameters.ToBytes();
            var hydratedWeights = finishedArtifact.Parameters.ToBytes();
            
            hydratedWeights.Length.Should().Be(originalWeights.Length);
            hydratedWeights.Span.SequenceEqual(originalWeights.Span).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }
    
    [Fact]
    public async Task CreateDeploymentZipAsync_InvalidSpecification_ThrowsValidationException()
    {
        // arrange
        string zipPath = Path.Combine(Path.GetTempPath(), $"invalid_artifact_{Guid.NewGuid()}.zip");
        
        // clone a valid artifact but strip a deeply nested [Required] constraint
        var invalidArtifact = CreateValidDummyArtifact() with
        {
            Specification = new ModelSpecification
            {
                Name = "InvalidModel",
                Algorithm = ModelSpecification.AlgorithmType.Gru, // requires GruParameters
                InputDimension = 10,
                OutputDimension = 2,
                WindowSize = 50,
                WindowStride = 10,
                Gru = null // forces shallow validation failure on the component model
            }
        };

        try
        {
            // act
            Func<Task> act = async () => await ArtifactManager.CreateDeploymentZipAsync(invalidArtifact, zipPath);

            // assert
            await act.Should().ThrowAsync<ValidationException>();
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }
    
    [Fact]
    public async Task LoadDeploymentZipAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // arrange
        string badPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}.zip");

        // act
        Func<Task> act = async () => await ArtifactManager.LoadDeploymentZipAsync(badPath);

        // assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
    
    [Fact]
    public async Task LoadDeploymentZipAsync_MissingWeightsEntry_ThrowsInvalidDataException()
    {
        // arrange
        string zipPath = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.zip");
        var dummySpec = CreateValidDummyArtifact().Specification;

        try
        {
            // manually construct a malformed zip archive omitting the binary weights
            await using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("model_spec.json");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync(JsonSerializer.Serialize(dummySpec));
            }

            // act
            Func<Task> act = async () => await ArtifactManager.LoadDeploymentZipAsync(zipPath);

            // assert
            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Constructs a fully compliant artifact graph satisfying all [Required] attributes for E2E streaming tests.
    /// </summary>
    private static DeploymentArtifact CreateValidDummyArtifact()
    {
        byte[] dummyWeights = new byte[256];
        new Random(42).NextBytes(dummyWeights);

        return new DeploymentArtifact
        {
            Specification = new ModelSpecification 
            { 
                Name = "TestCnn",
                Algorithm = ModelSpecification.AlgorithmType.Cnn,
                InputDimension = 10,
                OutputDimension = 2,
                WindowSize = 50,
                WindowStride = 10,
                Cnn = new CnnParameters
                {
                    ChannelSizes = new[] { 16, 32 },
                    KernelSize = 3,
                    Stride = 1
                }
            },
            Contract = new PreprocessingContract 
            {
                InputDimension = 10,
                InputSampleRate = 100.0,
                Pipeline = new List<PreprocessingStep> 
                {
                    new PreprocessingStep 
                    {
                        Type = PreprocessingStep.PreprocessingStepType.Reshaping,
                        Reshaping = new ReshapingOptions 
                        {
                            Method = ReshapingOptions.ReshapeMethod.Strict,
                            TargetDimension = 10
                        }
                    }
                },
                FinalReshaping = new ReshapingOptions 
                {
                    Method = ReshapingOptions.ReshapeMethod.Strict,
                    TargetDimension = 10
                }
            },
            ConsolidatorConfiguration = new BasicConfiguration 
            {
                FrameRateHz = 10.0,
                ClassNames = new[] { "ClassA", "ClassB" },
                Threshold = 0.8f,
                HangFrames = 5
            },
            Parameters = new TorchModelParameters(dummyWeights)
        };
    }
}