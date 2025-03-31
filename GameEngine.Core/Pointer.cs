namespace GameEngine.Core
{
    public static class Pointer
    {
        public enum PointerEventType
        {
            Press,
            Move,
            Release
        }

        public class PointerEvent
        {
            public Vec2 Position { get; }
            public PointerEvent(Vec2 position)
            {
                Position = position;
            }
        }

        public class PointerPressEvent : PointerEvent
        {
            public PointerPressEvent(Vec2 position) : base(position) { }
        }

        public class PointerMoveEvent : PointerEvent
        {
            public PointerMoveEvent(Vec2 position) : base(position) { }
        }

        public class PointerReleaseEvent : PointerEvent
        {
            public PointerReleaseEvent(Vec2 position) : base(position) { }
        }
    }
}
