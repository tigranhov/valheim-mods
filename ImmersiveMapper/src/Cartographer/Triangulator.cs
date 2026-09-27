using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>
    /// Cuts a hand-drawn outline into triangles by ear clipping. A freehand outline may cross itself; then no clean ear
    /// is left at some point and one is clipped anyway, so the fill still covers roughly what was drawn.
    /// </summary>
    internal static class Triangulator
    {
        public static List<int> Triangulate(List<Vector2> points)
        {
            var triangles = new List<int>();
            int n = points.Count;
            if (n < 3)
            {
                return triangles;
            }
            // Work counter-clockwise, so a convex corner always turns left.
            bool counterClockwise = SignedArea(points) > 0f;
            var ring = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                ring.Add(counterClockwise ? i : n - 1 - i);
            }

            int at = 0;
            int misses = 0;
            int guard = n * n + 16;
            while (ring.Count > 3 && guard-- > 0)
            {
                int count = ring.Count;
                at %= count;
                int a = ring[(at + count - 1) % count];
                int b = ring[at];
                int c = ring[(at + 1) % count];
                if (misses > count || IsEar(points, ring, a, b, c))
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    ring.RemoveAt(at);
                    misses = 0;
                }
                else
                {
                    at++;
                    misses++;
                }
            }
            if (ring.Count == 3)
            {
                triangles.Add(ring[0]);
                triangles.Add(ring[1]);
                triangles.Add(ring[2]);
            }
            return triangles;
        }

        private static bool IsEar(List<Vector2> points, List<int> ring, int a, int b, int c)
        {
            Vector2 pa = points[a];
            Vector2 pb = points[b];
            Vector2 pc = points[c];
            if (Cross(pb - pa, pc - pb) <= 0f)
            {
                return false;
            }
            foreach (int i in ring)
            {
                if (i == a || i == b || i == c)
                {
                    continue;
                }
                if (Inside(points[i], pa, pb, pc))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, p - a) > 0f && Cross(c - b, p - b) > 0f && Cross(a - c, p - c) > 0f;
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        private static float SignedArea(List<Vector2> points)
        {
            float area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                area += points[j].x * points[i].y - points[i].x * points[j].y;
            }
            return area * 0.5f;
        }
    }
}
