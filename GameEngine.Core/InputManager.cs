namespace GameEngine.Core
{
    public class InputManager
    {
        private readonly Dictionary<Keys, string> actionMap = new();
        private readonly Dictionary<string, bool> actionStates = new();
        private readonly Dictionary<string, Action<bool>> actionBindings = new(); // Maps actions to update functions

        public void AddAction(Keys key, string actionName)
        {
            if (!actionMap.ContainsKey(key))
            {
                actionMap[key] = actionName;
                actionStates[actionName] = false;
            }
        }

        public void RemoveAction(Keys key)
        {
            if (actionMap.TryGetValue(key, out string actionName))
            {
                actionMap.Remove(key);
                actionStates.Remove(actionName);
                actionBindings.Remove(actionName);
            }
        }

        public void BindAction(string actionName, Action<bool> onAction)
        {
            if (actionStates.ContainsKey(actionName))
            {
                actionBindings[actionName] = onAction;
            }
        }

        public void HandleKeyPress(Keys key)
        {
            if (actionMap.TryGetValue(key, out string actionName))
            {
                actionStates[actionName] = true;
                actionBindings.TryGetValue(actionName, out var action);
                action?.Invoke(true);
            }
        }

        public void HandleKeyRelease(Keys key)
        {
            if (actionMap.TryGetValue(key, out string actionName))
            {
                actionStates[actionName] = false;
                actionBindings.TryGetValue(actionName, out var action);
                action?.Invoke(false);
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

        public void MapActionToComponent<T>(string actionName, Entity entity, Action<T, bool> updateAction) where T : Component
        {
            var component = entity.GetComponent<T>();
            if (component != null)
            {
                inputManager.BindAction(actionName, isActive => updateAction(component, isActive));
            }
        }
    }
}
