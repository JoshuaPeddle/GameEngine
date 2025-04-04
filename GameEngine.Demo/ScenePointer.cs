using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Collections.Generic;
using static GameEngine.Core.Pointer;

namespace GameEngine.Demo
{
    public class ScenePointer : Scene
    {
        public override int VirtualWidth => 1000;
        public override int VirtualHeight => 1000;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> resetScene)
        {
            CreatePointerTrackingEntity(entityManager, inputManager);
            CreateDemoEntities(entityManager, inputManager);
            CreateVirtualBoundaryMarkers(entityManager);
        }

        private void CreatePointerTrackingEntity(EntityManager entityManager, InputManager inputManager)
        {
            var entity = entityManager.CreateEntity("entity");
            entity.AddComponent(new CTransform(new Vec2(100, 100)));
            entity.AddComponent(new CPointer());
            entity.AddComponent(new CText("Pointer", 24));

            var eventHandlers = new Dictionary<PointerEventType, Action<CPointer, Vec2>>
            {
                { PointerEventType.Press, (c, pos) => c.PressPosition = pos },
                { PointerEventType.Move, (c, pos) => c.MovePosition = pos },
                { PointerEventType.Release, (c, pos) => c.ReleasePosition = pos }
            };

            foreach (var handler in eventHandlers)
            {
                inputManager.ActionMapper.MapPointerActionToComponent<CPointer>(
                    handler.Key,
                    entity,
                    (cPointer, pointerEvent) => handler.Value(cPointer, pointerEvent.Position)
                );
            }
        }

        private void CreateDemoEntities(EntityManager entityManager, InputManager inputManager)
        {
            CreatePointerInteractiveEntity(inputManager, entityManager,
                PointerEventType.Press, "press", "Press", new Vec2(100, 200));

            CreatePointerInteractiveEntity(inputManager, entityManager,
                PointerEventType.Move, "move", "Move", new Vec2(100, 300));

            CreatePointerInteractiveEntity(inputManager, entityManager,
                PointerEventType.Release, "release", "Release", new Vec2(100, 400));
        }

        private void CreatePointerInteractiveEntity(InputManager inputManager, EntityManager entityManager,
            PointerEventType eventType, string tag, string text, Vec2 position)
        {
            var entity = entityManager.CreateEntity(tag);
            entity.AddComponent(new CTransform(position));
            entity.AddComponent(new CText(text, 24));

            inputManager.ActionMapper.MapPointerActionToComponent<CTransform>(
                eventType,
                entity,
                (transform, pointerEvent) => transform.Position = pointerEvent.Position
            );
        }

        private void CreateVirtualBoundaryMarkers(EntityManager entityManager)
        {
            var markers = new[]
            {
                new { Tag = "Origin", Position = new Vec2(0, 0) },
                new { Tag = "Width", Position = new Vec2(VirtualWidth, 0) },
                new { Tag = "Height", Position = new Vec2(0, VirtualHeight) }
            };

            foreach (var marker in markers)
            {
                var entity = entityManager.CreateEntity($"virtual{marker.Tag}");
                entity.AddComponent(new CTransform(marker.Position));
                entity.AddComponent(new CText(marker.Tag, 24));
            }
        }

        public override void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaTime)
        {
            var entity = entityManager.GetEntityWithTag("entity");
            var cPointer = entity.GetComponent<CPointer>();
            var cText = entity.GetComponent<CText>();
            cText.Text = $"Press: {cPointer.PressPosition.Round(1)} Move: {cPointer.MovePosition.Round(1)}  Release: {cPointer.ReleasePosition.Round(1)}";
        }
    }

    class CPointer : Component
    {
        public Vec2 PressPosition { get; set; }
        public Vec2 MovePosition { get; set; }
        public Vec2 ReleasePosition { get; set; }
    }
}