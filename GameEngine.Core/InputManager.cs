using System.Collections.Concurrent;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core
{
    public class InputManager
    {
        public ActionMapper ActionMapper { get; }
        public Vec2 RealResolution { get => _realResolution;  set => _realResolution = value; }
        public Vec2 VirtualResolution { get; set; }

        private ConcurrentDictionary<GeKeys, string> actionMap;
        private ConcurrentDictionary<string, bool> actionStates;
        private ConcurrentDictionary<string, List<Action<bool>>> actionBindings;
        private ConcurrentDictionary<PointerEventType, List<Action<PointerEvent>>> pointerActionBindings;

        private Vec2 _realResolution;

        public InputManager()
        {
            ActionMapper = new ActionMapper(this);
            actionMap = [];
            actionStates = [];
            actionBindings = [];
            pointerActionBindings = [];
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
            if (actionStates.ContainsKey(actionName))
            {
                if (!actionBindings.TryGetValue(actionName, out List<Action<bool>>? value))
                {
                    value = new List<Action<bool>>();
                    actionBindings[actionName] = value;
                }

                value.Add(onAction);
            }
        }

        public void BindPointerAction(PointerEventType actionName, Action<PointerEvent> onAction)
        {
            if (!pointerActionBindings.TryGetValue(actionName, out List<Action<PointerEvent>>? value))
            {
                value = new List<Action<PointerEvent>>();
                pointerActionBindings[actionName] = value;
            }

            value.Add(onAction);
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
            bool resolutionsValid = _realResolution.X > 0 && _realResolution.Y > 0 &&
                                       VirtualResolution.X > 0 && VirtualResolution.Y > 0;

            if (!resolutionsValid) { throw new InvalidOperationException("Real and Virtual dimensions must be set to handle pointer events."); }

            if (pointerActionBindings.TryGetValue(eventType, out List<Action<PointerEvent>>? actions))
            {
                foreach (var action in actions)
                {
                    double scaleX = _realResolution.X / VirtualResolution.X;
                    double scaleY = _realResolution.Y / VirtualResolution.Y;

                    double finalScale = Math.Min(scaleX, scaleY);

                    double scaledWidth = VirtualResolution.X * finalScale;
                    double scaledHeight = VirtualResolution.Y * finalScale;
                    double leftoverX = (_realResolution.X - scaledWidth) / 2;
                    double leftoverY = (_realResolution.Y - scaledHeight) / 2;

                    double adjustedX = pointerEvent.Position.X - leftoverX;
                    double adjustedY = pointerEvent.Position.Y - leftoverY;

                    if (adjustedX >= 0 && adjustedX <= scaledWidth &&
                        adjustedY >= 0 && adjustedY <= scaledHeight)
                    {
                        double virtualX = adjustedX / finalScale;
                        double virtualY = adjustedY / finalScale;

                        var remappedEvent = new PointerEvent(new Vec2(virtualX, virtualY));
                        action(remappedEvent);
                    }
                }
            }
        }

        public void DoActions()
        {
            foreach (var actions in actionBindings)
            {
                foreach (var action in actions.Value)
                {
                    action(actionStates[actions.Key]);
                }
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
