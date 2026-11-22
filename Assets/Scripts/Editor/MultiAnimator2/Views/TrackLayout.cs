using System.Collections.Generic;
using System.Text;

namespace MultiAnimator2
{
    public sealed class TrackRow
    {
        public bool IsProperty;
        public int Part;
        public PropertyGroup Group;
        public float Y;
        public float Height;

        public float Bottom => Y + Height;
    }

    public static class TrackLayout
    {
        public const float HeaderWidth = 250f;
        public const float PartHeight = 28f;
        public const float PropertyHeight = 20f;

        public static List<TrackRow> Build(MultiAnimatorSession session)
        {
            var rows = new List<TrackRow>();
            float y = 0f;
            var parts = session.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                rows.Add(new TrackRow { Part = i, Y = y, Height = PartHeight });
                y += PartHeight;

                if (!session.Expanded.Contains(i))
                    continue;

                var info = ClipCache.Get(parts[i].clip);
                if (info == null)
                    continue;

                foreach (var g in info.Groups)
                {
                    rows.Add(new TrackRow { IsProperty = true, Part = i, Group = g, Y = y, Height = PropertyHeight });
                    y += PropertyHeight;
                }
            }

            return rows;
        }

        public static string Signature(MultiAnimatorSession session, List<TrackRow> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                if (r.IsProperty)
                {
                    sb.Append('p').Append(r.Group.Label).Append(';');
                    continue;
                }
                var part = session.Parts[r.Part];
                sb.Append('P').Append(part.partName).Append('|')
                  .Append(part.clip != null ? part.clip.GetInstanceID() : 0).Append('|')
                  .Append(session.Expanded.Contains(r.Part) ? 1 : 0).Append(';');
            }
            return sb.ToString();
        }

        public static TrackRow RowAt(List<TrackRow> rows, float y)
        {
            foreach (var r in rows)
                if (y >= r.Y && y < r.Bottom)
                    return r;
            return null;
        }
    }
}
