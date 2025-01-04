using Avalonia.Media;
using Avalonia.Controls;
using Avalonia;
using GameEngine.Core.Systems;
using GameEngine.Core;
using SkiaSharp;
using GameEngine.Demo;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using SharpHook.Reactive;
using SharpHook;
using SharpHook.Native;
using System;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Platform;
using Avalonia.Skia;

namespace GameEngine.Runner.Avalonia
{
    public class GameView : Control
    {
        private Engine _gameEngine;
        SimpleReactiveGlobalHook _keyboardHook;

        private SKSurface _surface;
        private Bitmap _bitmap;

        public GameView()
        {
            _gameEngine = new Engine(InvalidateVisual);
            _gameEngine.ChangeScene(new SceneJson());
            ConfigureKeyEvents();
            _keyboardHook.RunAsync();
            _gameEngine.Start();
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), _gameEngine));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _surface?.Dispose();
            _bitmap?.Dispose();
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
    }

    class CustomDrawOp : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }
        private Engine _engine;

        public CustomDrawOp(Rect bounds, Engine engine)
        {
            Bounds = bounds;
            _engine = engine;
        }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation other) => false;

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);

            canvas.Restore();
        }
    }
}
