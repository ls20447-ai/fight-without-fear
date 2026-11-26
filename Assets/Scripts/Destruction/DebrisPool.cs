using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FWF.Destruction
{
    /// <summary>
    /// Pools debris by id. Hard global cap keeps physics cost predictable on low-end phones.
    /// </summary>
    public class DebrisPool : MonoBehaviour
    {
        public static DebrisPool Instance { get; private set; }

        [System.Serializable]
        public struct Entry { public string id; public DebrisChunk prefab; public int prewarm; }

        [SerializeField] Entry[] entries;
        [SerializeField] int globalMaxActive = 60;   // tune per device tier

        readonly Dictionary<string, ObjectPool<DebrisChunk>> _pools = new();
        int _active;

        void Awake()
        {
            Instance = this;
            foreach (var e in entries)
            {
                var entry = e; // capture
                ObjectPool<DebrisChunk> pool = null;
                pool = new ObjectPool<DebrisChunk>(
                    createFunc: () =>
                    {
                        var c = Instantiate(entry.prefab, transform);
                        c.Init(chunk => pool.Release(chunk));
                        return c;
                    },
                    actionOnGet: c => { c.gameObject.SetActive(true); _active++; },
                    actionOnRelease: c => { c.gameObject.SetActive(false); _active--; },
                    defaultCapacity: entry.prewarm, maxSize: globalMaxActive);

                // Prewarm to avoid first-explosion hitch
                var temp = new List<DebrisChunk>(entry.prewarm);
                for (int i = 0; i < entry.prewarm; i++) temp.Add(pool.Get());
                foreach (var c in temp) pool.Release(c);

                _pools[entry.id] = pool;
            }
        }

        public DebrisChunk Get(string id)
        {
            if (_active >= globalMaxActive || !_pools.TryGetValue(id, out var pool)) return null;
            return pool.Get();
        }
    }
}
