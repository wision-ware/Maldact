using FluentAssertions;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.ServerTests.StreamingTests;

public class StreamingSlotTests
{
    /// <summary>
    /// Proves that missing repository dependencies violently fail slot instantiation.
    /// </summary>
    [Fact]
    public void StreamingSlot_NullRepository_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new StreamingSlot(null!))
            .Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies the atomic exclusivity of the slot state machine.
    /// </summary>
    [Fact]
    public void StreamingSlot_TryActivate_LocksAtomicallyAndRejectsDuplicates()
    {
        // arrange
        var slot = new StreamingSlot(new StubResultRepository());

        // act
        bool firstActivation = slot.TryActivate("192.168.1.100");
        bool secondActivation = slot.TryActivate("10.0.0.5"); // simulate concurrent hijack attempt

        // assert
        firstActivation.Should().BeTrue();
        secondActivation.Should().BeFalse("the slot state was already atomically elevated to Active.");
        
        slot.State.Should().Be(SlotState.Active);
        slot.RemoteEndpoint.Should().Be("192.168.1.100");
        slot.ConnectedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Ensures that deactivating an active slot correctly records the termination timestamp and locks out future use.
    /// </summary>
    [Fact]
    public void StreamingSlot_Deactivate_TransitionsToInactiveWithTimestamp()
    {
        // arrange
        var slot = new StreamingSlot(new StubResultRepository());
        slot.TryActivate("127.0.0.1");

        // act
        slot.Deactivate();

        // assert
        slot.State.Should().Be(SlotState.Inactive);
        slot.DisconnectedAt.Should().NotBeNull();
        slot.DisconnectedAt.Should().BeOnOrAfter(slot.ConnectedAt!.Value);

        // a defunct slot should never be reactivated
        slot.TryActivate("127.0.0.1").Should().BeFalse();
    }
    
    /// <summary>
    /// Localized trace stub to verify persistence routing.
    /// </summary>
    private class StubResultRepository : IResultRepository
    {
        public List<ResultEntry[]> SavedBatches { get; } = new();

        public Task SaveAsync(ResultEntry[] results)
        {
            SavedBatches.Add(results);
            return Task.CompletedTask;
        }

        // bypass all other interface methods (not invoked by the ingestion loop)
        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
        public Task<int> GetCountAsync() => throw new NotImplementedException();
    }
}