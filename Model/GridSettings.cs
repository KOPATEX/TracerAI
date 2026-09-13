using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracerAI.Model
{
    public class GridSettings
    {
        public bool IsSnapEnabled { get; set; } = true;
        public float Step { get; set; } = 10.0f;
    }

    public struct Point2D
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Point2D(float x, float y) : this()
        {
            X = x;
            Y = y;
        }
    }
}
