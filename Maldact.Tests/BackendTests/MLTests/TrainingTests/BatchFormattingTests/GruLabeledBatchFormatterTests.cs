using FluentAssertions;
using Maldact.Backend.ML.Training.BatchFormatting;
using Maldact.Core.ML.Training;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.BatchFormattingTests;

public class GruLabeledBatchFormatterTests
{
    private readonly string[] _classes = { "ClassA", "ClassB" };
    private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(1);

    [Fact]
    public void Constructor_NullOrEmptyClasses_ThrowsException()
    {
        // arrange & act
        Action actNull = () => new GruLabeledBatchFormatter(null!, _windowDuration);
        Action actEmpty = () => new GruLabeledBatchFormatter([], _windowDuration);

        // assert
        actNull.Should().Throw<ArgumentNullException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_NullBatch_ThrowsArgumentNullException()
    {
        // arrange
        var sut = new GruLabeledBatchFormatter(_classes, _windowDuration);

        // act
        Action act = () => sut.Format(null!);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Format_EmptyBatch_ReturnsEmptySingleton()
    {
        // arrange
        var sut = new GruLabeledBatchFormatter(_classes, _windowDuration);
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
        var sut = new GruLabeledBatchFormatter(_classes, _windowDuration);
        
        // Window 1 has 2 timesteps, Window 2 has 3 timesteps
        var batch = new List<RawTrainingWindow>
        {
            CreateMockWindow(seqLen: 2, featureDim: 2),
            CreateMockWindow(seqLen: 3, featureDim: 2) 
        };

        // act
        Action act = () => sut.Format(batch);

        // assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jagged batch detected*timesteps*");
    }

    [Fact]
    public void Format_JaggedFeatureDimensions_ThrowsInvalidOperationException()
    {
        // arrange
        var sut = new GruLabeledBatchFormatter(_classes, _windowDuration);
        
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
           .WithMessage("*Jagged batch detected*features*");
    }

    [Fact]
    public void Format_ValidBatch_CorrectlyFlattensFeaturesAndCalculatesShapes()
    {
        // arrange
        var sut = new GruLabeledBatchFormatter(_classes, _windowDuration);
        
        // Batch size: 2
        // Sequence Length: 2
        // Feature Dim: 3
        var window1Features = new[]
        {
            new[] { 1f, 2f, 3f }, // w1 step 0
            new[] { 4f, 5f, 6f }  // w1 step 1
        };
        
        var window2Features = new[]
        {
            new[] { 7f, 8f, 9f },    // w2 step 0
            new[] { 10f, 11f, 12f }  // w2 step 1
        };

        var batch = new List<RawTrainingWindow>
        {
            new(window1Features, [], TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            new(window2Features, [], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
        };

        // act
        var result = sut.Format(batch);

        // assert
        // Feature Shape Validation
        result.FeatureShape.Should().BeEquivalentTo(new long[] { 2, 2, 3 });
        
        // Target Shape Validation [BatchSize (2), SeqLen (2), NumClasses (2)]
        result.TargetShape.Should().BeEquivalentTo(new long[] { 2, 2, 2 });

        // Memory flattening exact match
        result.FlattenedFeatures.Should().BeEquivalentTo(new float[]
        {
            1f, 2f, 3f,     // w1 s0
            4f, 5f, 6f,     // w1 s1
            7f, 8f, 9f,     // w2 s0
            10f, 11f, 12f   // w2 s1
        }, options => options.WithStrictOrdering());
        
        // Since no ground truth events were passed, targets should be perfectly zeroed
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