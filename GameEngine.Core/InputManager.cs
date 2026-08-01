using System.Collections.Concurrent;
using GameEngine.Core.Systems;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core
{
    public class InputManager
    {
        public ActionMapper ActionMapper { get; }
        public Vec2 RealResolution { get => _realResolution;  set => _realResolution = value; }
        public Vec2 VirtualResolution { get; set; }
        public ScalingStrategy ScalingStrategy { get; set; } = ScalingStrategy.Letterbox;

        private const int MaxQueuedPointerEvents = 256;

        private readonly ConcurrentDictionary<GeKeys, string> actionMap = [];
        private readonly ConcurrentDictionary<string, bool> actionStates = [];
        private readonly ConcurrentDictionary<string, IReadOnlyList<Action<bool>>> actionBindings = [];
        private readonly ConcurrentDictionary<PointerEventType, IReadOnlyList<Action<PointerEvent>>> pointerActionBindings = [];
        private readonly ConcurrentQueue<(PointerEventType Type, Vec2 RealPosition)> queuedPointerEvents = new();

        private Vec2 _realResolution;

        public InputManager()
        {
            ActionMapper = new ActionMapper(this);
        }

        public void AddAction(GeKeys key, string actionName)
        {
            if (!actionMap.ContainsKey(key))
            {
                actionMap[key] = actionName;
                actionStates[actionName] = false;
            }
        }

        public void RemoveAction(GeKeys key)
        {
            if (actionMap.TryGetValue(key, out string? actionName))
            {
                actionMap.Remove(key, out _);
                actionStates.TryRemove(actionName, out _);
                actionBindings.TryRemove(actionName, out _);
            }
        }

        public void BindAction(string actionName, Action<bool> onAction)
        {
            actionBindings.AddOrUpdate(actionName,
                _ => new[] { onAction },
                (_, existing) => AppendBinding(existing, onAction));
        }

        public void BindPointerAction(PointerEventType eventType, Action<PointerEvent> onAction)
        {
            pointerActionBindings.AddOrUpdate(eventType,
                _ => new[] { onAction },
                (_, existing) => AppendBinding(existing, onAction));
        }

        private static IReadOnlyList<T> AppendBinding<T>(IReadOnlyList<T> existing, T addition)
        {
            var extended = new T[existing.Count + 1];
            for (int i = 0; i < existing.Count; i++)
                extended[i] = existing[i];
            extended[existing.Count] = addition;
            return extended;
        }

        public void HandleKeyPress(GeKeys key)
        {
            if (actionMap.TryGetValue(key, out string? actionName))
            {
                actionStates[actionName] = true;
            }
        }

        public void HandleKeyRelease(GeKeys key)
        {
            if (actionMap.TryGetValue(key, out string? actionName))
            {
                actionStates[actionName] = false;
            }
        }

        public void HandlePointerEvent(PointerEventType eventType, PointerEvent pointerEvent)
        {
            if (queuedPointerEvents.Count >= MaxQueuedPointerEvents)
                return;

            queuedPointerEvents.Enqueue((eventType, pointerEvent.Position));
        }

        public void DispatchPointerEvents()
        {
            while (queuedPointerEvents.TryDequeue(out var queued))
            {
                if (!TryMapToVirtual(queued.RealPosition, out Vec2 virtualPosition))
                    continue;

                if (!pointerActionBindings.TryGetValue(queued.Type, out var actions))
                    continue;

                var remappedEvent = new PointerEvent(virtualPosition);
                for (int i = 0; i < actions.Count; i++)
                    actions[i](remappedEvent);
            }
        }

        private bool TryMapToVirtual(Vec2 realPosition, out Vec2 virtualPosition) =>
            ViewportTransform
                .Create(_realResolution, VirtualResolution, ScalingStrategy)
                .TryToVirtual(realPosition, out virtualPosition);

        public void DoActions()
        {
            foreach (var binding in actionBindings)
            {
                bool isActive = actionStates.TryGetValue(binding.Key, out bool state) && state;
                var actions = binding.Value;

                for (int i = 0; i < actions.Count; i++)
                    actions[i](isActive);
            }
        }

        public bool IsActionActive(string actionName) =>
            actionStates.TryGetValue(actionName, out bool isActive) && isActive;
    }

    public class ActionMapper
    {
        private readonly InputManager inputManager;

        public ActionMapper(InputManager inputManager)
        {
            this.inputManager = inputManager;
        }

        public void MapActionToComponent<T>(
            string actionName,
            Entity entity,
            Action<T, bool> updateAction,
            bool oneShot = false
        ) where T : Component
        {
            var component = entity.GetComponent<T>();
            bool previouslyActive = false;

            inputManager.BindAction(actionName, isActive =>
            {
                if (!oneShot)
                {
                    updateAction(component, isActive);
                }
                else
                {
                    if (!previouslyActive && isActive)
                    {
                        updateAction(component, true);
                    }
                    else if (previouslyActive && !isActive)
                    {
                        updateAction(component, false);
                    }
                }

                previouslyActive = isActive;
            });
        }

        public void MapPointerActionToComponent<T>(
             PointerEventType eventType,
             Entity entity,
             Action<T, PointerEvent> updateAction
        ) where T : Component
        {
            var component = entity.GetComponent<T>();

            inputManager.BindPointerAction(eventType, pointerEvent =>
            {
                updateAction(component, pointerEvent);
            });
        }

    }
}
