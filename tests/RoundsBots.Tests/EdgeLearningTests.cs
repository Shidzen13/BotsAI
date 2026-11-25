using RoundsBots.Logic;
using Xunit;

public sealed class EdgeLearningTests
{
    [Fact]
    public void Failure_PenalisesEdgeAndSameHeightNeighbours()
    {
        var learning = new EdgeLearning();

        learning.RecordFailure(10, 5, 20, 5);

        Assert.Equal(8f, learning.PenaltyOf(10, 5, 20, 5));
        Assert.Equal(4f, learning.PenaltyOf(9, 5, 19, 5));
        Assert.Equal(4f, learning.PenaltyOf(11, 5, 21, 5));
        Assert.Equal(0f, learning.PenaltyOf(10, 6, 20, 6)); // інша висота — не чіпаємо
        Assert.Equal(3, learning.Count);                    // було 81, стало 3
    }

    [Fact]
    public void Penalty_IsCappedAt24()
    {
        var learning = new EdgeLearning();

        for (int i = 0; i < 10; i++)
        {
            learning.RecordFailure(1, 1, 5, 1);
        }

        Assert.Equal(EdgeLearning.MaxPenalty, learning.PenaltyOf(1, 1, 5, 1));
    }

    [Fact]
    public void Decay_HalvesAndForgetsSmallPenalties()
    {
        var learning = new EdgeLearning();
        learning.RecordFailure(1, 1, 5, 1);

        learning.Decay();
        Assert.Equal(4f, learning.PenaltyOf(1, 1, 5, 1));
        Assert.Equal(2f, learning.PenaltyOf(0, 1, 4, 1));

        for (int i = 0; i < 4; i++)
        {
            learning.Decay();
        }

        Assert.Equal(0, learning.Count); // 8 → 0.25 — забуто
    }

    [Fact]
    public void Success_ClearsEdgePenalty()
    {
        var learning = new EdgeLearning();
        learning.RecordFailure(1, 1, 5, 1);

        Assert.True(learning.RecordSuccess(1, 1, 5, 1));

        Assert.Equal(0f, learning.PenaltyOf(1, 1, 5, 1));
        Assert.Equal(4f, learning.PenaltyOf(2, 1, 6, 1));
    }

    [Theory]
    [InlineData(NavMove.Jump, true, true)]
    [InlineData(NavMove.Fall, true, true)]
    [InlineData(NavMove.Jump, false, false)] // таймаут — лише лог
    [InlineData(NavMove.Walk, true, false)]  // ходьба — лише лог
    [InlineData(NavMove.Bounce, true, false)]
    public void OnlyRealFallsArePenalised(NavMove move, bool fell, bool expected)
    {
        Assert.Equal(expected, BotMath.ShouldPenalise(move, fell));
    }

    [Fact]
    public void TwoTimeouts_EqualOneFall_OnlyOnThatEdge()
    {
        var learning = new EdgeLearning();

        learning.RecordTimeout(3, 3, 4, 8);
        learning.RecordTimeout(3, 3, 4, 8);

        Assert.Equal(EdgeLearning.FailPenalty, learning.PenaltyOf(3, 3, 4, 8));
        Assert.Equal(4f, learning.PenaltyOf(3, 3, 2, 8)); // сусідні цілі в ряду теж (2 × 2)
        Assert.Equal(4f, learning.PenaltyOf(3, 3, 6, 8));
        Assert.Equal(0f, learning.PenaltyOf(3, 3, 7, 8));
        Assert.Equal(5, learning.Count);
    }

    [Theory]
    [InlineData(NavMove.Jump, false, true)]
    [InlineData(NavMove.Jump, true, false)] // падіння штрафується окремо
    [InlineData(NavMove.Walk, false, false)]
    public void TimeoutPenaltyOnlyForJumpsAndFalls(NavMove move, bool fell, bool expected)
    {
        Assert.Equal(expected, BotMath.ShouldPenaliseTimeout(move, fell));
    }
}
