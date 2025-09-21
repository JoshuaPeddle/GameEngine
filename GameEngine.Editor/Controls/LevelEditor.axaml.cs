using Avalonia.Controls;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Threading.Tasks;
using GameEngine.Editor.ViewModels;

namespace GameEngine.Editor.Controls
{
    public partial class LevelEditor : UserControl
    {
        public LevelEditor()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private async void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is LevelEditorViewModel vm)
            {
                // Kick off initial scene discovery if project path already present
                await vm.EnsureScenesLoadedAsync();
            }
        }
    }

    class SceneLevelEditor : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
        {
            var secondEntity = entityManager.CreateEntity("background");
            secondEntity.AddComponent(new CTransform(new Vec2(500, 300)));
            secondEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), true, true));
        }
    }

}
