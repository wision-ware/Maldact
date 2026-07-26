using FluentAssertions;
using Maldact.Backend.ML.Consolidation.Tuning.Optimizers;
using Maldact.Core.Data;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests.TuningTests.OptimizersTests;

/// <summary>
/// Verifies the round-robin candidate yielding, score routing, and exhaustion states 
/// of the sequential composite optimizer.
/// </summary>
public class SequentialCompositeOptimizerTests
{
    [Fact]
    public void Constructor_NullSequence_ThrowsArgumentNullException()
    {
        Action act = () => new SequentialCompositeOptimizer(null!);
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void Constructor_EmptySequence_ThrowsArgumentException()
    {
        Action act = () => new SequentialCompositeOptimizer(Array.Empty<IConsolidatorOptimizer>());
        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }
    
    [Fact]
    public void SuggestNext_FirstCall_PassesNullScoreToFirstOptimizer()
    {
        var opt1 = new StubOptimizer(new StubConsolidator("A1"));
        var sut = new SequentialCompositeOptimizer(new[] { opt1 });

        var candidate = sut.SuggestNext(null);

        candidate.Should().NotBeNull();
        ((StubConsolidator)candidate!).Name.Should().Be("A1");
        
        opt1.ReceivedScores.Should().ContainSingle()
            .Which.Should().BeNull("the first call has no preceding score to route.");
    }

    [Fact]
    public void SuggestNext_RoundRobin_AlternatesOptimizersAndRoutesScores()
    {
        var opt1 = new StubOptimizer(new StubConsolidator("A1"), new StubConsolidator("A2"));
        var opt2 = new StubOptimizer(new StubConsolidator("B1"), new StubConsolidator("B2"));
        
        var sut = new SequentialCompositeOptimizer(new[] { opt1, opt2 });

        var c1 = sut.SuggestNext(null);
        ((StubConsolidator)c1!).Name.Should().Be("A1");
        opt1.ReceivedScores.Should().BeEquivalentTo(new float?[] { null });

        var c2 = sut.SuggestNext(0.5f);
        ((StubConsolidator)c2!).Name.Should().Be("B1");
        opt2.ReceivedScores.Should().BeEquivalentTo(new float?[] { null });

        var c3 = sut.SuggestNext(0.8f);
        ((StubConsolidator)c3!).Name.Should().Be("A2");
        opt1.ReceivedScores.Should().BeEquivalentTo(new float?[] { null, 0.5f });

        var c4 = sut.SuggestNext(0.9f);
        ((StubConsolidator)c4!).Name.Should().Be("B2");
        opt2.ReceivedScores.Should().BeEquivalentTo(new float?[] { null, 0.8f });
    }
    
    [Fact]
    public void SuggestNext_OneOptimizerExhausted_SkipsAndReturnsNext()
    {
        var opt1 = new StubOptimizer(new StubConsolidator("A1"), null); // exhausts after 1
        var opt2 = new StubOptimizer(new StubConsolidator("B1"), new StubConsolidator("B2"));
        
        var sut = new SequentialCompositeOptimizer(new[] { opt1, opt2 });

        sut.SuggestNext(null); // yields A1
        sut.SuggestNext(0.5f); // yields B1
        
        var c3 = sut.SuggestNext(0.6f);

        c3.Should().NotBeNull();
        ((StubConsolidator)c3!).Name.Should().Be("B2", "opt1 was exhausted, so it should have fallen through to opt2's next candidate.");
        
        opt2.ReceivedScores.Last().Should().Be(0.6f);
    }
    
    [Fact]
    public void SuggestNext_AllExhausted_ReturnsNullAndStaysExhausted()
    {
        var opt1 = new StubOptimizer([null]);
        var opt2 = new StubOptimizer([null]);
        
        var sut = new SequentialCompositeOptimizer(new[] { opt1, opt2 });

        var c1 = sut.SuggestNext(null);
        var c2 = sut.SuggestNext(0.5f); 

        c1.Should().BeNull();
        c2.Should().BeNull();
    }

    private class StubOptimizer : IConsolidatorOptimizer
    {
        public List<float?> ReceivedScores { get; } = new();
        private readonly Queue<ITunableResultConsolidator?> _candidates;

        public StubOptimizer(params ITunableResultConsolidator?[] candidates)
        {
            _candidates = new Queue<ITunableResultConsolidator?>(candidates);
        }

        public ITunableResultConsolidator? SuggestNext(float? score)
        {
            ReceivedScores.Add(score);
            return _candidates.Count > 0 ? _candidates.Dequeue() : null;
        }
    }

    private class StubConsolidator : ITunableResultConsolidator
    {
        public string Name { get; }

        public StubConsolidator(string name) => Name = name;

        public void Consolidate(PipelineChunk chunk)
        {
            chunk.TransitionToConsolidated(Array.Empty<ResultEntry>());
        }
        
        public ConsolidatorConfiguration GetConsolidatorConfiguration() => throw new NotImplementedException();
    }
}