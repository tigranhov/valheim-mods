using System;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>
    /// While a followed swing is being handled, loot from the chunks it breaks is moved to just in front of the miner, so
    /// buried chunks don't leave it inside the ground.
    /// </summary>
    internal static class LootDrop
    {
        private const float Distance = 0.8f;
        private const float Height = 1f;
        private const float Spread = 0.2f;

        private static Player _miner;
        private static int _solidMask;

        public static Scope For(Player miner)
        {
            Player previous = _miner;
            _miner = miner;
            return new Scope(previous);
        }

        /// <summary>For each item a rock drops: moves it in front of the miner when a followed swing broke the chunk.</summary>
        public static void Place(GameObject item)
        {
            if (_miner == null || item == null || FollowConfig.Loot.Value != LootPlace.InFront)
            {
                return;
            }
            item.transform.position = InFront(_miner) + UnityEngine.Random.insideUnitSphere * Spread;
        }

        // At chest height a step ahead of the miner, or closer if the rock is right there.
        private static Vector3 InFront(Player miner)
        {
            if (_solidMask == 0)
            {
                _solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            }
            Vector3 forward = miner.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 origin = miner.transform.position + Vector3.up * Height;
            float distance = Distance;
            if (Physics.SphereCast(origin, Spread, forward, out RaycastHit blocked, Distance, _solidMask, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0f, blocked.distance - 0.1f);
            }
            return origin + forward * distance;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly Player _previous;

            public Scope(Player previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                _miner = _previous;
            }
        }
    }
}
