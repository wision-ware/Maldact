using FluentAssertions;
using Maldact.Core.Results;
using Maldact.Core.Streaming;

namespace Maldact.Tests.CoreTests.StreamingTests;

public class StreamHeaderTests
{
    /// <summary>
    /// Proves that absolute wall-clock temporal boundaries serialize and deserialize correctly across the wire.
    /// </summary>
    [Fact]
    public void StreamHeader_AbsoluteTime_RoundtripsPerfectly()
    {
        // arrange
        var token = Guid.NewGuid();
        var t0 = new StreamTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var originalHeader = new StreamHeader(token, t0);

        // act
        byte[] payload = originalHeader.ToBytes();
        var deserializedHeader = new StreamHeader(payload);

        // assert
        payload.Length.Should().Be(32);
        deserializedHeader.Token.Should().Be(token);
        deserializedHeader.T0.IsAbsolute.Should().BeTrue();
        deserializedHeader.T0.AsAbsolute().Should().Be(t0.AsAbsolute());
    }

    /// <summary>
    /// Proves that relative stream duration boundaries serialize and deserialize correctly across the wire.
    /// </summary>
    [Fact]
    public void StreamHeader_RelativeTime_RoundtripsPerfectly()
    {
        // arrange
        var token = Guid.NewGuid();
        var t0 = new StreamTime(TimeSpan.FromMilliseconds(5000));
        var originalHeader = new StreamHeader(token, t0);

        // act
        byte[] payload = originalHeader.ToBytes();
        var deserializedHeader = new StreamHeader(payload);

        // assert
        payload.Length.Should().Be(32);
        deserializedHeader.Token.Should().Be(token);
        deserializedHeader.T0.IsRelative.Should().BeTrue();
        deserializedHeader.T0.AsRelative().Should().Be(t0.AsRelative());
    }

    /// <summary>
    /// Verifies that partial or fragmented network packets instantly fault rather than parsing corrupted memory.
    /// </summary>
    [Fact]
    public void StreamHeader_FragmentedPayload_ThrowsArgumentException()
    {
        // arrange
        var fragmentedPayload = new byte[15];

        // act & assert
        FluentActions.Invoking(() => new StreamHeader(fragmentedPayload))
            .Should().Throw<ArgumentException>()
            .WithMessage("*at least 32 bytes*");
    }
}