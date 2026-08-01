using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameEngine.Core.Utils;

namespace GameEngine.Core.Utils
{
    // Helper class for building levels programmatically
    public class LevelBuilder
    {
        private readonly LevelFile levelFile;

        public LevelBuilder(string levelName = "New Level")
        {
            levelFile = new LevelFile();
            levelFile.Metadata.Name = levelName;
        }

        public LevelBuilder SetMetadata(string name, string version = "1.0", string description = "")
        {
            levelFile.Metadata.Name = name;
            levelFile.Metadata.Version = version;
            levelFile.Metadata.Description = description;
            return this;
        }

        public LevelBuilder AddProperty(string key, object value)
        {
            levelFile.Metadata.Properties[key] = value;
            return this;
        }

        public LevelBuilder AddEntity(string tag, Action<EntityBuilder> configureEntity)
        {
            var entityBuilder = new EntityBuilder(tag);
            configureEntity(entityBuilder);
            levelFile.Entities.Add(entityBuilder.Build());
            return this;
        }

        public LevelFile Build()
        {
            levelFile.Validate();
            return levelFile;
        }

        public void SaveToFile(string filePath)
        {
            Build().SaveToFile(filePath);
        }
    }

    // Helper class for building entities
    public class EntityBuilder
    {
        private readonly EntityData entityData;

        public EntityBuilder(string tag)
        {
            entityData = new EntityData { Tag = tag };
        }

        public EntityBuilder AddTransform(double x, double y, double rotation = 0, int layer = 0,
            double scaleX = 1, double scaleY = 1)
        {
            var transformData = new Dictionary<string, object>
            {
                ["position"] = new { x, y },
                ["rotation"] = rotation,
                ["layer"] = layer,
                ["scale"] = new { x = scaleX, y = scaleY }
            };

            return AddComponent("CTransform", transformData);
        }

        public EntityBuilder AddText(string text, int size = 24)
        {
            return AddComponent("CText", new Dictionary<string, object>
            {
                ["text"] = text,
                ["size"] = size
            });
        }

        public EntityBuilder AddCamera(double x = 0, double y = 0, double zoom = 1.0)
        {
            return AddComponent("CCamera", new Dictionary<string, object>
            {
                ["position"] = new { x, y },
                ["zoom"] = zoom
            });
        }

        public EntityBuilder AddGravity(double acceleration = 200)
        {
            return AddComponent("CGravity", new Dictionary<string, object>
            {
                ["acceleration"] = acceleration
            });
        }

        public EntityBuilder AddMovement(double speed = 600, double maxSpeed = 250)
        {
            return AddComponent("CMovement", new Dictionary<string, object>
            {
                ["speed"] = speed,
                ["maxSpeed"] = maxSpeed
            });
        }

        public EntityBuilder AddAnimation(string animationName)
        {
            var animationData = new Dictionary<string, object>
            {
                ["animationName"] = animationName
            };

            return AddComponent("CAnimation", animationData);
        }

        public EntityBuilder AddBoundingBox(double width, double height, bool blockVision = false, bool blockMovement = false)
        {
            var boundingBoxData = new Dictionary<string, object>
            {
                ["size"] = new { x = width, y = height },
                ["blockVision"] = blockVision,
                ["blockMovement"] = blockMovement
            };

            return AddComponent("CBoundingBox", boundingBoxData);
        }

        public EntityBuilder AddInput()
        {
            return AddComponent("CInput", new Dictionary<string, object>());
        }

        public EntityBuilder AddComponent(string componentType, Dictionary<string, object> componentData)
        {
            var jsonElement = JsonSerializer.SerializeToElement(componentData);

            entityData.Components.Add(new ComponentData
            {
                Type = componentType,
                Data = jsonElement
            });

            return this;
        }

        public EntityData Build()
        {
            return entityData;
        }
    }

    // Example usage and utility methods
}
