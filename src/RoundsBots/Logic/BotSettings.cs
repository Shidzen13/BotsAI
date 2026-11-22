namespace RoundsBots.Logic
{
    /// <summary>
    /// A/B-експеримент бот-проти-бота: бот з непарним id (варіант B) вимикає вибрану функцію,
    /// парний (A) грає повною версією. Результати — рядки RESULT у лозі.
    /// </summary>
    public enum BotExperiment
    {
        None,
        NoHide,
        NoBreakObstacles,
        NoLoadoutDistance,
        NoCloseBlock,
    }

    public enum BotDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>Незмінний набір параметрів поведінки бота. Без залежностей від Unity — тестується окремо.</summary>
    public sealed class BotSettings
    {
        public BotSettings(
            float thinkInterval,
            float aimErrorDegrees,
            float blockChance,
            float shootChance,
            float preferredDistance,
            float cardRandomness)
        {
            ThinkInterval = thinkInterval;
            AimErrorDegrees = aimErrorDegrees;
            BlockChance = blockChance;
            ShootChance = shootChance;
            PreferredDistance = preferredDistance;
            CardRandomness = cardRandomness;
        }

        /// <summary>Як часто (сек) бот приймає нові рішення. Менше = швидша реакція.</summary>
        public float ThinkInterval { get; }

        /// <summary>Максимальне відхилення прицілу в градусах.</summary>
        public float AimErrorDegrees { get; }

        /// <summary>Шанс (0..1) заблокувати кулю, яка летить у бота.</summary>
        public float BlockChance { get; }

        /// <summary>Шанс (0..1) стріляти, коли ціль видно.</summary>
        public float ShootChance { get; }

        /// <summary>Відстань, яку бот намагається тримати до цілі.</summary>
        public float PreferredDistance { get; }

        /// <summary>Скільки випадковості додається до оцінки карток (0 = завжди «найкраща»).</summary>
        public float CardRandomness { get; }

        /// <summary>Копія з іншою бажаною дистанцією (залежить від збірки бота).</summary>
        public BotSettings WithPreferredDistance(float distance)
        {
            return new BotSettings(ThinkInterval, AimErrorDegrees, BlockChance, ShootChance, distance, CardRandomness);
        }

        public static BotSettings For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                // Бажана дистанція — з запису гравця: людина стріляє з ~16 і тримає ~14–22.
                case BotDifficulty.Easy:
                    return new BotSettings(0.25f, 12f, 0.25f, 0.6f, 16f, 2.0f);
                case BotDifficulty.Hard:
                    return new BotSettings(0.07f, 2f, 0.85f, 1.0f, 13f, 0.3f);
                default:
                    return new BotSettings(0.15f, 6f, 0.5f, 0.85f, 15f, 1.0f);
            }
        }
    }
}
