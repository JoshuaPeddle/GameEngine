using System.Collections.Concurrent;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core
{
    public class InputManager
    {
        public ActionMapper ActionMapper { get; }

        private ConcurrentDictionary<GeKeys, string> actionMap;
        private ConcurrentDictionary<string, bool> actionStates;
        private ConcurrentDictionary<string, Action<bool>> actionBindings;
        private ConcurrentDictionary<PointerEventType, Action<PointerEvent>> pointerActionBindings;

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
                actionBindings[actionName] = onAction;
            }
        }

        public void BindPointerAction(PointerEventType actionName, Action<PointerEvent> onAction)
        {
            pointerActionBindings[actionName] = onAction;
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
            if (pointerActionBindings.TryGetValue(eventType, out Action<PointerEvent>? action))
            {
                action(pointerEvent);
            }
        }

        public void DoActions()
        {
            foreach (var action in actionBindings)
            {
                action.Value(actionStates[action.Key]);
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
                    // Original "continuous" behavior
                    updateAction(component, isActive);
                }
                else
                {
                    // One-shot logic with press/release transitions
                    if (!previouslyActive && isActive)
                    {
                        // Transition from false -> true (key pressed)
                        updateAction(component, true);
                    }
                    else if (previouslyActive && !isActive)
                    {
                        // Transition from true -> false (key released)
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
