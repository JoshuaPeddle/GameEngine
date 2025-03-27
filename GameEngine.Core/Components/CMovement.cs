namespace GameEngine.Core.Components
{
    public class CMovement : Component
    {
        public double Speed { get; set; } = 600;
        public double MaxSpeed { get; set; } = 250;

        public CMovement() { }

        public CMovement(double speed) 
        {
            Speed = speed;
        }

        public CMovement(double speed, double maxSpeed)
        {
            Speed = speed;
            MaxSpeed = maxSpeed;
        }
    }
}
