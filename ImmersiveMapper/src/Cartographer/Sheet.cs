using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.Cartographer
{
    internal enum Pen : byte
    {
        Charcoal = 0,
        Ink = 1,
        /// <summary>A wide, see-through colour wash, drawn under the lines.</summary>
        Wash = 2,
        /// <summary>A faint line traced from a draft, to ink over and then clear.</summary>
        Guide = 3,
    }

    internal enum LineStyle : byte
    {
        Solid = 0,
        Dotted = 1,
    }

    internal sealed class Stroke
    {
        public Pen Pen;
        public byte Color;
        public LineStyle Style;
        /// <summary>Line width in canvas units.</summary>
        public float Width;
        /// <summary>255 is solid; lower is see-through.</summary>
        public byte Alpha = 255;
        public readonly List<Vector2> Points = new List<Vector2>();
    }

    /// <summary>A closed outline filled with a see-through colour, drawn under everything else.</summary>
    internal sealed class Fill
    {
        public byte Color;
        /// <summary>255 is solid; lower is see-through.</summary>
        public byte Alpha = 255;
        public readonly List<Vector2> Points = new List<Vector2>();

        private List<int> _triangles;

        /// <summary>The outline cut into triangles (indices into Points), worked out once. Not saved.</summary>
        public List<int> Triangles => _triangles ?? (_triangles = Triangulator.Triangulate(Points));
    }

    internal sealed class Stamp
    {
        public string Id;
        public Vector2 Position;
        /// <summary>Height in canvas units.</summary>
        public float Size;
    }

    internal sealed class Note
    {
        public string Text;
        public Vector2 Position;
        /// <summary>Text height in canvas units.</summary>
        public float Size;
    }

    /// <summary>
    /// A drawing: strokes, fills, stamps and notes in canvas units, where one unit is the height of a draft sheet
    /// (a draft spans 0..Aspect by 0..1; a master map has no edges and grows as you draw).
    /// Nothing in a drawing refers to the world; it only holds what the player drew.
    /// Saved as compressed vectors: a first point, then small steps.
    /// </summary>
    internal sealed class Sheet
    {
        /// <summary>A draft sheet's width over its height.</summary>
        public const float Aspect = 1.4f;
        /// <summary>A master map's grid square, in canvas units.</summary>
        public const float GridSquare = 0.1f;
        public const int DefaultPacesPerSquare = 100;

        public const float DefaultStampSize = 0.065f;
        public const float DefaultNoteSize = 0.036f;

        private const int Version = 3;
        // Version 2 had no opacity: washes and fills were see-through, everything else solid.
        private const byte OldWashAlpha = 150;
        private const byte OldFillAlpha = 110;
        // Steps are stored in 1/4096 of a unit; a longer step is written out in full after this marker.
        private const float StepScale = 4096f;
        private const short FullPoint = short.MinValue;

        public readonly List<Stroke> Strokes = new List<Stroke>();
        public readonly List<Fill> Fills = new List<Fill>();
        public readonly List<Stamp> Stamps = new List<Stamp>();
        public readonly List<Note> Notes = new List<Note>();

        /// <summary>A master map's scale: how many paces one grid square stands for.</summary>
        public int PacesPerSquare = DefaultPacesPerSquare;

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
                foreach (Fill fill in Fills)
                {
                    count += fill.Points.Count;
                }
                return count;
            }
        }

        public int MarkCount => Stamps.Count + Notes.Count;

        public bool IsEmpty => Strokes.Count == 0 && Fills.Count == 0 && Stamps.Count == 0 && Notes.Count == 0;

        public void Clear()
        {
            Strokes.Clear();
            Fills.Clear();
            Stamps.Clear();
            Notes.Clear();
        }

        /// <summary>The smallest box around everything drawn, or false when nothing is.</summary>
        public bool TryGetBounds(out Rect bounds)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            void Add(Vector2 p)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            foreach (Stroke stroke in Strokes)
            {
                stroke.Points.ForEach(Add);
            }
            foreach (Fill fill in Fills)
            {
                fill.Points.ForEach(Add);
            }
            foreach (Stamp stamp in Stamps)
            {
                Add(stamp.Position);
            }
            foreach (Note note in Notes)
            {
                Add(note.Position);
            }
            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return min.x <= max.x;
        }

        public byte[] ToBytes()
        {
            var pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(PacesPerSquare);
            pkg.Write(Strokes.Count);
            foreach (Stroke stroke in Strokes)
            {
                pkg.Write((byte)stroke.Pen);
                pkg.Write(stroke.Color);
                pkg.Write((byte)stroke.Style);
                pkg.Write(stroke.Width);
                pkg.Write(stroke.Alpha);
                WritePoints(pkg, stroke.Points);
            }
            pkg.Write(Fills.Count);
            foreach (Fill fill in Fills)
            {
                pkg.Write(fill.Color);
                pkg.Write(fill.Alpha);
                WritePoints(pkg, fill.Points);
            }
            pkg.Write(Stamps.Count);
            foreach (Stamp stamp in Stamps)
            {
                pkg.Write(stamp.Id ?? "");
                pkg.Write(stamp.Position.x);
                pkg.Write(stamp.Position.y);
                pkg.Write(stamp.Size);
            }
            pkg.Write(Notes.Count);
            foreach (Note note in Notes)
            {
                pkg.Write(note.Text ?? "");
                pkg.Write(note.Position.x);
                pkg.Write(note.Position.y);
                pkg.Write(note.Size);
            }
            return Utils.Compress(pkg.GetArray());
        }

        public string Encode()
        {
            return Convert.ToBase64String(ToBytes());
        }

        public static Sheet Decode(string text)
        {
            return string.IsNullOrEmpty(text) ? new Sheet() : FromBytes(Convert.FromBase64String(text));
        }

        public static Sheet FromBytes(byte[] bytes)
        {
            var sheet = new Sheet();
            if (bytes == null || bytes.Length == 0)
            {
                return sheet;
            }
            try
            {
                var pkg = new ZPackage(Utils.Decompress(bytes));
                int version = pkg.ReadInt();
                if (version == 1)
                {
                    ReadVersion1(pkg, sheet);
                    return sheet;
                }
                if (version > Version)
                {
                    Plugin.Log.LogWarning($"Drawing saved by a newer version ({version}); showing it empty and leaving the saved data alone.");
                    return new Sheet { Unreadable = true };
                }
                sheet.PacesPerSquare = pkg.ReadInt();
                int strokes = pkg.ReadInt();
                for (int i = 0; i < strokes; i++)
                {
                    var stroke = new Stroke { Pen = (Pen)pkg.ReadByte(), Color = pkg.ReadByte(), Style = (LineStyle)pkg.ReadByte(), Width = pkg.ReadSingle() };
                    stroke.Alpha = version >= 3 ? pkg.ReadByte() : stroke.Pen == Pen.Wash ? OldWashAlpha : (byte)255;
                    ReadPoints(pkg, stroke.Points);
                    sheet.Strokes.Add(stroke);
                }
                int fills = pkg.ReadInt();
                for (int i = 0; i < fills; i++)
                {
                    var fill = new Fill { Color = pkg.ReadByte() };
                    fill.Alpha = version >= 3 ? pkg.ReadByte() : OldFillAlpha;
                    ReadPoints(pkg, fill.Points);
                    sheet.Fills.Add(fill);
                }
                int stamps = pkg.ReadInt();
                for (int i = 0; i < stamps; i++)
                {
                    sheet.Stamps.Add(new Stamp { Id = pkg.ReadString(), Position = new Vector2(pkg.ReadSingle(), pkg.ReadSingle()), Size = pkg.ReadSingle() });
                }
                int notes = pkg.ReadInt();
                for (int i = 0; i < notes; i++)
                {
                    sheet.Notes.Add(new Note { Text = pkg.ReadString(), Position = new Vector2(pkg.ReadSingle(), pkg.ReadSingle()), Size = pkg.ReadSingle() });
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read a drawing: {e.Message}");
                return new Sheet { Unreadable = true };
            }
            return sheet;
        }

        // Each step is measured from where the reader will land, not from the exact point, so rounding never adds up.
        private static void WritePoints(ZPackage pkg, List<Vector2> points)
        {
            pkg.Write(points.Count);
            if (points.Count == 0)
            {
                return;
            }
            Vector2 at = points[0];
            pkg.Write(at.x);
            pkg.Write(at.y);
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 step = (points[i] - at) * StepScale;
                int dx = Mathf.RoundToInt(step.x);
                int dy = Mathf.RoundToInt(step.y);
                if (Mathf.Abs(dx) > short.MaxValue - 1 || Mathf.Abs(dy) > short.MaxValue - 1)
                {
                    pkg.Write(FullPoint);
                    pkg.Write(points[i].x);
                    pkg.Write(points[i].y);
                    at = points[i];
                    continue;
                }
                pkg.Write((short)dx);
                pkg.Write((short)dy);
                at += new Vector2(dx, dy) / StepScale;
            }
        }

        private static void ReadPoints(ZPackage pkg, List<Vector2> points)
        {
            int count = pkg.ReadInt();
            if (count == 0)
            {
                return;
            }
            var at = new Vector2(pkg.ReadSingle(), pkg.ReadSingle());
            points.Add(at);
            for (int i = 1; i < count; i++)
            {
                short dx = pkg.ReadShort();
                if (dx == FullPoint)
                {
                    at = new Vector2(pkg.ReadSingle(), pkg.ReadSingle());
                }
                else
                {
                    at += new Vector2(dx, pkg.ReadShort()) / StepScale;
                }
                points.Add(at);
            }
        }

        // The first test build stored draft points as 0..1 of the sheet's width and height, with fixed widths.
        private static void ReadVersion1(ZPackage pkg, Sheet sheet)
        {
            Vector2 Point() => new Vector2(pkg.ReadUShort() / (float)ushort.MaxValue * Aspect, pkg.ReadUShort() / (float)ushort.MaxValue);
            int strokes = pkg.ReadInt();
            for (int i = 0; i < strokes; i++)
            {
                var stroke = new Stroke { Pen = (Pen)pkg.ReadByte(), Color = pkg.ReadByte() };
                stroke.Width = stroke.Pen == Pen.Ink ? 0.0045f : 0.011f;
                int points = pkg.ReadInt();
                for (int j = 0; j < points; j++)
                {
                    stroke.Points.Add(Point());
                }
                sheet.Strokes.Add(stroke);
            }
            int stamps = pkg.ReadInt();
            for (int i = 0; i < stamps; i++)
            {
                sheet.Stamps.Add(new Stamp { Id = pkg.ReadString(), Position = Point(), Size = DefaultStampSize });
            }
            int notes = pkg.ReadInt();
            for (int i = 0; i < notes; i++)
            {
                sheet.Notes.Add(new Note { Text = pkg.ReadString(), Position = Point(), Size = DefaultNoteSize });
            }
        }
    }
}
