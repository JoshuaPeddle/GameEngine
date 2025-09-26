namespace GameEngine.Editor.Models
{
    public class Sound
    {
        public string Name { get; set; }
        public string Path { get; set; }

        public Sound(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}
