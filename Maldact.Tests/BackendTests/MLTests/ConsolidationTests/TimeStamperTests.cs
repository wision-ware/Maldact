using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests;

/// <summary>
/// Verifies the chronological sample offset calculations and origin bindings of the TimeStamper.
/// </summary>
public class TimeStamperTests
{
    [Fact]
    public void GetTime_RelativeOrigin_CalculatesCorrectOffsets()
    {
        // arrange
        var t0 = new StreamTime("1000"); // 1 second origin offset
        var sut = new TimeStamper(frameRateHz: 10.0, t0: t0);

        // act
        var timeAtFrame0 = sut.GetTime(0);
        var timeAtFrame15 = sut.GetTime(15); // +1.5 seconds

        // assert
        timeAtFrame0.IsRelative.Should().BeTrue();
        timeAtFrame0.TotalMilliseconds.Should().Be(1000.0);

        timeAtFrame15.IsRelative.Should().BeTrue();
        timeAtFrame15.TotalMilliseconds.Should().Be(2500.0); // 1000ms origin + 1500ms offset
    }
    
    [Fact]
    public void GetTime_AbsoluteOrigin_CalculatesCorrectOffsets()
    {
        // arrange
        var t0 = new StreamTime("2024-01-01T12:00:00Z");
        var sut = new TimeStamper(frameRateHz: 100.0, t0: t0);

        // act
        var timeAtFrame50 = sut.GetTime(50); // +0.5 seconds

        // assert
        timeAtFrame50.IsAbsolute.Should().BeTrue();
        timeAtFrame50.AsAbsolute().Should().Be(DateTime.Parse("2024-01-01T12:00:00.500Z").ToUniversalTime());
    }
    
    [Fact]
    public void GetTime_HighFrequencyRate_MaintainsPrecision()
    {
        // arrange
        var t0 = new StreamTime("0");
        var sut = new TimeStamper(frameRateHz: 44100.0, t0: t0); // standard cd audio rate

        // act
        var timeAt1Second = sut.GetTime(44100);

        // assert
        timeAt1Second.TotalMilliseconds.Should().Be(1000.0);
    }
}