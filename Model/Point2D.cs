using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracerAI.Model
{
    public class Point2D
    {
        public float X { get; set; }
        public float Y { get; set; }

        // Конструктор по умолчанию (нужен для привязок WPF и сериализации)
        public Point2D()
        {
            X = 0;
            Y = 0;
        }

        // Конструктор с параметрами
        public Point2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        // Конструктор копирования (для создания глубокой копии точки)
        public Point2D(Point2D other)
        {
            if (other != null)
            {
                X = other.X;
                Y = other.Y;
            }
        }
    }
}