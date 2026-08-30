using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameTemplate.Tests;

public class MainSceneTests
{
    [Test]
    public void TheSceneLoadsAndSurvivesASecondOfPlay()
    {
        var harness = SceneHarness.Load(new MainScene());
        harness.Run(60);

        Assert.That(harness.Entities.GetEntities(), Is.Not.Empty);
    }

    [Test]
    public void TheLevelProvidesWallsAndPickups()
    {
        var harness = SceneHarness.Load(new MainScene());
        harness.Run(1);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Entities.GetEntitiesWithTag("wall"), Is.Not.Empty, "levels/level1.json declares walls");
            Assert.That(harness.Entities.GetEntitiesWithTag("pickup"), Is.Not.Empty, "levels/level1.json declares pickups");
        });
    }

    [TestCase(GeKeys.D)]
    [TestCase(GeKeys.Right)]
    public void HoldingRightMovesThePlayerRight(GeKeys key)
    {
        var harness = SceneHarness.Load(new MainScene());
        harness.Run(5);

        var player = harness.Require("player").GetComponent<CTransform>();
        var startX = player.Position.X;

        harness.PressAndRelease(key, framesHeld: 30);

        Assert.That(player.Position.X, Is.GreaterThan(startX));
    }

    [Test]
    public void ReleasingOneOfTwoKeysForTheSameDirectionKeepsMoving()
    {
        var harness = SceneHarness.Load(new MainScene());
        harness.Run(5);

        var player = harness.Require("player").GetComponent<CTransform>();
        var input = harness.Engine.Systems.Get<InputSystem>();

        input.KeyDown(GeKeys.Right);
        input.KeyDown(GeKeys.D);
        input.KeyUp(GeKeys.D);
        harness.Run(5);

        var beforeCoasting = player.Position.X;
        harness.Run(20);

        Assert.That(player.Position.X, Is.GreaterThan(beforeCoasting),
            "the arrow key is still held, so the player should still be accelerating");
    }

    [Test]
    public void WallsAreSolid()
    {
        var harness = SceneHarness.Load(new MainScene());
        harness.Run(5);

        var player = harness.Require("player").GetComponent<CTransform>();
        harness.PressAndRelease(GeKeys.W, framesHeld: 600);

        Assert.That(player.Position.Y, Is.GreaterThan(0), "the top wall should stop the player");
    }
}
