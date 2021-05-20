using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    public class PolarVector2
    {
        public double magnitude;
        private double angle; // needs to always be positive for easy calculations.
        public double Angle
        {
            get { return angle; }
            set
            {
                // Need to ensure that angle is between 0 and 2pi.
                if (value < 0)
                {
                    int mul = (int)Math.Abs(value / (2.0 * Math.PI)) + 1;
                    angle = value + 2.0 * Math.PI * mul;
                }
                else if (value >= 2.0 * Math.PI)
                {
                    int mul = (int)(value / (2.0 * Math.PI));
                    angle = value - 2.0 * Math.PI * mul;
                }
                else angle = value;
            }
        }

        public PolarVector2(double m, double a)
        {
            magnitude = m;
            Angle = a;
        }

        public PolarVector2(Vector2 v)
        {
            magnitude = Math.Sqrt(Math.Pow(v.X, 2) + Math.Pow(v.Y, 2));
            Angle = Math.Atan2(v.Y, v.X);
        }

        public Vector2 ToVector2()
        {

            return new Vector2((float)(magnitude * Math.Cos(angle)), (float)(magnitude * Math.Sin(angle)));

        }

        public Vector2 ToVector2(double xAspectRatio)
        {
            Vector2 v = ToVector2();
            v.X /= (float)xAspectRatio;
            return v;
        }

    }
}
