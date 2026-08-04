using SharpHook.Reactive;
using SharpHook;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Reactive.Linq;
using SharpHook.Data;
using GameEngine.Core;
using GameEngine.Demo;
using GameEngine.Core.Systems;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
using System.Runtime;

namespace GameEngine.Runner.Maui
{
    public partial class HelloBitmapPage : ContentPage
    {
        private Engine _gameEngine;
        ReactiveGlobalHook _keyboardHook = null!;

        private int _invalidationsPending = 0;

        public HelloBitmapPage()
        {
            SKCanvasView canvasView = new SKCanvasView();
            canvasView.PaintSurface += OnCanvasViewPaintSurface;

            _gameEngine = new Engine(() =>
            {
                if (Interlocked.Exchange(ref _invalidationsPending, 1) == 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _invalidationsPending = 0;
                        canvasView.InvalidateSurface();
                    });
                }
            }, audioEnabled: false);

            _gameEngine.ChangeScene(new ScenePong());
            _gameEngine.TargetFrameRate = 240;
            Content = canvasView;
            ConfigureKeyEvents();
            _keyboardHook.RunAsync();

            _gameEngine.Start(); // Dont await this, it will block the UI thread
        }

        public void ConfigureKeyEvents()
        {
            _keyboardHook = new ReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
            
            _keyboardHook.KeyPressed
                .Subscribe(KeyPressed);

            _keyboardHook.KeyReleased
                .Subscribe(KeyReleased);
        }

        void KeyPressed(KeyboardHookEventArgs args)
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyDown(value);

        }

        void KeyReleased(KeyboardHookEventArgs args)
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyUp(value);
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

            _gameEngine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas, _gameEngine.GetRenderSnapshot());

            // Resume updates after first visible frame of a new scene
            _gameEngine.NotifyFirstPresent();
        }
    }
}
