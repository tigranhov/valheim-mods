using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    internal enum Pen : byte
    {
        Charcoal = 0,
        Ink = 1,
    }

    internal sealed class Stroke
    {
        public Pen Pen;
        public byte Color;
        public readonly List<Vector2> Points = new List<Vector2>();
    }

    internal sealed class Stamp
    {
        public string Id;
        public Vector2 Position;
    }

    internal sealed class Note
    {
        public string Text;
        public Vector2 Position;
    }

    /// <summary>
    /// One sheet of parchment: strokes, stamps and notes, in sheet space (0..1 on both axes, origin bottom-left).
    /// Nothing on a sheet refers to the world; it only holds what the player drew.
    /// Saved as quantized vectors, compressed, so a full sheet stays a few kilobytes.
    /// </summary>
    internal sealed class Sheet
    {
        /// <summary>Width over height, the same everywhere a sheet is shown.</summary>
        public const float Aspect = 1.4f;

        private const int Version = 1;

        public readonly List<Stroke> Strokes = new List<Stroke>();
        public readonly List<Stamp> Stamps = new List<Stamp>();
        public readonly List<Note> Notes = new List<Note>();

        /// <summary>Set when the saved data couldn't be read: the sheet shows empty and is never saved over.</summary>
        public bool Unreadable;

        public int PointCount
        {
            get
            {
                int count = 0;
                foreach (Stroke stroke in Strokes)
                {
                    count += stroke.Points.Count;
                }
                return count;
            }
        }

        public int MarkCount => Stamps.Count + Notes.Count;

        public string Encode()
        {
            var pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(Strokes.Count);
            foreach (Stroke stroke in Strokes)
            {
                pkg.Write((byte)stroke.Pen);
                pkg.Write(stroke.Color);
                pkg.Write(stroke.Points.Count);
                foreach (Vector2 point in stroke.Points)
                {
                    WritePoint(pkg, point);
                }
            }
            pkg.Write(Stamps.Count);
            foreach (Stamp stamp in Stamps)
            {
                pkg.Write(stamp.Id ?? "");
                WritePoint(pkg, stamp.Position);
            }
            pkg.Write(Notes.Count);
            foreach (Note note in Notes)
            {
                pkg.Write(note.Text ?? "");
                WritePoint(pkg, note.Position);
            }
            return Convert.ToBase64String(Utils.Compress(pkg.GetArray()));
        }

        public static Sheet Decode(string text)
        {
            var sheet = new Sheet();
            if (string.IsNullOrEmpty(text))
            {
                return sheet;
            }
            try
            {
                var pkg = new ZPackage(Utils.Decompress(Convert.FromBase64String(text)));
                int version = pkg.ReadInt();
                if (version > Version)
                {
                    Plugin.Log.LogWarning($"Sheet saved by a newer version ({version}); showing it empty and leaving the saved data alone.");
                    return new Sheet { Unreadable = true };
                }
                int strokes = pkg.ReadInt();
                for (int i = 0; i < strokes; i++)
                {
                    var stroke = new Stroke { Pen = (Pen)pkg.ReadByte(), Color = pkg.ReadByte() };
                    int points = pkg.ReadInt();
                    for (int j = 0; j < points; j++)
                    {
                        stroke.Points.Add(ReadPoint(pkg));
                    }
                    sheet.Strokes.Add(stroke);
                }
                int stamps = pkg.ReadInt();
                for (int i = 0; i < stamps; i++)
                {
                    sheet.Stamps.Add(new Stamp { Id = pkg.ReadString(), Position = ReadPoint(pkg) });
                }
                int notes = pkg.ReadInt();
                for (int i = 0; i < notes; i++)
                {
                    sheet.Notes.Add(new Note { Text = pkg.ReadString(), Position = ReadPoint(pkg) });
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read a sheet: {e.Message}");
                return new Sheet { Unreadable = true };
            }
            return sheet;
        }

        private static void WritePoint(ZPackage pkg, Vector2 point)
        {
            pkg.Write((ushort)Mathf.RoundToInt(Mathf.Clamp01(point.x) * ushort.MaxValue));
            pkg.Write((ushort)Mathf.RoundToInt(Mathf.Clamp01(point.y) * ushort.MaxValue));
        }

        private static Vector2 ReadPoint(ZPackage pkg)
        {
            float x = pkg.ReadUShort() / (float)ushort.MaxValue;
            float y = pkg.ReadUShort() / (float)ushort.MaxValue;
            return new Vector2(x, y);
        }
    }
}
