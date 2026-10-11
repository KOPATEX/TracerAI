using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracerAI.Model
{
    public abstract class PcbElement
    {
        // tolerance - допустимая погрешность клика в мировой системе координат (мм)
        public abstract bool IsHit(Point2D point, float tolerance = 0.5f);
        public PcbLayer Layer { get; set; }

    }


    public class PcbTrack : PcbElement
    {

        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public float Width { get; set; } = 1.5f;

        public override bool IsHit(Point2D point, float tolerance = 0.5f)
        {
            // Расстояние от точки до отрезка линии
            float maxDist = (Width / 2f) + tolerance;
            return DistanceToSegment(point, Start, End) <= maxDist;
        }

        private float DistanceToSegment(Point2D p, Point2D a, Point2D b)
        {
            float l2 = (float)(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
            if (l2 == 0) return (float)Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));

            float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / l2));
            Point2D projection = new Point2D(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            return (float)Math.Sqrt(Math.Pow(p.X - projection.X, 2) + Math.Pow(p.Y - projection.Y, 2));
        }
    }

    public class PcbPad : PcbElement
    {
        public Point2D Position { get; set; }
        public Point2D Size { get; set; } = new Point2D(1.5f, 1.5f);
        public float HoleSize { get; set; } = 0.8f;

        public override bool IsHit(Point2D point, float tolerance = 0.5f)
        {
            float radius = (Size.X / 2f) + tolerance;
            float dist = (float)Math.Sqrt(Math.Pow(point.X - Position.X, 2) + Math.Pow(point.Y - Position.Y, 2));
            return dist <= radius;
        }
    }

    public class PcbCircle : PcbElement
    {
        public Point2D Center { get; set; }
        public float Radius { get; set; }

        public override bool IsHit(Point2D point, float tolerance = 0.5f)
        {
            float dist = (float)Math.Sqrt(Math.Pow(point.X - Center.X, 2) + Math.Pow(point.Y - Center.Y, 2));
            return Math.Abs(dist - Radius) <= tolerance; // Попадание в контур окружности
        }
    }

    public class PcbRectangle : PcbElement
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }

        public override bool IsHit(Point2D point, float tolerance = 0.5f)
        {
            float minX = Math.Min(Start.X, End.X) - tolerance;
            float maxX = Math.Max(Start.X, End.X) + tolerance;
            float minY = Math.Min(Start.Y, End.Y) - tolerance;
            float maxY = Math.Max(Start.Y, End.Y) + tolerance;

            return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
        }
    }
}
