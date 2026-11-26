using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FWF.Destruction
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point, Vector3 direction);
    }

    /// <summary>
    /// Cover/wall with 3 visual stages: Intact -> Damaged -> Destroyed.
    /// Mobile-friendly design:
    ///  - NO runtime mesh slicing. Stages are pre-authored meshes (cheap, deterministic).
    ///  - Debris are pooled low-poly chunks with a short lifetime, hard-capped globally.
    ///  - Heavy weapons use explosive damage; bullets only chip walls (weapon-class multiplier).
    ///  - State is a tiny struct (id + stage) so it syncs cheaply over the network:
    ///    the SERVER owns health; clients just receive "set stage" events.
    /// </summary>
    public class DestructibleCover : MonoBehaviour, IDamageable
    {
        public enum Stage : byte { Intact, Damaged, Destroyed }

        [Header("Stats")]
        [SerializeField] float maxHealth = 300f;
        [SerializeField, Range(0f, 1f)] float bulletDamageMultiplier = 0.15f; // guns barely hurt walls
        [SerializeField, Range(0f, 1f)] float damagedThreshold = 0.5f;        // below this % -> damaged mesh

        [Header("Stage visuals (children, toggled not instantiated)")]
        [SerializeField] GameObject intactVisual;
        [SerializeField] GameObject damagedVisual;
        [SerializeField] Collider fullCollider;      // blocks movement + bullets
        [SerializeField] Collider damagedCollider;   // smaller / shorter, optional

        [Header("Debris")]
        [SerializeField] string debrisPoolId = "debris_concrete";
        [SerializeField] int debrisCount = 6;
        [SerializeField] float debrisForce = 6f;

        public Stage CurrentStage { get; private set; } = Stage.Intact;

        float _health;

        void Awake() => _health = maxHealth;

        // --- Bullets / melee ---------------------------------------------------
        public void TakeDamage(float amount, Vector3 point, Vector3 direction)
            => ApplyDamage(amount * bulletDamageMultiplier, point, direction);

        // --- Grenades / rockets: call from explosion code ---------------------
        /// <summary>Explosion damage falls off with distance. Full damage at center.</summary>
        public void ApplyExplosion(Vector3 center, float radius, float damage)
        {
            float dist = Vector3.Distance(center, transform.position);
            if (dist > radius) return;
            float falloff = 1f - (dist / radius);
            Vector3 dir = (transform.position - center).normalized;
            ApplyDamage(damage * falloff, center, dir);
        }

        void ApplyDamage(float amount, Vector3 point, Vector3 dir)
        {
            if (CurrentStage == Stage.Destroyed) return;
            _health -= amount;

            if (_health <= 0f)             SetStage(Stage.Destroyed, point, dir);
            else if (CurrentStage == Stage.Intact && _health <= maxHealth * damagedThreshold)
                                           SetStage(Stage.Damaged, point, dir);
        }

        /// <summary>
        /// Public so the network layer can call it on clients when the server
        /// broadcasts a stage change. Keep it idempotent.
        /// </summary>
        public void SetStage(Stage stage, Vector3 hitPoint, Vector3 hitDir)
        {
            if (stage <= CurrentStage) return;
            CurrentStage = stage;

            switch (stage)
            {
                case Stage.Damaged:
                    intactVisual.SetActive(false);
                    damagedVisual.SetActive(true);
                    if (damagedCollider) { fullCollider.enabled = false; damagedCollider.enabled = true; }
                    SpawnDebris(hitPoint, hitDir, Mathf.Max(1, debrisCount / 3));
                    break;

                case Stage.Destroyed:
                    intactVisual.SetActive(false);
                    damagedVisual.SetActive(false);
                    fullCollider.enabled = false;
                    if (damagedCollider) damagedCollider.enabled = false;
                    SpawnDebris(hitPoint, hitDir, debrisCount);
                    // Gameplay hook: bake into nav / notify bots & line-of-sight systems here
                    break;
            }
        }

        void SpawnDebris(Vector3 origin, Vector3 dir, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var d = DebrisPool.Instance.Get(debrisPoolId);
                if (d == null) return; // global cap reached: skip silently, saves frames
                Vector3 jitter = Random.insideUnitSphere * 0.4f;
                d.Launch(origin + jitter, (dir + Random.insideUnitSphere * 0.6f).normalized * debrisForce);
            }
        }
    }
}
