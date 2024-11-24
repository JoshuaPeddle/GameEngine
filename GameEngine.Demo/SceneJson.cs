using GameEngine.Core;
using GameEngine.Core.Components;
using System.Text.Json;

namespace GameEngine.Demo
{
    public class SceneJson : Scene
    {
        private readonly Assets assets = new("assets.txt");

        private ComponentFactory? componentFactory;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            componentFactory = new ComponentFactory(assets);

            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

            LoadLevel("levels/level1.json", entityManager, inputManager, actionMapper);
        }


        private void LoadLevel(string levelFilePath, EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            var json = File.ReadAllText(levelFilePath);
            var entitiesData = JsonSerializer.Deserialize<List<JsonElement>>(json);

            foreach (var entityData in entitiesData)
            {
                string tag = entityData.GetProperty("tag").GetString();
                var entity = entityManager.CreateEntity(tag);

                foreach (var componentData in entityData.GetProperty("components").EnumerateArray())
                {
                    string componentType = componentData.GetProperty("type").GetString();
                    Component component = componentFactory!.CreateComponent(componentType, componentData);

                    entity.AddComponent(component);

                    // Handle special cases, like mapping input actions
                    if (component is CInput)
                    {
                        MapInputActions(entity, inputManager, actionMapper);
                    }
                }
            }
        }

        private void MapInputActions(Entity entity, InputManager inputManager, ActionMapper actionMapper)
        {
            actionMapper.MapActionToComponent<CInput>("Up", entity, (input, isActive) => input.Up = isActive);
            actionMapper.MapActionToComponent<CInput>("Down", entity, (input, isActive) => input.Down = isActive);
            actionMapper.MapActionToComponent<CInput>("Left", entity, (input, isActive) => input.Left = isActive);
            actionMapper.MapActionToComponent<CInput>("Right", entity, (input, isActive) => input.Right = isActive);
        }
    }
}
