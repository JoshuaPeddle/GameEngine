using SharpHook.Reactive;
using SharpHook;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Reactive.Linq;
using SharpHook.Native;
using GameEngine.Core;
using GameEngine.Demo;
using GameEngine.Core.Systems;

namespace GameEngine.Runner.Maui
{
    public partial class HelloBitmapPage : ContentPage
    {
        private Engine _gameEngine;
        SimpleReactiveGlobalHook _keyboardHook;

        public HelloBitmapPage()
        {
            SKCanvasView canvasView = new SKCanvasView();
            canvasView.PaintSurface += OnCanvasViewPaintSurface;

            _gameEngine = new Engine(canvasView.InvalidateSurface);

            _gameEngine.ChangeScene(new SceneJson());

            Content = canvasView;
            ConfigureKeyEvents();
            _keyboardHook.RunAsync();
            _gameEngine.Start(); // Dont await this, it will block the UI thread

        }

        public void ConfigureKeyEvents()
        {
            _keyboardHook = new SimpleReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
            
            _keyboardHook.KeyPressed
                .Subscribe(KeyPressed);

            _keyboardHook.KeyReleased
                .Subscribe(KeyReleased);
        }

        void KeyPressed(KeyboardHookEventArgs args)
        {
            if (KeyMap.ContainsKey(args.Data.KeyCode))
                _gameEngine.Systems.Get<InputSystem>().KeyDown(KeyMap[args.Data.KeyCode]);

        }

        void KeyReleased(KeyboardHookEventArgs args)
        {
            if (KeyMap.ContainsKey(args.Data.KeyCode))
                _gameEngine.Systems.Get<InputSystem>().KeyUp(KeyMap[args.Data.KeyCode]);
        }

        public Dictionary<KeyCode, GeKeys> KeyMap { get; private set; } = new Dictionary<KeyCode, GeKeys>()
        {
            { KeyCode.VcW, GeKeys.W },
            { KeyCode.VcA, GeKeys.A },
            { KeyCode.VcS, GeKeys.S },
            { KeyCode.VcD, GeKeys.D },
            { KeyCode.VcSpace, GeKeys.Space }
        };

        void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            _gameEngine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);
        }
    }
}
