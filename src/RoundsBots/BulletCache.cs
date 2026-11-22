using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Спільний для всіх ботів список куль на сцені. FindObjectsOfType дорогий,
    /// тому оновлюємо його не частіше ніж раз на <see cref="RefreshInterval"/> секунд.
    /// </summary>
    internal static class BulletCache
    {
        private const float RefreshInterval = 0.1f;

        private static ProjectileHit[] bullets = new ProjectileHit[0];
        private static float refreshedAt = float.NegativeInfinity;

        public static ProjectileHit[] Get()
        {
            float now = Time.time;
            if (now - refreshedAt >= RefreshInterval || now < refreshedAt)
            {
                bullets = Object.FindObjectsOfType<ProjectileHit>();
                refreshedAt = now;
            }

            return bullets;
        }
    }
}
