using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Demo;
using SharpHook;
using SharpHook.Native;
using SharpHook.Reactive;
using System;
using System.Collections.Generic;

namespace GameEngine.Runner.Avalonia
{
    public class GameView : Control
    {
        private readonly Engine _gameEngine;
        private readonly SimpleReactiveGlobalHook _keyboardHook;

        public GameView()
        {
            _gameEngine = new Engine(InvalidateVisual);
            _gameEngine.ChangeScene(new SceneSnake());
            _keyboardHook = new SimpleReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
            ConfigureKeyEvents();
            _keyboardHook.RunAsync();
            _gameEngine.Start();
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), _gameEngine));
        }

        public void ConfigureKeyEvents()
        {
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
    }

    class CustomDrawOp : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }
        private readonly Engine _engine;

        public CustomDrawOp(Rect bounds, Engine engine)
        {
            Bounds = bounds;
            _engine = engine;
        }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);

            canvas.Restore();
        }
    }
}
