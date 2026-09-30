using System.Collections.Generic;
using UnityEngine;

namespace VeinFollow
{
    /// <summary>
    /// DigGround: when a buried chunk breaks, the ground above it is dug out down to where the chunk was. The dig is the
    /// game's own terrain lowering, done on the ground under your game's care so it can be the chunk's size (the game only
    /// sends ready-made digs to whoever looks after that ground).
    /// </summary>
    internal static class Ground
    {
        private const float Margin = 0.3f;

        /// <summary>Digs out every chunk that broke in a hit and lay below the ground.</summary>
        public static void DigBroken(Rock rock, Dictionary<int, Bounds> before)
        {
            foreach (KeyValuePair<int, Bounds> chunk in before)
            {
                if (!rock.IsValid || rock.Health(chunk.Key) <= 0f)
                {
                    Dig(chunk.Value);
                }
            }
        }

        private static void Dig(Bounds chunk)
        {
            // Buried: the ground over its middle is higher than its middle.
            if (!Heightmap.GetHeight(chunk.center, out float ground) || ground <= chunk.center.y)
            {
                return;
            }
            float radius = Mathf.Max(chunk.extents.x, chunk.extents.z) + Margin;
            Vector3 top = new Vector3(chunk.center.x, ground, chunk.center.z);
            if (!PrivateArea.CheckAccess(top, radius, flash: false) || Location.IsInsideNoBuildLocation(top))
            {
                return;
            }
            var settings = new TerrainOp.Settings
            {
                m_raise = true,
                m_raiseRadius = radius,
                // Down to the chunk's bottom, level across (no falloff), only ever lowering.
                m_raiseDelta = chunk.min.y - ground,
                m_raisePower = 0f,
                m_square = false,
                m_paintCleared = true,
                m_paintType = TerrainModifier.PaintType.Dirt,
                m_paintRadius = radius,
            };
            var heightmaps = new List<Heightmap>();
            Heightmap.FindHeightmap(top, radius, heightmaps);
            foreach (Heightmap heightmap in heightmaps)
            {
                TerrainComp terrain = heightmap.GetAndCreateTerrainCompiler();
                if (terrain == null || terrain.m_nview == null || !terrain.m_nview.IsValid())
                {
                    continue;
                }
                terrain.m_nview.ClaimOwnership();
                terrain.DoOperation(top, Vector3.zero, settings);
            }
        }
    }
}
