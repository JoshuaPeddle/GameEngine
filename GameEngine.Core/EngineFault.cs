namespace GameEngine.Core
{
    /// <summary>
    /// What stopped an engine: which scene it was running, which operation failed, which
    /// system was executing if the failure came from one, and the exception itself. A faulted
    /// engine stops simulating and keeps its last snapshot; loading another scene clears it.
    /// </summary>
    public sealed record EngineFault(
        string Operation,
        string? SceneName,
        string? SystemName,
        Exception Exception)
    {
        public override string ToString()
        {
            var where = SystemName == null ? Operation : $"{Operation} in {SystemName}";
            var scene = SceneName == null ? "no scene" : $"scene {SceneName}";
            return $"{where} ({scene}): {Exception.GetType().Name}: {Exception.Message}";
        }
    }
}
