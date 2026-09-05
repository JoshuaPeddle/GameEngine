using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameEngine.Editor.ViewModels
{
    // Every change to the level document goes through one of these, which is what makes undo,
    // redo and a trustworthy dirty flag possible: the document is never mutated behind the
    // history's back.
    public interface ILevelEdit
    {
        string Description { get; }

        void Apply();

        void Undo();
    }

    public sealed class EditHistory
    {
        private readonly List<ILevelEdit> _done = new();
        private readonly List<ILevelEdit> _undone = new();

        public event Action? Changed;

        public bool CanUndo => _done.Count > 0;

        public bool CanRedo => _undone.Count > 0;

        public string? NextUndo => CanUndo ? _done[^1].Description : null;

        public string? NextRedo => CanRedo ? _undone[^1].Description : null;

        /// <summary>Edits applied since the history was last marked clean.</summary>
        public int Depth { get; private set; }

        public bool IsClean => Depth == 0;

        public bool IsReplaying { get; private set; }

        public void Do(ILevelEdit edit)
        {
            ArgumentNullException.ThrowIfNull(edit);

            Replay(edit.Apply);
            _done.Add(edit);
            _undone.Clear();
            Depth++;
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (!CanUndo)
                return;

            var edit = _done[^1];
            _done.RemoveAt(_done.Count - 1);
            Replay(edit.Undo);
            _undone.Add(edit);
            Depth--;
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo)
                return;

            var edit = _undone[^1];
            _undone.RemoveAt(_undone.Count - 1);
            Replay(edit.Apply);
            _done.Add(edit);
            Depth++;
            Changed?.Invoke();
        }

        /// <summary>Marks the document as matching what is on disk, keeping the undo stack.</summary>
        public void MarkClean()
        {
            Depth = 0;
            Changed?.Invoke();
        }

        public void Clear()
        {
            _done.Clear();
            _undone.Clear();
            Depth = 0;
            Changed?.Invoke();
        }

        // While an edit is applying, the view models it touches raise their usual change
        // notifications. Recording those would push the undo we are performing back onto the
        // stack, so the document checks this before it records anything.
        private void Replay(Action action)
        {
            IsReplaying = true;
            try
            {
                action();
            }
            finally
            {
                IsReplaying = false;
            }
        }
    }

    public sealed class AddEntityEdit(
        ObservableCollection<LevelEntityViewModel> entities, LevelEntityViewModel entity, int index)
        : ILevelEdit
    {
        public string Description => $"add entity '{entity.Tag}'";

        public void Apply() => entities.Insert(Math.Clamp(index, 0, entities.Count), entity);

        public void Undo() => entities.Remove(entity);
    }

    public sealed class RemoveEntityEdit(
        ObservableCollection<LevelEntityViewModel> entities, LevelEntityViewModel entity, int index)
        : ILevelEdit
    {
        public string Description => $"remove entity '{entity.Tag}'";

        public void Apply() => entities.Remove(entity);

        public void Undo() => entities.Insert(Math.Clamp(index, 0, entities.Count), entity);
    }

    public sealed class RenameEntityEdit(LevelEntityViewModel entity, string before, string after)
        : ILevelEdit
    {
        public string Description => $"rename '{before}' to '{after}'";

        public void Apply() => entity.Tag = after;

        public void Undo() => entity.Tag = before;
    }

    public sealed class AddComponentEdit(
        LevelEntityViewModel entity, LevelComponentViewModel component, int index)
        : ILevelEdit
    {
        public string Description => $"add {component.Type} to '{entity.Tag}'";

        public void Apply() =>
            entity.Components.Insert(Math.Clamp(index, 0, entity.Components.Count), component);

        public void Undo() => entity.Components.Remove(component);
    }

    public sealed class RemoveComponentEdit(
        LevelEntityViewModel entity, LevelComponentViewModel component, int index)
        : ILevelEdit
    {
        public string Description => $"remove {component.Type} from '{entity.Tag}'";

        public void Apply() => entity.Components.Remove(component);

        public void Undo() =>
            entity.Components.Insert(Math.Clamp(index, 0, entity.Components.Count), component);
    }

    public sealed class EditComponentEdit(
        LevelComponentViewModel component, ComponentState before, ComponentState after)
        : ILevelEdit
    {
        public ComponentState Before => before;

        public ComponentState After => after;

        public LevelComponentViewModel Component => component;

        public string Description => $"edit {after.Type}";

        public void Apply() => component.Restore(after);

        public void Undo() => component.Restore(before);
    }
}
