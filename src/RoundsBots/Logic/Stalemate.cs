using System;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Розрив пата: боти «бачать» одне одного, але пострілу немає (виступи під стелею модової карти),
    /// стоять у зоні спокою — і очко не закінчується ніколи. Два запобіжники:
    /// 1) немає чистого пострілу довше <see cref="NoShotSeconds"/> — ціль вважаємо «невидимою» і йдемо маршрутом до неї;
    /// 2) ніхто не отримує шкоди довше <see cref="QuietSeconds"/> — бажана дистанція всіх ботів стискається до ближньої.
    /// </summary>
    public static class Stalemate
    {
        public const float NoShotSeconds = 3f;
        public const float QuietSeconds = 5f;   // Шидзен: підходити вже після 5 с без влучань
        public const float ShrinkSeconds = 5f;  // до впритул — на 10-й секунді
        public const float CloseDistance = 5f;

        /// <summary>Скільки секунд безперервного пострілу потрібно, щоб вийти з режиму наближення (без смикання).</summary>
        public const float ExitShotSeconds = 0.5f;

        /// <summary>Скільки секунд без шляху до найближчого ворога — і бот бере іншу, досяжну ціль.</summary>
        public const float NoRouteSeconds = 3f;

        /// <summary>Скільки секунд без шкоди, доки запобіжник примусово не завершить очко (лише самі наші боти).</summary>
        public const float ForceEndSeconds = 60f;

        /// <summary>
        /// Примусово завершити очко: живих людей і чужих ботів немає, наших ботів щонайменше двоє, тиша ≥ 60 с.
        /// </summary>
        public static bool ShouldForceEnd(int aliveOthers, int aliveOwnBots, float secondsSinceAnyDamage)
        {
            return ShouldForceEnd(aliveOthers, aliveOwnBots, aliveTeams: 2, secondsSinceAnyDamage, ForceEndSeconds);
        }

        /// <summary>Повна умова: ще й живі щонайменше 2 команди (інакше смерть очко не закриє) і поріг із конфігу.</summary>
        public static bool ShouldForceEnd(int aliveOthers, int aliveOwnBots, int aliveTeams, float secondsSinceAnyDamage, float thresholdSeconds)
        {
            return aliveOthers == 0 && aliveOwnBots >= 2 && aliveTeams >= 2 && secondsSinceAnyDamage >= thresholdSeconds;
        }

        /// <summary>Чи вважати видиму ціль недосяжною для пострілу (і йти до неї маршрутом).</summary>
        public static bool ShouldApproach(bool canSee, float secondsWithoutClearShot)
        {
            return canSee && secondsWithoutClearShot >= NoShotSeconds;
        }

        /// <summary>Бажана дистанція з урахуванням тиші: після <see cref="QuietSeconds"/> лінійно до <see cref="CloseDistance"/>.</summary>
        public static float PreferredDistance(float baseDistance, float secondsSinceAnyDamage)
        {
            if (secondsSinceAnyDamage <= QuietSeconds)
            {
                return baseDistance;
            }

            float t = Math.Min(1f, (secondsSinceAnyDamage - QuietSeconds) / ShrinkSeconds);
            float target = Math.Min(baseDistance, CloseDistance);
            return baseDistance + (target - baseDistance) * t;
        }
    }
}
