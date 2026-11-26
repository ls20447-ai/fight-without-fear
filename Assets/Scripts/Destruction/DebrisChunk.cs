using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FWF.Destruction
{
    /// <summary>Short-lived physics chunk. Returns itself to its pool after a lifetime.</summary>
    public class DebrisChunk : MonoBehaviour
    {
        [SerializeField] Rigidbody rb;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float shrinkTime = 0.5f;

        float _spawnTime;
        Vector3 _baseScale;
        System.Action<DebrisChunk> _release;

        public void Init(System.Action<DebrisChunk> release) { _release = release; _baseScale = transform.localScale; }

        public void Launch(Vector3 pos, Vector3 velocity)
        {
            transform.SetPositionAndRotation(pos, Random.rotation);
            transform.localScale = _baseScale;
            rb.linearVelocity = velocity;           // Unity 6; use rb.velocity on 2022 LTS
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            _spawnTime = Time.time;
        }

        // Only runs while the chunk is active; inactive objects cost nothing
        void Update()
        {
            float age = Time.time - _spawnTime;
            if (age > lifetime - shrinkTime)
                transform.localScale = _baseScale * Mathf.Clamp01((lifetime - age) / shrinkTime);
            if (age >= lifetime) _release(this);
        }
    }
}
