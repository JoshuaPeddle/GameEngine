namespace GameEngine.Core.Components
{
    public class CInput : Component
    {
        public bool Up = false;
        public bool Down = false;
        public bool Left = false;
        public bool Right = false;
        public bool Any => Up || Down || Left || Right;
    }
}
