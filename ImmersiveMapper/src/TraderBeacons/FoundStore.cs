using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>
    /// Server-side record of which player has found which trader, one file per world in
    /// BepInEx/config/TraderBeacons/. Each line is "playerId|locationPrefab".
    /// </summary>
    internal static class FoundStore
    {
        private static readonly HashSet<string> Found = new HashSet<string>();
        private static string _filePath;
        private static bool _dirty;

        public static void EnsureLoaded()
        {
            World world = ZNet.instance != null ? ZNet.instance.GetWorld() : null;
            if (world == null)
            {
                return;
            }
            string path = Path.Combine(Paths.ConfigPath, "TraderBeacons", $"{Sanitize(world.m_name)}_{world.m_uid}.found.txt");
            if (path == _filePath)
            {
                return;
            }
            SaveIfDirty();
            _filePath = path;
            Found.Clear();
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string entry = line.Trim();
                    if (entry.Length > 0 && !entry.StartsWith("#"))
                    {
                        Found.Add(entry);
                    }
                }
            }
            Plugin.Log.LogInfo($"Loaded {Found.Count} found-trader record(s) for world {world.m_name}.");
        }

        public static void Unload()
        {
            SaveIfDirty();
            _filePath = null;
            Found.Clear();
        }

        public static bool HasFound(long playerId, string prefab)
        {
            return Found.Contains(Key(playerId, prefab));
        }

        public static void MarkFound(long playerId, string prefab)
        {
            if (Found.Add(Key(playerId, prefab)))
            {
                _dirty = true;
            }
        }

        public static int ResetPlayer(long playerId)
        {
            string prefix = playerId + "|";
            int removed = Found.RemoveWhere(e => e.StartsWith(prefix));
            if (removed > 0)
            {
                _dirty = true;
            }
            return removed;
        }

        public static void SaveIfDirty()
        {
            if (!_dirty || _filePath == null)
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            var lines = new List<string> { "# TraderBeacons: playerId|traderLocation for every trader a player has found" };
            lines.AddRange(Found.OrderBy(e => e));
            File.WriteAllLines(_filePath, lines);
            _dirty = false;
        }

        private static string Key(long playerId, string prefab)
        {
            return playerId + "|" + prefab;
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
