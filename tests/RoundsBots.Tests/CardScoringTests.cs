using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests;

public sealed class CardScoringTests
{
    [Fact]
    public void Score_AddsSpecialValue()
    {
        var plain = new CardFeatures();
        var special = new CardFeatures { SpecialValue = 2.5f };

        Assert.Equal(CardScoring.Score(plain) + 2.5f, CardScoring.Score(special), 3);
    }

    [Fact]
    public void Score_IsZero_ForCardThatChangesNothing()
    {
        Assert.Equal(0f, CardScoring.Score(new CardFeatures()), 3);
    }

    [Fact]
    public void Score_IsHigher_ForMoreDamage()
    {
        var weak = new CardFeatures { DamageMultiplier = 1.1f };
        var strong = new CardFeatures { DamageMultiplier = 2f };

        Assert.True(CardScoring.Score(strong) > CardScoring.Score(weak));
    }

    [Fact]
    public void Score_RewardsFasterAttack_AndPenalisesSlowerAttack()
    {
        var faster = new CardFeatures { AttackSpeedMultiplier = 0.7f };
        var slower = new CardFeatures { AttackSpeedMultiplier = 1.5f };

        Assert.True(CardScoring.Score(faster) > 0f);
        Assert.True(CardScoring.Score(slower) < 0f);
    }

    [Fact]
    public void Score_IgnoresNonPositiveMultipliers_InsteadOfReturningNaN()
    {
        var broken = new CardFeatures { DamageMultiplier = 0f, AttackSpeedMultiplier = -1f };

        Assert.False(float.IsNaN(CardScoring.Score(broken)));
    }

    [Fact]
    public void Score_RarityAddsValue_ForModdedCardsWithoutStats()
    {
        Assert.True(CardScoring.Score(new CardFeatures { Rarity = 2 }) > CardScoring.Score(new CardFeatures { Rarity = 0 }));
    }

    [Fact]
    public void ChooseIndex_ReturnsMinusOne_WhenNoCards()
    {
        Assert.Equal(-1, CardScoring.ChooseIndex(new float[0], null!, 1f));
    }

    [Fact]
    public void ChooseIndex_PicksHighestScore_WithoutRandomness()
    {
        Assert.Equal(1, CardScoring.ChooseIndex(new[] { 0.5f, 3f, 1f }, new[] { 1f, 0f, 1f }, 0f));
    }

    [Fact]
    public void ChooseIndex_NoiseCanOverturnSmallDifferences()
    {
        Assert.Equal(0, CardScoring.ChooseIndex(new[] { 1f, 1.2f }, new[] { 1f, 0f }, 1f));
    }
}
