using System;
using GameEngine.Core;

namespace GameEngine.Runner.Avalonia;

public sealed class HostLifecycle
{
    public HostState State { get; private set; } = HostState.Active;
    public event Action<HostState>? Changed;

    public void SetState(HostState state)
    {
        if (State == state) return;
        State = state;
        Changed?.Invoke(state);
    }
}
