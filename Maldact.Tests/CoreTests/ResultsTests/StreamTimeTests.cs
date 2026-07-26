using FluentAssertions;
using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Tests.CoreTests.ResultsTests;

/// <summary>
/// Verifies string parsing, cross-type safety boundaries, and operator overloads for the StreamTime struct.
/// </summary>
public class StreamTimeTests
{

    [Theory]
    [InlineData("2024-01-01T12:00:00Z", true)] // ISO-8601 Absolute (UTC)
    [InlineData("00:01:30", false)]            // TimeSpan String
    [InlineData("1500", false)]                // Raw Milliseconds
    public void Constructor_StringParsing_DetectsCorrectTypeAndValues(string input, bool expectAbsolute)
    {
        // act
        var sut = new StreamTime(input);

        // assert
        sut.IsAbsolute.Should().Be(expectAbsolute);

        if (expectAbsolute)
        {
            sut.AsAbsolute().Kind.Should().Be(DateTimeKind.Utc);
            sut.AsAbsolute().Should().Be(DateTime.Parse("2024-01-01T12:00:00Z").ToUniversalTime());
        }
        else if (input == "00:01:30")
        {
            sut.AsRelative().Should().Be(TimeSpan.FromSeconds(90));
        }
        else // "1500"
        {
            sut.AsRelative().Should().Be(TimeSpan.FromMilliseconds(1500));
            sut.TotalMilliseconds.Should().Be(1500);
        }
    }


    [Fact]
    public void Constructor_InvalidString_ThrowsFormatException()
    {
        // arrange
        string badData = "Not-A-Time-Value";

        // act
        Action act = () => new StreamTime(badData);

        // assert
        act.Should().Throw<FormatException>();
    }
    
    [Fact]
    public void MathOperators_AdditionAndSubtraction_MaintainBaseType()
    {
        // arrange
        var absolute = new StreamTime(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var relative = new StreamTime(TimeSpan.FromSeconds(10));
        var offset = TimeSpan.FromSeconds(5);

        // act
        var absPlus = absolute + offset;
        var relMinus = relative - offset;

        // assert
        absPlus.IsAbsolute.Should().BeTrue();
        absPlus.AsAbsolute().Should().Be(absolute.AsAbsolute().AddSeconds(5));

        relMinus.IsRelative.Should().BeTrue();
        relMinus.AsRelative().Should().Be(TimeSpan.FromSeconds(5));
    }
    
    [Fact]
    public void ComparisonOperators_SameType_EvaluateCorrectly()
    {
        // arrange
        var rel1 = new StreamTime("1000"); // 1s
        var rel2 = new StreamTime("2000"); // 2s
        var rel3 = new StreamTime("1000"); // 1s

        // assert
        (rel1 < rel2).Should().BeTrue();
        (rel2 > rel1).Should().BeTrue();
        (rel1 == rel3).Should().BeTrue();
        (rel1 != rel2).Should().BeTrue();
    }
    
    [Fact]
    public void ComparisonOperators_MismatchedTypes_ThrowsInvalidOperationException()
    {
        // arrange
        var absolute = new StreamTime(DateTime.UtcNow);
        var relative = new StreamTime("1500");

        // act
        Func<bool> compareAction = () => absolute > relative;
        Func<bool> equalsAction = () => absolute == relative;

        // assert
        compareAction.Should().Throw<InvalidOperationException>("You cannot > compare an absolute time to a duration.");
        equalsAction().Should().BeFalse("== is safe but inherently false for mismatched types.");
    }
}