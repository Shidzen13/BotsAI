using RoundsBots.Logic;
using Xunit;

public sealed class LoopDetectorTests
{
    [Fact]
    public void DetectsCirclingInPlaceForTenSeconds()
    {
        var detector = new LoopDetector();
        for (int i = 0; i <= 20; i++)
        {
            detector.Add(i * 0.5f, i % 2 == 0 ? -1f : 1f, 0f);
        }

        Assert.True(detector.IsLooping(10f));
    }

    [Fact]
    public void MovingAcrossTheMapIsNotALoop()
    {
        var detector = new LoopDetector();
        for (int i = 0; i <= 20; i++)
        {
            detector.Add(i * 0.5f, i, 0f);
        }

        Assert.False(detector.IsLooping(10f));
    }

    [Fact]
    public void ShortWindowIsNotALoop()
    {
        var detector = new LoopDetector();
        detector.Add(0f, 0f, 0f);
        detector.Add(1f, 0f, 0f);
        detector.Add(2f, 0f, 0f);
        Assert.False(detector.IsLooping(2f));
    }
}
