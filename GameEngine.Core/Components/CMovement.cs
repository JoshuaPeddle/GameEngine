namespace GameEngine.Core.Components
{
    public class CMovement : Component
    {
        public int Speed { get; set; } = 600;

        public CMovement() { }

        public CMovement(int speed)
        {
            Speed = speed;
        }
    }
}
