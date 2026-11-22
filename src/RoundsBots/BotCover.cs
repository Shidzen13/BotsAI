using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Пошук укриття під час перезарядки: найближча досяжна клітинка, з якої ворог нас не бачить
    /// і яка не ближче до ворога, ніж ми зараз. Результат кешується на <see cref="CacheSeconds"/>.
    /// </summary>
    internal sealed class BotCover
    {
        private const float SearchBudget = 18f;   // ~10–15 клітинок ходу
        private const float CacheSeconds = 0.6f;
        private const int MaxCandidates = 40;

        /// <summary>Точки тіла гравця відносно центру: укриття — лише якщо ворог не бачить жодної.</summary>
        private static readonly Vector2[] BodyPoints =
        {
            Vector2.zero, new Vector2(0f, 0.6f), new Vector2(0f, -0.5f), new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f),
        };

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[16];

        private Vector2? cover;
        private float foundAt = float.NegativeInfinity;

        public Vector2? Current => cover;

        public void Clear()
        {
            cover = null;
            foundAt = float.NegativeInfinity;
        }

        public Vector2? Find(Vector2 myPosition, Player enemy)
        {
            // Гістерезис: тримаємо поточне укриття, поки воно сховане. Порожній результат теж кешуємо,
            // щоб на відкритій арені не шукати заново кожне рішення.
            if (cover.HasValue ? !IsBodyVisible(enemy, cover.Value) : Time.time - foundAt < CacheSeconds)
            {
                return cover;
            }

            foundAt = Time.time;
            cover = Search(myPosition, enemy);
            return cover;
        }

        private static Vector2? Search(Vector2 myPosition, Player enemy)
        {
            NavGrid grid = NavMap.Get();
            (int cx, int cy) = NavMap.ToCell(myPosition);
            if (!grid.TryFindStandable(cx, cy, out int sx, out int sy))
            {
                return null;
            }

            Vector2 enemyPosition = enemy.data.playerVel.position;
            float myDistance = Vector2.Distance(myPosition, enemyPosition);
            int checkedCount = 0;

            foreach ((int x, int y, float _) in grid.ReachableWithin(sx, sy, SearchBudget))
            {
                if (++checkedCount > MaxCandidates)
                {
                    break;
                }

                // Центр гравця — трохи вище клітинки під ногами.
                Vector2 candidate = NavMap.ToWorld(x, y) + Vector2.up * 0.5f;
                if (Vector2.Distance(candidate, enemyPosition) < myDistance * 0.8f || HazardCache.IsDangerous(candidate))
                {
                    continue;
                }

                if (!IsBodyVisible(enemy, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsBodyVisible(Player enemy, Vector2 center)
        {
            foreach (Vector2 offset in BodyPoints)
            {
                if (IsVisibleFrom(enemy, center + offset))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Чи бачить ворог точку (та сама логіка, що PlayerManager.CanSeePlayer; руйнівні об'єкти — не укриття).</summary>
        private static bool IsVisibleFrom(Player enemy, Vector2 point)
        {
            Vector2 from = enemy.data.playerVel.position;
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            int count = Physics2D.LinecastNonAlloc(from, point, Hits, mask);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = Hits[i];
                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue; // кулі проходять крізь тригери — це не укриття
                }

                Transform root = hit.transform != null ? hit.transform.root : null;
                if (root != null && root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null
                    && !Destructibles.IsDestructible(hit.collider))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
