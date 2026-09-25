using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracerAI.Model
{
    public abstract class PcbElement { }

    // 1. Прямоугольник (Контур платы / Заливка)
    public class PcbRectangle : PcbElement
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
    }

    // 2. Дорожка (Track)
    public class PcbTrack : PcbElement
    {
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public float Width { get; set; } = 1.5f;
    }

    // 3. Контактная площадка (Pad) — ДОБАВЛЕНО
    public class PcbPad : PcbElement
    {
        public Point2D Position { get; set; }
        public Point2D Size { get; set; } = new Point2D(1.5f, 1.5f);
        public float HoleSize { get; set; } = 0.8f;
    }

    // 4. Окружность (Circle) — ДОБАВЛЕНО
    public class PcbCircle : PcbElement
    {
        public Point2D Center { get; set; }
        public float Radius { get; set; }
    }

    public class PcbEditor : PcbElement
    {

    }
}
