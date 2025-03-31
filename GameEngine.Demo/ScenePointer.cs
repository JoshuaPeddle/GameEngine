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

