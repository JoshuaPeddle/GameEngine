using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameEngine.Editor.Magic
{
    // Simple façade you can bind to in UI.
    public sealed class SceneSelectionService : IAsyncDisposable
    {
        private readonly SceneProjectCompiler _compiler;
        private IReadOnlyList<SceneInfo> _scenes = Array.Empty<SceneInfo>();

        public SceneSelectionService(string projectPath)
        {
            _compiler = new SceneProjectCompiler(projectPath);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await _compiler.InitializeAsync(ct);
            _scenes = await _compiler.DiscoverScenesAsync(ct);
        }

        public IReadOnlyList<SceneInfo> GetScenes() => _scenes;

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            _scenes = await _compiler.DiscoverScenesAsync(ct);
        }

        public async Task<SceneCompilationResult> CompileAllAsync(CancellationToken ct = default)
            => await _compiler.CompileAsync(ct);

        public async Task<SceneCompilationResult> UpdateSceneSourceAsync(SceneInfo scene, string newSource, CancellationToken ct = default)
            => await _compiler.UpdateSceneAsync(scene, newSource, ct);

        public async ValueTask DisposeAsync() => await _compiler.DisposeAsync();
    }
}