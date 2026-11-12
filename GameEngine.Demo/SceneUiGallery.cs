using System;
using System.Threading;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo;

public sealed record UiGalleryState(double Salvage = 12840, int Rank = 3, double ResearchRemaining = 0);

public sealed class SceneUiGallery : Scene
{
    private UiGalleryState state = new();
    private double publishElapsed;
    public UiGalleryState State => Volatile.Read(ref state);
    public event Action? Changed;
    public override int VirtualWidth => 360;
    public override int VirtualHeight => 800;

    public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene?> resetScene)
    {
        var title = entityManager.CreateEntity("ui-gallery");
        title.AddComponent(new CTransform(new Vec2(180, 400)));
        title.AddComponent(new CText("Aurora UI gallery", 24));
    }
    public void Upgrade()
    {
        var current = State;
        if (current.Salvage < 2400) return;
        Publish(current with { Salvage = current.Salvage - 2400, Rank = current.Rank + 1 });
    }
    public void BeginResearch() => Publish(State with { ResearchRemaining = 30 });
    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        var current = State;
        if (current.ResearchRemaining <= 0) return;
        current = current with { ResearchRemaining = Math.Max(0, current.ResearchRemaining - deltaSeconds) };
        Volatile.Write(ref state, current);
        publishElapsed += deltaSeconds;
        if (publishElapsed < .1 && current.ResearchRemaining > 0) return;
        publishElapsed = 0; Changed?.Invoke();
    }
    private void Publish(UiGalleryState value) { Volatile.Write(ref state, value); Changed?.Invoke(); }
}
