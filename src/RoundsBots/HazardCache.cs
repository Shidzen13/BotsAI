using System.Collections.Generic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Небезпеки на карті (пилки тощо): ванільні <see cref="DamageBox"/>/<see cref="Saw"/> і модові об'єкти,
    /// в назві типу яких є «Saw»/«Damage». Атаки гравців (SpawnedAttack) не враховуються.
    /// Список спільний для всіх ботів і оновлюється раз на <see cref="RefreshInterval"/> с.
    /// </summary>
    internal static class HazardCache
    {
        private const float RefreshInterval = 0.5f;
        private const float SafetyMargin = 1.5f;

        private static readonly List<(Transform transform, float radius)> Hazards = new List<(Transform, float)>();
        private static float refreshedAt = float.NegativeInfinity;

        /// <summary>Чи небезпечна точка (поруч пилка чи інша шкідлива штука).</summary>
        public static bool IsDangerous(Vector2 point, float extraMargin = 0f)
        {
            Refresh();
            foreach ((Transform transform, float radius) in Hazards)
            {
                if (transform == null)
                {
                    continue;
                }

                float limit = radius + SafetyMargin + extraMargin;
                if (((Vector2)transform.position - point).sqrMagnitude <= limit * limit)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Напрямок «геть від найближчої небезпеки» або нуль, якщо поруч нічого немає.</summary>
        public static Vector2 EscapeDirection(Vector2 point, float extraMargin)
        {
            Refresh();
            Vector2 escape = Vector2.zero;
            foreach ((Transform transform, float radius) in Hazards)
            {
                if (transform == null)
                {
                    continue;
                }

                Vector2 away = point - (Vector2)transform.position;
                float limit = radius + SafetyMargin + extraMargin;
                if (away.sqrMagnitude <= limit * limit)
                {
                    escape += away.sqrMagnitude > 0.001f ? away.normalized : Vector2.up;
                }
            }

            return escape;
        }

        private static void Refresh()
        {
            float now = Time.time;
            if (now - refreshedAt < RefreshInterval && now >= refreshedAt)
            {
                return;
            }

            refreshedAt = now;
            Hazards.Clear();
            var seen = new HashSet<Transform>();

            foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || !IsHazardType(behaviour))
                {
                    continue;
                }

                Transform transform = behaviour.transform;
                if (!seen.Add(transform) || transform.root.GetComponent<Player>() != null || behaviour.GetComponentInParent<SpawnedAttack>() != null)
                {
                    continue;
                }

                Hazards.Add((transform, EstimateRadius(behaviour.gameObject)));
            }
        }

        private static bool IsHazardType(MonoBehaviour behaviour)
        {
            if (behaviour is DamageBox || behaviour is Saw)
            {
                return true;
            }

            string name = behaviour.GetType().Name;
            return name.Contains("Saw") || name.Contains("DamageBox") || name.Contains("Hazard");
        }

        private static float EstimateRadius(GameObject gameObject)
        {
            Collider2D collider = gameObject.GetComponentInChildren<Collider2D>();
            if (collider == null)
            {
                return 1.5f;
            }

            Vector3 extents = collider.bounds.extents;
            return Mathf.Max(extents.x, extents.y);
        }
    }
}
