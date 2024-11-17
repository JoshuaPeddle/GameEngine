using GameEngine.Core;
using GameEngine.Demo;

namespace GameEngine
{
    public partial class MainView : Form
    {
        public MainView()
        {
            InitializeComponent();
            var gameEngine = new Engine(skMain, new Size(1161, 671), new Point(12, 12));

            var scene = new Scene2();

            gameEngine.ChangeScene(scene);
            gameEngine.Start();
        }
    }
}
