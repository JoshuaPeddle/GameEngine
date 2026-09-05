namespace GameEngine.Editor.Models
{
    public class Animation
    {
        public string Name { get; set; } = string.Empty;
        public Texture Texture { get; set; } = new();
        public int FrameCount { get; set; }
        public float Delay { get; set; } // Delay between frames in milliseconds


        public Animation(string name, Texture texture, int frameCount, float delay)
        {
            Name = name;
            Texture = texture;
            FrameCount = frameCount;
            Delay = delay;
        }

        public Animation(string name)
        {
            Name = name;
        }

        public Animation() { }
    }
}
