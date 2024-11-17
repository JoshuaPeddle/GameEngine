namespace GameEngine.Core
{
    public class Vec2
    {
        private float x;
        private float y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float X { get => x; set => x = value; }
        public float Y { get => y; set => y = value; }

        public static Vec2 operator +(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X + b.X, a.Y + b.Y);
        }

        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X - b.X, a.Y - b.Y);
        }

        public static Vec2 operator *(Vec2 a, float b)
        {
            return new Vec2(a.X * b, a.Y * b);
        }

        public static Vec2 operator /(Vec2 a, float b)
        {
            return new Vec2(a.X / b, a.Y / b);
        }

        public static Vec2 operator *(float a, Vec2 b)
        {
            return new Vec2(a * b.X, a * b.Y);
        }

        public static Vec2 operator /(float a, Vec2 b)
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
            return (float)Math.Sqrt(X * X + Y * Y);
        }

        public Vec2 Normalize()
        {
            return this / Magnitude();
        }

        public float Dot(Vec2 other)
        {
            return X * other.X + Y * other.Y;
        }

        public float Cross(Vec2 other)
        {
            return X * other.Y - Y * other.X;
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
            return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
        }

        public double Length()
        {
            return Math.Sqrt(X * X + Y * Y);
        }
    }
}
