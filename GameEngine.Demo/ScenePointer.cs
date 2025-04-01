using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using static GameEngine.Core.Pointer;

namespace GameEngine.Demo
{
    public class ScenePointer : Scene
    {
        public override int VirtualWidth => 500;
        public override int VirtualHeight => 500;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
        {
            var entity = entityManager.CreateEntity("entity");
            entity.AddComponent(new CTransform(new Vec2(100, 100)));
            var pointer = new CPointer();
            entity.AddComponent(pointer);
            entity.AddComponent(new CText("Pointer", 24));

            inputManager.ActionMapper.MapPointerActionToComponent<CPointer>(PointerEventType.Press, entity, (cPointer, pointerEvent) =>
            {
                cPointer.PressPosition = pointerEvent.Position;
            });

            inputManager.ActionMapper.MapPointerActionToComponent<CPointer>(PointerEventType.Move, entity, (cPointer, pointerEvent) =>
            {
                cPointer.MovePosition = pointerEvent.Position;
            });

            inputManager.ActionMapper.MapPointerActionToComponent<CPointer>(PointerEventType.Release, entity, (cPointer, pointerEvent) =>
            {
                cPointer.ReleasePosition = pointerEvent.Position;
            });

            var press = entityManager.CreateEntity("press");
            press.AddComponent(new CTransform(new Vec2(100, 200)));
            press.AddComponent(new CText("Press", 24));

            // Move the press text to the location of the press event
            inputManager.ActionMapper.MapPointerActionToComponent<CTransform>(PointerEventType.Press, press, (transform, pointerEvent) =>
            {
                transform.Position = pointerEvent.Position;
            });

            var move = entityManager.CreateEntity("move");
            move.AddComponent(new CTransform(new Vec2(100, 300)));
            move.AddComponent(new CText("Move", 24));

            // Move the move text to the location of the move event
            inputManager.ActionMapper.MapPointerActionToComponent<CTransform>(PointerEventType.Move, move, (transform, pointerEvent) =>
            {
                transform.Position = pointerEvent.Position;
            });

            var release = entityManager.CreateEntity("release");
            release.AddComponent(new CTransform(new Vec2(100, 400)));
            release.AddComponent(new CText("Release", 24));

            // Move the release text to the location of the release event
            inputManager.ActionMapper.MapPointerActionToComponent<CTransform>(PointerEventType.Release, release, (transform, pointerEvent) =>
            {
                transform.Position = pointerEvent.Position;
            });

            var virtualOrigin = entityManager.CreateEntity("virtualOrigin");
            virtualOrigin.AddComponent(new CTransform(new Vec2(0, 0)));

            var virtualWidth = entityManager.CreateEntity("virtualWidth");
            virtualWidth.AddComponent(new CTransform(new Vec2(VirtualWidth, 0)));

            var virtualHeight = entityManager.CreateEntity("virtualHeight");
            virtualHeight.AddComponent(new CTransform(new Vec2(0, VirtualHeight)));



        }

        public override void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaTime)
        {
            var entity = entityManager.GetEntityWithTag("entity");
            var cPointer = entity.GetComponent<CPointer>();
            var cText = entity.GetComponent<CText>();
            cText.Text = $"Press: {cPointer.PressPosition}\nMove: {cPointer.MovePosition}\nRelease: {cPointer.ReleasePosition}";
        }
    }

    class CPointer : Component
    {
        public Vec2 PressPosition { get; set; }
        public Vec2 MovePosition { get; set; }
        public Vec2 ReleasePosition { get; set; }
    }
}

