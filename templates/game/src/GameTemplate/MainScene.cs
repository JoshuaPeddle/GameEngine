using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Core.Utils;

namespace GameTemplate;

// Move the blue square with WASD or the arrow keys (or swipe on a touch screen) and
// collect the gold pickups.
// The walls and pickups come from levels/level1.json; the player is built here in code.
public class MainScene : Scene
{
    public override int VirtualWidth => 960;
    public override int VirtualHeight => 540;

    private Assets? assets;

    public int Collected { get; private set; }

    public override void Initialize(
        EntityManager entities,
        InputManager input,
        AudioSystem? audio,
        Action<Scene> resetScene)
    {
        assets = LoadAssets(AssetManifest.DefaultFileName);

        input.AddAction(GeKeys.W, "Up");
        input.BindGestureAction(PointerGesture.Up, "Up");
        input.AddAction(GeKeys.Up, "Up");
        input.AddAction(GeKeys.S, "Down");
        input.BindGestureAction(PointerGesture.Down, "Down");
        input.AddAction(GeKeys.Down, "Down");
        input.AddAction(GeKeys.A, "Left");
        input.BindGestureAction(PointerGesture.Left, "Left");
        input.AddAction(GeKeys.Left, "Left");
        input.AddAction(GeKeys.D, "Right");
        input.BindGestureAction(PointerGesture.Right, "Right");
        input.AddAction(GeKeys.Right, "Right");

        LevelManager.LoadLevelIntoScene("levels/level1.json", entities, input, audio, assets);

        var player = entities.CreateEntity("player");
        player.AddComponent(new CTransform(new Vec2(VirtualWidth / 2.0, VirtualHeight / 2.0)));
        player.AddComponent(new CAnimation(assets.GetAnimation("Player")));
        player.AddComponent(new CBoundingBox(new Vec2(32, 32), blockVision: false, blockMove: false));
        player.AddComponent(new CMovement(1200, 260));
        player.AddComponent<CInput>();

        input.ActionMapper.MapActionToComponent<CInput>("Up", player, (c, held) => c.Up = held, true);
        input.ActionMapper.MapActionToComponent<CInput>("Down", player, (c, held) => c.Down = held, true);
        input.ActionMapper.MapActionToComponent<CInput>("Left", player, (c, held) => c.Left = held, true);
        input.ActionMapper.MapActionToComponent<CInput>("Right", player, (c, held) => c.Right = held, true);

        var score = entities.CreateEntity("score");
        score.AddComponent(new CTransform(new Vec2(VirtualWidth / 2.0, 40)));
        score.AddComponent(new CText("Collect the pickups", 28));
    }

    public override void Update(EntityManager entities, SystemContainer systems, double deltaSeconds)
    {
        foreach (var collision in systems.Get<PhysicsSystem>().CollisionEvents)
        {
            if (!InvolvesThePlayer(collision))
                continue;

            var pickup = collision.A.Tag == "pickup" ? collision.A
                : collision.B.Tag == "pickup" ? collision.B
                : null;

            if (pickup == null || !pickup.Active)
                continue;

            pickup.Active = false;
            Collected++;

            if (entities.GetEntityWithTag("score") is { } score
                && score.TryGetComponent<CText>(out var text))
            {
                text.Text = $"Collected {Collected}";
            }
        }
    }

    private static bool InvolvesThePlayer(CollisionEvent collision) =>
        collision.A.Tag == "player" || collision.B.Tag == "player";
}
