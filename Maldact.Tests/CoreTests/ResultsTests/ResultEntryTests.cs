using FluentAssertions;
using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Tests.CoreTests.ResultsTests;

/// <summary>
/// Verifies instantiation and layout constraints for the ResultEntry DTO.
/// </summary>
public class ResultEntryTests
{
    [Fact]
    public void Constructor_BindsPropertiesCorrectly()
    {
        // arrange
        var targetClass = new ClassificationClass("TestFault");
        var start = new StreamTime("1000");
        var end = new StreamTime("5000");
        var centroid = new StreamTime("3500");
        float score = 0.95f;
        string id = Guid.NewGuid().ToString("N");

        // act
        var entry = new ResultEntry(targetClass, start, end, centroid, score, id);

        // assert
        entry.Classification.Should().Be(targetClass);
        entry.StartTime.Should().Be(start);
        entry.EndTime.Should().Be(end);
        entry.CentroidTime.Should().Be(centroid);
        entry.Score.Should().Be(score);
        entry.Id.Should().Be(id);

        // ensure ToString executes without crashing
        entry.ToString().Should().Contain("TestFault").And.Contain(id);
    }
}