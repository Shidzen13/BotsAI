using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests;

public sealed class BankShotTests
{
    // Стіна x=5 (y від -3 до 3) між стрільцем (0,0) і ціллю (10,0); стеля y=5.
    private static bool Cast(float ax, float ay, float bx, float by, out float hx, out float hy, out float nx, out float ny)
    {
        hx = hy = nx = ny = 0f;
        if ((ax - 5f) * (bx - 5f) <= 0f && ax != bx)
        {
            float t = (5f - ax) / (bx - ax);
            float y = ay + t * (by - ay);
            if (y >= -3f && y <= 3f)
            {
                hx = 5f;
                hy = y;
                nx = bx > ax ? -1f : 1f;
                return true;
            }
        }

        if (ay < 5f && by >= 5f)
        {
            float t = (5f - ay) / (by - ay);
            hx = ax + t * (bx - ax);
            hy = 5f;
            ny = -1f;
            return true;
        }

        return false;
    }

    [Fact]
    public void FindsShot_OffCeiling_WhenWallBlocksDirectShot()
    {
        bool found = BankShot.TryFind(0f, 0f, 40f, 0f, 0f, 0f, 1, 10f, 0f, 0.8f, 2f, 72, Cast, out float dx, out float dy);

        Assert.True(found);
        Assert.True(dy > 0f && dx > 0f); // угору-вправо, у стелю
    }

    [Fact]
    public void NoShot_WithoutBounces()
    {
        Assert.False(BankShot.TryFind(0f, 0f, 40f, 0f, 0f, 0f, 0, 10f, 0f, 0.8f, 2f, 72, Cast, out _, out _));
    }

    [Fact]
    public void DirectHit_IsNotCountedAsBankShot()
    {
        // Ціль перед стіною — пряме влучання, рикошет не потрібен.
        Assert.False(BankShot.Simulate(0f, 0f, 1f, 0f, 40f, 0f, 0f, 0f, 1, 3f, 0f, 0.5f, 1f, Cast, out _));
    }

    [Fact]
    public void ArenaEdge_ReflectsBullet()
    {
        // Порожня арена висотою ±5: ціль за «стіною» з x=5, лише межа зверху дає рикошет.
        SegmentCast wallOnly = (float ax, float ay, float bx, float by, out float hx, out float hy, out float nx, out float ny) =>
        {
            bool hit = Cast(ax, ay, bx, by, out hx, out hy, out nx, out ny);
            return hit && nx != 0f; // без стелі: лишаємо тільки вертикальну стіну
        };
        SegmentCast withEdges = BankShot.WithArenaBounds(wallOnly, 100f, 5f);

        bool wrong = BankShot.TryFind(0f, 0f, 40f, 0f, 0f, 0f, 1, 10f, 0f, 0.8f, 2f, 72, wallOnly, out float wx, out float wy);
        Assert.False(wrong, $"dir=({wx},{wy})");
        Assert.True(BankShot.TryFind(0f, 0f, 40f, 0f, 0f, 0f, 1, 10f, 0f, 0.8f, 2f, 72, withEdges, out _, out float dy));
        Assert.True(dy > 0f);
    }
}
