using FluentAssertions;
using Maldact.Backend.ML.Training.BatchFormatting;
using Maldact.Core.ML;
using Maldact.Core.ML.Training;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.BatchFormattingTests;

public class TargetLabelingMathTests
{
    private readonly string[] _classes = { "Dog", "Cat", "Bird" };

    [Fact]
    public void CalculateWindowTargets_ValidBatch_CalculatesMultiHotEncoding()
    {
        // arrange
        var classDog = new ClassificationClass("Dog");
        var classBird = new ClassificationClass("Bird");
        var classCat = new ClassificationClass("Cat");

        var batch = new List<RawTrainingWindow>
        {
            // Window 1: 0ms to 1000ms
            new RawTrainingWindow(
                features: Array.Empty<float[]>(),
                startOffset: TimeSpan.FromMilliseconds(0),
                endOffset: TimeSpan.FromMilliseconds(1000),
                groundTruthEvents: new List<AbsoluteEvent>
                {
                    // fully inside window -> Dog = 1
                    new() { Class = classDog, StartOffset = TimeSpan.FromMilliseconds(200), EndOffset = TimeSpan.FromMilliseconds(300) },
                    
                    // partially overlaps end of window -> Bird = 1
                    new() { Class = classBird, StartOffset = TimeSpan.FromMilliseconds(900), EndOffset = TimeSpan.FromMilliseconds(1100) }
                }
            ),
            // Window 2: 1000ms to 2000ms
            new RawTrainingWindow(
                features: Array.Empty<float[]>(),
                startOffset: TimeSpan.FromMilliseconds(1000),
                endOffset: TimeSpan.FromMilliseconds(2000),
                groundTruthEvents: new List<AbsoluteEvent>
                {
                    // fully inside window -> Cat = 1
                    new() { Class = classCat, StartOffset = TimeSpan.FromMilliseconds(1500), EndOffset = TimeSpan.FromMilliseconds(1600) }
                }
            )
        };

        // act
        var result = TargetLabelingMath.CalculateWindowTargets(batch, _classes, windowDurationMs: 1000);

        // assert
        // Expected shape: [batchSize (2) * numClasses (3)] = 6
        // Batch 0 (0-1000): Dog [Idx 0], Bird [Idx 2] are active. -> [1, 0, 1]
        // Batch 1 (1000-2000): Cat [Idx 1] is active. -> [0, 1, 0]
        result.Should().BeEquivalentTo(new float[]
        {
            1f, 0f, 1f,   // b0
            0f, 1f, 0f    // b1
        });
    }

    [Fact]
    public void CalculateSequenceTargets_ValidBatch_MapsGranularTimesteps()
    {
        // arrange
        var classDog = new ClassificationClass("Dog"); // Idx 0

        var batch = new List<RawTrainingWindow>
        {
            // Window 1: 0ms to 1000ms. Sequence length 4 (250ms per step)
            new RawTrainingWindow(
                features: Array.Empty<float[]>(),
                startOffset: TimeSpan.FromMilliseconds(0),
                endOffset: TimeSpan.FromMilliseconds(1000),
                groundTruthEvents: new List<AbsoluteEvent>
                {
                    // Event spans 100ms to 400ms. 
                    // Step 0 (0-250) -> Active
                    // Step 1 (250-500) -> Active
                    // Step 2 (500-750) -> Inactive
                    // Step 3 (750-1000) -> Inactive
                    new() { Class = classDog, StartOffset = TimeSpan.FromMilliseconds(100), EndOffset = TimeSpan.FromMilliseconds(400) }
                }
            )
        };

        // act
        var result = TargetLabelingMath.CalculateSequenceTargets(batch, _classes, windowDurationMs: 1000, seqLen: 4);

        // assert
        // Expected shape: [batchSize (1) * seqLen (4) * numClasses (3)] = 12
        result.Should().BeEquivalentTo(new float[]
        {
            1f, 0f, 0f,  // step 0 (Dog)
            1f, 0f, 0f,  // step 1 (Dog)
            0f, 0f, 0f,  // step 2 (Empty)
            0f, 0f, 0f   // step 3 (Empty)
        });
    }

    [Fact]
    public void CalculateSequenceTargets_EventLargerThanWindow_TruncatesSafelyToWindowBounds()
    {
        // arrange
        var classCat = new ClassificationClass("Cat"); // Idx 1
        var batch = new List<RawTrainingWindow>
        {
            new RawTrainingWindow(
                features: Array.Empty<float[]>(),
                startOffset: TimeSpan.FromMilliseconds(1000),
                endOffset: TimeSpan.FromMilliseconds(2000),
                groundTruthEvents: new List<AbsoluteEvent>
                {
                    // Event completely engulfs the window (500ms to 2500ms)
                    new() { Class = classCat, StartOffset = TimeSpan.FromMilliseconds(500), EndOffset = TimeSpan.FromMilliseconds(2500) }
                }
            )
        };

        // act
        // 2 steps total (500ms per step)
        var result = TargetLabelingMath.CalculateSequenceTargets(batch, _classes, windowDurationMs: 1000, seqLen: 2);

        // assert
        // Every sequence step in the window should have the Cat active, without throwing OutOfBounds exceptions.
        result.Should().BeEquivalentTo(new float[]
        {
            0f, 1f, 0f,  // step 0
            0f, 1f, 0f   // step 1
        });
    }
}