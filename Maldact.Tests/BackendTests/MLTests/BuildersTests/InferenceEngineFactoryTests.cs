using System.Runtime.Serialization.DataContracts;
using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Packaging;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Training;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the artifact parsing, algorithm routing, and defensive payload validation 
/// of the deployment factory.
/// </summary>
public class InferenceEngineFactoryTests
{
    /// <summary>
    /// Ensures defensive initialization rejects null artifacts and missing essential contracts.
    /// (Simulates edge cases where JSON deserializers bypass the 'required' keyword constraints).
    /// </summary>
    [Fact]
    public void Constructor_NullOrInvalidArtifact_ThrowsArgumentExceptions()
    {
        // act & assert
        FluentActions.Invoking(() => new InferenceEngineFactory(null!))
            .Should().Throw<ArgumentNullException>();

        // We explicitly instantiate the artifact here to avoid the helper's null-coalescing (??) logic
        FluentActions.Invoking(() => new InferenceEngineFactory(new DeploymentArtifact
        {
            Specification = null!, // Explicitly null
            Contract = CreateValidContract(),
            Parameters = new TorchModelParameters(Array.Empty<byte>()),
            ConsolidatorConfiguration = new DummyConfiguration {ClassNames = [], FrameRateHz = 10}
        })).Should().Throw<ArgumentException>().WithMessage("*Model Specification*");

        FluentActions.Invoking(() => new InferenceEngineFactory(new DeploymentArtifact
        {
            Specification = CreateValidSpec(ModelSpecification.AlgorithmType.Gru),
            Contract = null!, // Explicitly null
            Parameters = new TorchModelParameters(Array.Empty<byte>()),
            ConsolidatorConfiguration = new DummyConfiguration {ClassNames = [], FrameRateHz = 10}
        })).Should().Throw<ArgumentException>().WithMessage("*Data Contract*");
    }

    /// <summary>
    /// Verifies that a TreeEnsemble configuration containing the wrong parameter DTO safely aborts 
    /// instead of crashing with a blind InvalidCastException.
    /// </summary>
    [Fact]
    public void Constructor_TreeEnsembleWithMismatchedParameters_ThrowsInvalidOperationException()
    {
        // arrange
        var artifact = CreateBaseArtifact(
            spec: CreateValidSpec(ModelSpecification.AlgorithmType.TreeEnsemble),
            parameters: new TorchModelParameters(Array.Empty<byte>()) // Mismatch! Expects TreeModelParameters
        );

        // act & assert
        FluentActions.Invoking(() => new InferenceEngineFactory(artifact))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*do not match the TreeEnsemble*");
    }

    /// <summary>
    /// Verifies that an empty model payload is caught before causing opaque ML.NET binary exceptions.
    /// </summary>
    [Fact]
    public void Constructor_TreeEnsembleWithEmptyBytes_ThrowsInvalidOperationException()
    {
        // arrange
        var artifact = CreateBaseArtifact(
            spec: CreateValidSpec(ModelSpecification.AlgorithmType.TreeEnsemble),
            parameters: new TreeModelParameters(Array.Empty<byte>()) // Empty payload trap
        );

        // act & assert
        FluentActions.Invoking(() => new InferenceEngineFactory(artifact))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*byte payload is empty*");
    }

    /// <summary>
    /// Ensures that if the Torch routing branch receives the wrong parameter DTO, it aborts cleanly.
    /// </summary>
    [Fact]
    public void Create_TorchAlgorithmWithMismatchedParameters_ThrowsInvalidOperationException()
    {
        // arrange
        var artifact = CreateBaseArtifact(
            spec: CreateValidSpec(ModelSpecification.AlgorithmType.Cnn),
            parameters: new TreeModelParameters(new byte[] { 0x01, 0x02 }) // Mismatch!
        );
        
        var sut = new InferenceEngineFactory(artifact);

        // act & assert
        FluentActions.Invoking(() => sut.Create(new StreamTime(TimeSpan.Zero)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*do not match Torch algorithm types*");
    }

    /// <summary>
    /// Ensures unknown or newly added algorithms trigger a clear NotSupportedException.
    /// </summary>
    [Fact]
    public void Create_UnsupportedAlgorithm_ThrowsNotSupportedException()
    {
        // arrange
        var artifact = CreateBaseArtifact(
            spec: CreateValidSpec((ModelSpecification.AlgorithmType)999),
            parameters: new TorchModelParameters(new byte[] { 0x01 })
        );
        
        var sut = new InferenceEngineFactory(artifact);

        // act & assert
        FluentActions.Invoking(() => sut.Create(new StreamTime(TimeSpan.Zero)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*is not supported for inference*");
    }

    // --- SETUP HELPERS ---

    private static ModelSpecification CreateValidSpec(ModelSpecification.AlgorithmType algo) => new()
    {
        Name = "UnitTestModel",
        Algorithm = algo,
        InputDimension = 10,
        OutputDimension = 2,
        WindowSize = 32,
        WindowStride = 16,
        OverlapPoolingMethod = ModelSpecification.ResultOverlapTimePoolingMethod.Max
    };

    private static PreprocessingContract CreateValidContract() => new()
    {
        Pipeline = new List<PreprocessingStep>(),
        FinalReshaping = new ReshapingOptions { Method = ReshapingOptions.ReshapeMethod.Strict, TargetDimension = 10 },
        InputDimension = 10,
        InputSampleRate = 100.0
    };

    /// <summary>
    /// Constructs a valid baseline DeploymentArtifact. Passing null to optional parameters allows 
    /// tests to inject malformed state to test factory guard clauses.
    /// </summary>
    private static DeploymentArtifact CreateBaseArtifact(
        ModelSpecification? spec = null, 
        PreprocessingContract? contract = null, 
        IModelParameters? parameters = null) 
    {
        return new DeploymentArtifact
        {
            Specification = spec ?? CreateValidSpec(ModelSpecification.AlgorithmType.Gru),
            Contract = contract ?? CreateValidContract(),
            Parameters = parameters ?? new TorchModelParameters(new byte[] { 0x00 }),
            ConsolidatorConfiguration = new DummyConfiguration()
            {
                ClassNames = [],
                FrameRateHz = 10
            }
        };
    }

    // Minimal stub to satisfy the required ConsolidatorConfiguration constraint
    private record class DummyConfiguration : ConsolidatorConfiguration
    {
        public IResultConsolidator BuildConsolidator(StreamTime sessionStartTime) 
            => throw new NotImplementedException();
    }
}