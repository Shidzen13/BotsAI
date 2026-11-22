using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests;

public sealed class CardKnowledgeTests
{
    [Fact]
    public void RiskyCard_GetsPenalty()
    {
        Assert.True(CardKnowledge.Adjustment("Abyssal Countdown", 0) < 0f);
    }

    [Fact]
    public void CloseRangeCard_IsBetter_WhenBuildIsAlreadyClose()
    {
        Assert.True(CardKnowledge.Adjustment("ABYSSAL COUNTDOWN", 2) > CardKnowledge.Adjustment("ABYSSAL COUNTDOWN", 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown Card From Future Mod")]
    public void UnknownCard_IsNeutral(string? name)
    {
        Assert.Equal(0f, CardKnowledge.Adjustment(name!, 0));
    }
}

public sealed class BuildBalanceTests
{
    [Fact]
    public void RoleOf_DetectsOffenseAndDefense()
    {
        Assert.Equal(CardRole.Offense, CardScoring.RoleOf(new CardFeatures { DamageMultiplier = 1.5f }));
        Assert.Equal(CardRole.Defense, CardScoring.RoleOf(new CardFeatures { HealthMultiplier = 1.5f }));
        Assert.Equal(CardRole.Utility, CardScoring.RoleOf(new CardFeatures { ReloadTimeMultiplier = 0.5f }));
        Assert.Equal(CardRole.None, CardScoring.RoleOf(new CardFeatures()));
    }

    [Fact]
    public void Balance_PrefersMissingRole()
    {
        var owned = new[] { CardRole.Defense, CardRole.Defense, CardRole.Defense };

        Assert.True(CardScoring.BalanceAdjustment(CardRole.Offense, owned) > 0f);
        Assert.True(CardScoring.BalanceAdjustment(CardRole.Defense, owned) < 0f);
    }

    [Fact]
    public void Balance_IsNeutral_WithoutCards()
    {
        Assert.Equal(0f, CardScoring.BalanceAdjustment(CardRole.Offense, new CardRole[0]));
    }
}

public sealed class LearnedFromHumanTests
{
    [Fact]
    public void Pipsqueak_IsValued_FromDatabaseEffects()
    {
        // Людина взяла Pipsqueak (менший розмір, швидший рух і стрільба) — бот раніше бачив лише 0.54.
        Assert.True(CardKnowledge.TryGet("Pipsqueak", out CardTraits traits));
        Assert.NotNull(traits.Features);
        Assert.True(CardScoring.Score(traits.Features!) > 1f);
    }

    [Fact]
    public void SecondCopy_ScoresLower()
    {
        Assert.True(CardKnowledge.DuplicateAdjustment("HUGE", 1) < CardKnowledge.DuplicateAdjustment("HUGE", 0));
    }
}
