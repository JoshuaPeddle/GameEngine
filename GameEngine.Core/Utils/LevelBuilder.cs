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

        public EntityBuilder AddTransform(double x, double y, double rotation = 0)
        {
            var transformData = new Dictionary<string, object>
            {
                ["position"] = new { x, y },
                ["rotation"] = rotation
            };

            return AddComponent("CTransform", transformData);
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
    public static class LevelBuilderExamples
    {
        public static LevelFile CreateSimpleLevel()
        {
            return new LevelBuilder("Simple Test Level")
                .SetMetadata("Simple Test Level", "1.0", "A basic level for testing")
                .AddProperty("difficulty", "easy")
                .AddProperty("backgroundMusic", "TestMusic")

                // Add player
                .AddEntity("player", player => player
                    .AddTransform(100, 100)
                    .AddAnimation("PlayerIdle")
                    .AddBoundingBox(50, 80)
                    .AddInput())

                // Add some walls
                .AddEntity("tile", wall => wall
                    .AddTransform(250, 300)
                    .AddAnimation("StoneBlock")
                    .AddBoundingBox(60, 60, blockVision: true, blockMovement: true))

                .AddEntity("tile", wall => wall
                    .AddTransform(310, 300)
                    .AddAnimation("StoneBlock")
                    .AddBoundingBox(60, 60, blockVision: true, blockMovement: true))

                .Build();
        }

        public static LevelFile CreateMazeLevel(int width, int height)
        {
            var builder = new LevelBuilder($"Maze {width}x{height}")
                .SetMetadata($"Generated Maze", "1.0", $"A {width}x{height} randomly generated maze")
                .AddProperty("mazeWidth", width)
                .AddProperty("mazeHeight", height)
                .AddProperty("generatedAt", DateTime.Now.ToString());

            // Add player at start
            builder.AddEntity("player", player => player
                .AddTransform(50, 50)
                .AddAnimation("PlayerIdle")
                .AddBoundingBox(40, 40)
                .AddInput());

            // Generate maze walls (simplified example)
            var random = new Random();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Simple random maze generation
                    if (random.Next(0, 100) < 30) // 30% chance of wall
                    {
                        builder.AddEntity("tile", wall => wall
                            .AddTransform(x * 60, y * 60)
                            .AddAnimation("StoneBlock")
                            .AddBoundingBox(60, 60, blockVision: true, blockMovement: true));
                    }
                }
            }

            return builder.Build();
        }

        public static void CloneLevel(string inputPath, string outputPath, string newName)
        {
            // Load existing level
            var levelFile = LevelFile.LoadFromFile(inputPath);

            // Update metadata for the clone
            levelFile.Metadata.Name = newName;
            levelFile.Metadata.Properties["clonedAt"] = DateTime.Now.ToString();
            levelFile.Metadata.Properties["clonedFrom"] = Path.GetFileName(inputPath);

            // Save as new level
            levelFile.SaveToFile(outputPath);
        }

        public static void ValidateAllLevelsInDirectory(string directoryPath)
        {
            var levelFiles = Directory.GetFiles(directoryPath, "*.json");

            foreach (var filePath in levelFiles)
            {
                try
                {
                    var levelFile = LevelFile.LoadFromFile(filePath);
                    levelFile.Validate();
                    Console.WriteLine($"✓ {Path.GetFileName(filePath)} - Valid");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {Path.GetFileName(filePath)} - Error: {ex.Message}");
                }
            }
        }
    }
}