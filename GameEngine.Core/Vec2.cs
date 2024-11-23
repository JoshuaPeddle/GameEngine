namespace GameEngine.Core
{
    public class Vec2
    {
        private double x;
        private double y;

        public Vec2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public double X { get => x; set => x = value; }
        public double Y { get => y; set => y = value; }

        public static Vec2 operator +(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X + b.X, a.Y + b.Y);
        }

        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X - b.X, a.Y - b.Y);
        }

        public static Vec2 operator *(Vec2 a, double b)
        {
            return new Vec2(a.X * b, a.Y * b);
        }

        public static Vec2 operator /(Vec2 a, double b)
        {
            return new Vec2(a.X / b, a.Y / b);
        }

        public static Vec2 operator *(double a, Vec2 b)
        {
            return new Vec2(a * b.X, a * b.Y);
        }

        public static Vec2 operator /(double a, Vec2 b)
        {
            return new Vec2(a / b.X, a / b.Y);
        }

        public static Vec2 operator -(Vec2 a)
        {
            return new Vec2(-a.X, -a.Y);
        }

        public static bool operator ==(Vec2 a, Vec2 b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(Vec2 a, Vec2 b)
        {
            return a.X != b.X || a.Y != b.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Vec2 d &&
                   x == d.x &&
                   y == d.y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public float Magnitude()
        {
            return (float)Math.Sqrt((X * X) + (Y * Y));
        }

        public Vec2 Normalize()
        {
            return this / Magnitude();
        }

        public double Dot(Vec2 other)
        {
            return (X * other.X) + (Y * other.Y);
        }

        public double Cross(Vec2 other)
        {
            return (X * other.Y) - (Y * other.X);
        }

        public Vec2 Project(Vec2 other)
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

        public Vec2 Clone()
        {
            return new Vec2(X, Y);
        }

        public static Vec2 Zero => new(0, 0);
        public static Vec2 One => new(1, 1);
    }
}
