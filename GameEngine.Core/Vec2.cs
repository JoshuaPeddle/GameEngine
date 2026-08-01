namespace GameEngine.Core
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
           X = x;
           Y = y;
        }

        public Vec2(SkiaSharp.SKSize size) : this()
        {
            X = size.Width;
            Y = size.Height;
        }

        public static Vec2 operator +(in Vec2 a, in Vec2 b)
        {
            return new Vec2(a.X + b.X, a.Y + b.Y);
        }

        public static Vec2 operator -(in Vec2 a, in Vec2 b)
        {
            return new Vec2(a.X - b.X, a.Y - b.Y);
        }

        public static Vec2 operator *(in Vec2 a, double b)
        {
            return new Vec2(a.X * b, a.Y * b);
        }

        public static Vec2 operator /(in Vec2 a, double b)
        {
            return new Vec2(a.X / b, a.Y / b);
        }

        public static Vec2 operator *(double a, Vec2 b)
        {
            return new Vec2(a * b.X, a * b.Y);
        }

        public static Vec2 operator /(double a, in Vec2 b)
        {
            return new Vec2(a / b.X, a / b.Y);
        }

        public static Vec2 operator -(in Vec2 a)
        {
            return new Vec2(-a.X, -a.Y);
        }

        public static bool operator ==(in Vec2 a, in Vec2 b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(in Vec2 a, in Vec2 b)
        {
            return a.X != b.X || a.Y != b.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Vec2 d &&
                   X == d.X &&
                   Y == d.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public double Magnitude() => Length();

        public Vec2 Normalize()
        {
            double length = Length();
            return length > 0 ? this / length : Zero;
        }

        public double Dot(in Vec2 other)
        {
            return (X * other.X) + (Y * other.Y);
        }

        public double Cross(in Vec2 other)
        {
            return (X * other.Y) - (Y * other.X);
        }

        public Vec2 Project(in Vec2 other)
        {
            return other * (Dot(other) / other.Dot(other));
        }

        public Vec2 Rotate(float angle)
        {
            float rad = (float)(angle * Math.PI / 180);
            float sin = (float)Math.Sin(rad);
            float cos = (float)Math.Cos(rad);
            return new Vec2((X * cos) - (Y * sin), (X * sin) + (Y * cos));
        }

        public double Length()
        {
            return Math.Sqrt((X * X) + (Y * Y));
        }

        public double LengthSquared() => (X * X) + (Y * Y);

        public double DistanceTo(in Vec2 other) => (this - other).Length();

        public static Vec2 Lerp(in Vec2 from, in Vec2 to, double amount) =>
            new(from.X + ((to.X - from.X) * amount), from.Y + ((to.Y - from.Y) * amount));

        public bool Equals(Vec2 other)
        {
            return 
               X == other.X &&
               Y == other.Y;
        }

        public static Vec2 Zero => new(0, 0);
        public static Vec2 One => new(1, 1);

        public double Angle => Math.Atan2(Y, X);

        public Vec2 Round(int digits)
        {
            return new Vec2(Math.Round(X, digits), Math.Round(Y, digits));
        }
    }
}
