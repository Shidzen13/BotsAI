using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Руйнівні об'єкти карти: дерев'яні перегородки, коробки (Destructible / DamagableEvent та інші нащадки Damagable).
    /// Гравці теж Damagable (HealthHandler), тому їх виключаємо. Перегородка ламається від удару ≥25, звичайна куля — 55.
    /// </summary>
    /// <summary>Що стоїть попереду: нічого/стіна, руйнівна перегородка, рухомий ящик чи куля.</summary>
    internal enum ObstacleKind
    {
        None,
        Breakable,
        Movable,
    }

    internal static class Destructibles
    {
        public static bool IsDestructible(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            Damagable damagable = collider.GetComponentInParent<Damagable>();
            return damagable != null
                && collider.transform.root.GetComponent<Player>() == null
                && damagable.GetComponentInParent<Player>() == null;
        }

        /// <summary>
        /// Вільний фізичний об'єкт (ящик, куля): не ламається, але його штовхають кулі й гравці.
        /// Платформи на мотузках/шарнірах (Joint2D) висять на місці — це не «рухомі».
        /// </summary>
        public static bool IsMovable(Collider2D collider)
        {
            Rigidbody2D body = collider != null ? collider.attachedRigidbody : null;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || body.GetComponent<Joint2D>() != null)
            {
                return false;
            }

            Transform root = collider.transform.root;
            return root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null && root.GetComponent<ProjectileHit>() == null;
        }

        /// <summary>Чи стоїть руйнівна перешкода просто попереду; повертає точку, куди стріляти.</summary>
        public static bool TryFindAhead(Vector2 from, Vector2 direction, float distance, out Vector2 point)
        {
            return FindAhead(from, direction, distance, out point) == ObstacleKind.Breakable;
        }

        /// <summary>Найближча перешкода попереду та її вид.</summary>
        public static ObstacleKind FindAhead(Vector2 from, Vector2 direction, float distance, out Vector2 point)
        {
            point = Vector2.zero;
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            RaycastHit2D[] hits = Physics2D.RaycastAll(from, direction.normalized, distance, mask);
            float best = float.PositiveInfinity;
            ObstacleKind found = ObstacleKind.None;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.transform.root.GetComponent<Player>() != null)
                {
                    continue;
                }

                if (hit.distance >= best)
                {
                    continue;
                }

                // Найближча перешкода: стіна — нічого не робимо.
                best = hit.distance;
                found = IsDestructible(hit.collider) ? ObstacleKind.Breakable
                    : IsMovable(hit.collider) ? ObstacleKind.Movable
                    : ObstacleKind.None;
                point = hit.point;
            }

            return found;
        }
    }
}
