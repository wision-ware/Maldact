using FluentAssertions;
using Maldact.Core.ML.Training;

namespace Maldact.Tests.CoreTests.MLTests.TrainingTests;

public class RawTrainingWindowTests
{
    [Fact]
    public void Constructor_EndBeforeStart_ThrowsArgumentException()
    {
        // arrange
        var start = TimeSpan.FromSeconds(10);
        var end = TimeSpan.FromSeconds(5); // invalid

        // act
        Action act = () => new RawTrainingWindow(
            Array.Empty<float[]>(), 
            Array.Empty<AbsoluteEvent>(), 
            start, 
            end);

        // assert
        act.Should().Throw<ArgumentException>().WithMessage("*EndOffset cannot be earlier than StartOffset*");
    }

    [Fact]
    public void DurationProperty_CalculatesCorrectLength()
    {
        // arrange
        var sut = new RawTrainingWindow(
            Array.Empty<float[]>(), 
            Array.Empty<AbsoluteEvent>(), 
            TimeSpan.FromSeconds(5), 
            TimeSpan.FromSeconds(15));

        // act & assert
        sut.Duration.TotalSeconds.Should().Be(10);
    }
}