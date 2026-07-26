using FluentAssertions;
using Maldact.Backend.ML.Training.BatchFormatting;
using Maldact.Core.ML.Training;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.BatchFormattingTests;

public class CnnLabeledBatchFormatterTests
{
    private readonly string[] _classes = { "ClassA", "ClassB" };
    private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(1);

    [Fact]
    public void Constructor_NullOrEmptyClasses_ThrowsException()
    {
        // arrange & act
        Action actNull = () => new CnnLabeledBatchFormatter(null!, _windowDuration);
        Action actEmpty = () => new CnnLabeledBatchFormatter([], _windowDuration);

        // assert
        actNull.Should().Throw<ArgumentNullException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_NullBatch_ThrowsArgumentNullException()
    {
        // arrange
        var sut = new CnnLabeledBatchFormatter(_classes, _windowDuration);

        // act
        Action act = () => sut.Format(null!);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Format_EmptyBatch_ReturnsEmptySingleton()
    {
        // arrange
        var sut = new CnnLabeledBatchFormatter(_classes, _windowDuration);
        var emptyBatch = new List<RawTrainingWindow>();

        // act
        var result = sut.Format(emptyBatch);

        // assert
        result.Should().BeSameAs(FormattedLabeledBatch.Empty);
    }

    [Fact]
    public void Format_JaggedSequenceLengths_ThrowsInvalidOperationException()
    {
        // arrange
        var sut = new CnnLabeledBatchFormatter(_classes, _windowDuration);
        
        var batch = new List<RawTrainingWindow>
        {
            CreateMockWindow(seqLen: 2, featureDim: 2),
            CreateMockWindow(seqLen: 3, featureDim: 2) 
        };

        // act
        Action act = () => sut.Format(batch);

        // assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jagged batch*timesteps*");
    }

    [Fact]
    public void Format_JaggedFeatureDimensions_ThrowsInvalidOperationException()
    {
        // arrange
        var sut = new CnnLabeledBatchFormatter(_classes, _windowDuration);
        
        var badFeatures = new[]
        {
            new[] { 1f, 2f },      // step 1: 2 features
            new[] { 1f, 2f, 3f }   // step 2: 3 features (jagged!)
        };
        
        var batch = new List<RawTrainingWindow>
        {
            new(badFeatures, [], TimeSpan.Zero, TimeSpan.FromSeconds(1))
        };

        // act
        Action act = () => sut.Format(batch);

        // assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jagged batch*features*");
    }

    [Fact]
    public void Format_ValidBatch_CorrectlyTransposesFeaturesAndCalculatesShapes()
    {
        // arrange
        var sut = new CnnLabeledBatchFormatter(_classes, _windowDuration);
        
        // Batch size: 1
        // Sequence Length: 3 (timesteps)
        // Feature Dim: 2 (channels)
        var windowFeatures = new[]
        {
            new[] { 1f, 10f }, // step 0 (f1=1, f2=10)
            new[] { 2f, 20f }, // step 1 (f1=2, f2=20)
            new[] { 3f, 30f }  // step 2 (f1=3, f2=30)
        };
        
        var batch = new List<RawTrainingWindow>
        {
            new(windowFeatures, [], TimeSpan.Zero, TimeSpan.FromSeconds(1))
        };

        // act
        var result = sut.Format(batch);

        // assert
        // Feature Shape Validation -> [Batch (1), Features (2), SeqLen (3)]
        result.FeatureShape.Should().BeEquivalentTo(new long[] { 1, 3, 2 });
        
        // Target Shape Validation -> [Batch (1), SeqLen (3), NumClasses (2)]
        result.TargetShape.Should().BeEquivalentTo(new long[] { 1, 3, 2 });

        // Memory flattening exact match (Transposed!)
        result.FlattenedFeatures.Should().BeEquivalentTo([
            1f, 10f,
            2f, 20f,
            3f, 30f
        ], options => options.WithStrictOrdering());
        
        
        
        result.FlattenedTargets.Should().OnlyContain(t => t == 0f);
    }

    // Helper to spin up dummy windows with predictable array sizes
    private static RawTrainingWindow CreateMockWindow(int seqLen, int featureDim)
    {
        var features = new float[seqLen][];
        for (int i = 0; i < seqLen; i++)
        {
            features[i] = new float[featureDim];
        }
        return new RawTrainingWindow(features, [], TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
}