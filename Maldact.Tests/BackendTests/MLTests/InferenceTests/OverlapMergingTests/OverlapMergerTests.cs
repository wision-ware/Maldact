using FluentAssertions;
using Maldact.Backend.ML.Inference.OverlapMerging;

namespace Maldact.Tests.BackendTests.MLTests.InferenceTests.OverlapMergingTests;

/// <summary>
/// Verifies the mathematical accuracy and defensive parity checks of the zero-allocation overlap mergers.
/// </summary>
public class OverlapMergerTests
{
    /// <summary>
    /// Ensures the AverageMerger strictly calculates the mean of both spans in-place.
    /// </summary>
    [Fact]
    public void AverageMerger_EqualSpans_CalculatesMeanInPlace()
    {
        // arrange
        Span<float> head = new float[] { 0.2f, 0.8f, 0.5f };
        ReadOnlySpan<float> tail = new float[] { 0.4f, 0.2f, 0.5f };
        var sut = new AverageMerger();

        // act
        sut.Merge(head, tail);

        // assert
        head.ToArray().Should().BeEquivalentTo(new[] { 0.3f, 0.5f, 0.5f }, "the average of the head and tail must be calculated per-class.");
    }

    /// <summary>
    /// Ensures the MaxMerger strictly retains the highest confidence value.
    /// </summary>
    [Fact]
    public void MaxMerger_EqualSpans_RetainsMaximumValueInPlace()
    {
        // arrange
        Span<float> head = new float[] { 0.2f, 0.8f, 0.5f };
        ReadOnlySpan<float> tail = new float[] { 0.4f, 0.2f, 0.9f };
        var sut = new MaxMerger();

        // act
        sut.Merge(head, tail);

        // assert
        head.ToArray().Should().BeEquivalentTo(new[] { 0.4f, 0.8f, 0.9f }, "the maximum of the head and tail must be retained per-class.");
    }

    /// <summary>
    /// Verifies defensive guard clauses prevent memory access violations on span length mismatches.
    /// </summary>
    [Fact]
    public void Mergers_MismatchedSpans_ThrowArgumentExceptions()
    {
        // arrange
        var head = new float[] { 0.1f };
        var tail = new float[] { 0.1f, 0.2f };

        var avg = new AverageMerger();
        var max = new MaxMerger();

        // act & assert
        FluentActions.Invoking(() => avg.Merge(head, tail))
            .Should().Throw<ArgumentException>("spans must have identical lengths.");

        FluentActions.Invoking(() => max.Merge(head, tail))
            .Should().Throw<ArgumentException>("spans must have identical lengths.");
    }
}