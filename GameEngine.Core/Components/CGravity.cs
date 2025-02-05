namespace GameEngine.Core.Components
{
    public class CGravity : Component
    {
        public double Acceleration { get; set; } = 0.2; // virtual pixels per second^2
        public CGravity() { }
    }
}
