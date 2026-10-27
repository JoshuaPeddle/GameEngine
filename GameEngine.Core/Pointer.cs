namespace GameEngine.Core;

public static class Pointer
{
    public enum PointerEventType { Press, Move, Release, Cancel }

    public class PointerEvent(Vec2 position, long contactId, bool isCaptured = false)
    {
        public PointerEvent(Vec2 position) : this(position, 0) { }
        public Vec2 Position { get; set; } = position;
        public long ContactId { get; } = contactId;
        public bool IsCaptured { get; } = isCaptured;
    }

    public class PointerPressEvent(Vec2 position, long contactId, bool isCaptured = false)
        : PointerEvent(position, contactId, isCaptured)
    {
        public PointerPressEvent(Vec2 position) : this(position, 0) { }
    }
    public class PointerMoveEvent(Vec2 position, long contactId, bool isCaptured = false)
        : PointerEvent(position, contactId, isCaptured)
    {
        public PointerMoveEvent(Vec2 position) : this(position, 0) { }
    }
    public class PointerReleaseEvent(Vec2 position, long contactId, bool isCaptured = false)
        : PointerEvent(position, contactId, isCaptured)
    {
        public PointerReleaseEvent(Vec2 position) : this(position, 0) { }
    }
    public class PointerCancelEvent(Vec2 position, long contactId, bool isCaptured = false)
        : PointerEvent(position, contactId, isCaptured)
    {
        public PointerCancelEvent(Vec2 position) : this(position, 0) { }
    }
}
