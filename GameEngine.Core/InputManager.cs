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
        private readonly ConcurrentDictionary<(GeKeys Key, long SourceId), byte> heldKeys = [];
        private readonly ConcurrentDictionary<string, bool> actionStates = [];
        private readonly ConcurrentDictionary<string, IReadOnlyList<Action<bool>>> actionBindings = [];
        private readonly ConcurrentDictionary<PointerEventType, IReadOnlyList<Action<PointerEvent>>> pointerActionBindings = [];
        private readonly Queue<(PointerEventType Type, PointerEvent Event)> queuedPointerEvents = new();
        private readonly object _pointerLock = new();
        private readonly Dictionary<long, PointerEvent> _incomingContacts = new();
        private readonly Dictionary<long, PointerEvent> _contacts = new();
        public IReadOnlyDictionary<long, PointerEvent> Contacts => _contacts;
        public bool ReleaseGesturesEnabled { get; set; }

        private Vec2 _realResolution;

        public InputManager()
        {
            ActionMapper = new ActionMapper(this);
        }

        public void Reset()
        {
            actionMap.Clear();
            heldKeys.Clear();
            actionStates.Clear();
            actionBindings.Clear();
            pointerActionBindings.Clear();

            lock (_pointerLock)
            {
                queuedPointerEvents.Clear();
                _incomingContacts.Clear();
            }
            _contacts.Clear();
            ReleaseGesturesEnabled = false;
        }

        public void AddAction(GeKeys key, string actionName)
        {
            if (!actionMap.ContainsKey(key))
            {
                actionMap[key] = actionName;
                actionStates[actionName] = IsAnyKeyHeldFor(actionName);
            }
        }

        // Removes one key's mapping and nothing else. An action several keys share — D and
        // Right, say — keeps its remaining keys and every callback bound to it, and its state
        // is recomputed from the keys that are still down.
        public void RemoveAction(GeKeys key)
        {
            if (!actionMap.TryRemove(key, out string? actionName))
                return;

            foreach (var held in heldKeys.Keys.Where(held => held.Key == key)) heldKeys.TryRemove(held, out _);
            actionStates[actionName] = IsAnyKeyHeldFor(actionName);
        }

        // Removes the action itself: every key mapped to it, its state, and its callbacks.
        public void RemoveAction(string actionName)
        {
            foreach (var mapping in actionMap)
            {
                if (mapping.Value != actionName)
                    continue;

                actionMap.TryRemove(mapping.Key, out _);
                foreach (var held in heldKeys.Keys.Where(held => held.Key == mapping.Key)) heldKeys.TryRemove(held, out _);
            }

            actionStates.TryRemove(actionName, out _);
            actionBindings.TryRemove(actionName, out _);
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

        public void HandleKeyPress(GeKeys key) => HandleKeyPress(key, 0);

        public void HandleKeyPress(GeKeys key, long sourceId)
        {
            if (actionMap.TryGetValue(key, out string? actionName))
            {
                heldKeys[(key, sourceId)] = 0;
                actionStates[actionName] = true;
            }
        }

        public void HandleKeyRelease(GeKeys key) => HandleKeyRelease(key, 0);

        public void HandleKeyRelease(GeKeys key, long sourceId)
        {
            if (actionMap.TryGetValue(key, out string? actionName))
            {
                heldKeys.TryRemove((key, sourceId), out _);
                actionStates[actionName] = IsAnyKeyHeldFor(actionName);
            }
        }

        // Several keys can share one action — WASD and the arrows, say — so releasing one of
        // them only ends the action if none of its other keys are still down.
        private bool IsAnyKeyHeldFor(string actionName)
        {
            foreach (var mapping in actionMap)
            {
                if (mapping.Value == actionName && heldKeys.Keys.Any(held => held.Key == mapping.Key))
                    return true;
            }

            return false;
        }

        public void ReleaseAllKeys()
        {
            heldKeys.Clear();
            foreach (var action in actionStates.Keys) actionStates[action] = false;
        }

        public void HandlePointerEvent(PointerEventType eventType, PointerEvent pointerEvent)
        {
            lock (_pointerLock)
            {
                if (eventType == PointerEventType.Move && queuedPointerEvents.Count >= MaxQueuedPointerEvents)
                    return;
                var copy = new PointerEvent(pointerEvent.Position, pointerEvent.ContactId, pointerEvent.IsCaptured);
                if (eventType == PointerEventType.Press || (eventType == PointerEventType.Move && _incomingContacts.ContainsKey(copy.ContactId)))
                    _incomingContacts[copy.ContactId] = copy;
                if (eventType is PointerEventType.Release or PointerEventType.Cancel)
                    _incomingContacts.Remove(copy.ContactId);
                queuedPointerEvents.Enqueue((eventType, copy));
            }
        }

        public void CancelAllContacts()
        {
            lock (_pointerLock)
            {
                foreach (var contact in _incomingContacts.Values)
                    queuedPointerEvents.Enqueue((PointerEventType.Cancel, contact));
                _incomingContacts.Clear();
            }
        }

        public void DispatchPointerEvents()
        {
            while (true)
            {
                (PointerEventType Type, PointerEvent Event) queued;
                lock (_pointerLock)
                {
                    if (!queuedPointerEvents.TryDequeue(out queued)) break;
                }
                var source = queued.Event;
                var transform = ViewportTransform.Create(_realResolution, VirtualResolution, ScalingStrategy);
                bool mapped = transform.TryToVirtual(source.Position, out var position);
                bool terminal = queued.Type is PointerEventType.Release or PointerEventType.Cancel;
                bool active = _contacts.TryGetValue(source.ContactId, out var previous);
                if (!mapped)
                {
                    if (!terminal && (!active || !source.IsCaptured)) continue;
                    position = transform.IsValid
                        ? new Vec2((source.Position.X - transform.OffsetX) / transform.ScaleX, (source.Position.Y - transform.OffsetY) / transform.ScaleY)
                        : previous?.Position ?? Vec2.Zero;
                }
                var remapped = new PointerEvent(position, source.ContactId, source.IsCaptured);
                if (queued.Type == PointerEventType.Press || (queued.Type == PointerEventType.Move && active))
                    _contacts[source.ContactId] = remapped;
                if (terminal) _contacts.Remove(source.ContactId);
                if (!pointerActionBindings.TryGetValue(queued.Type, out var actions)) continue;
                for (int i = 0; i < actions.Count; i++) actions[i](remapped);
            }
        }

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

    // A binding names an entity and a component type, not one component instance: replacing
    // the component leaves the mapping driving the replacement, and removing it leaves the
    // mapping inert rather than writing to something the entity no longer holds. The entity
    // must already have the component when the mapping is made, so a wrong type is still a
    // mistake reported at the call site.
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
            _ = entity.GetComponent<T>();
            bool previouslyActive = false;

            inputManager.BindAction(actionName, isActive =>
            {
                if (!entity.TryGetComponent<T>(out var component))
                    return;

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
            _ = entity.GetComponent<T>();

            inputManager.BindPointerAction(eventType, pointerEvent =>
            {
                if (entity.TryGetComponent<T>(out var component))
                    updateAction(component, pointerEvent);
            });
        }

    }
}
