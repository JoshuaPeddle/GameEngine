namespace GameEngine.Core.Components
{
    public class CGravity : Component
    {
        /// <summary>
        /// Downward acceleration in virtual pixels per second squared. This value used to be
        /// multiplied by a delta expressed in milliseconds, so the effective acceleration was
        /// 1000x what it claimed; the old default of 0.2 behaved as the 200 it is now.
        /// </summary>
        public double Acceleration { get; set; } = 200;
        public CGravity() { }
    }
}
