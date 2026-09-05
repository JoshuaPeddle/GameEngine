using System;
using System.Collections.Generic;
using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Core.Utils;

namespace GameEngine.Editor.Magic
{
    /// <summary>
    /// Previews the level document the author is editing, rather than a compiled scene. Every
    /// entity it creates comes from a document entry, and it remembers which, so a click in the
    /// viewport selects the row that produced it. An entity the running simulation spawns later
    /// has no document identity and maps to nothing.
    /// </summary>
    public sealed class LevelDocumentScene : Scene
    {
        private readonly LevelFile _level;
        private readonly IReadOnlyList<Guid> _documentIds;
        private readonly Func<Assets> _assets;
        private readonly Dictionary<int, Guid> _runtimeToDocument = new();

        public LevelDocumentScene(LevelFile level, IReadOnlyList<Guid> documentIds, Func<Assets> assets)
        {
            ArgumentNullException.ThrowIfNull(level);
            ArgumentNullException.ThrowIfNull(documentIds);
            ArgumentNullException.ThrowIfNull(assets);

            _level = level;
            _documentIds = documentIds;
            _assets = assets;
        }

        public override int VirtualWidth => 1600;

        public override int VirtualHeight => 1600;

        public override void Initialize(
            EntityManager entityManager,
            InputManager inputManager,
            AudioSystem? audioPlayer,
            Action<Scene?> resetScene)
        {
            _runtimeToDocument.Clear();

            var loader = LevelManager.CreateLoader(_assets());
            loader.LoadLevel(_level, entityManager, inputManager, audioPlayer, (index, entity) =>
            {
                if (index < _documentIds.Count)
                    _runtimeToDocument[entity.Id] = _documentIds[index];
            });
        }

        public Guid? DocumentIdOf(int runtimeEntityId) =>
            _runtimeToDocument.TryGetValue(runtimeEntityId, out var documentId) ? documentId : null;
    }
}
