using System.Collections.Generic;
using System.Linq;
using GameEngine.Core;
using ReactiveUI;

namespace GameEngine.Editor.ViewModels
{
    public class EntityInspectorViewModel : ViewModelBase
    {
        private Entity? _selectedEntity;
        public Entity? SelectedEntity
        {
            get => _selectedEntity;
            private set => this.RaiseAndSetIfChanged(ref _selectedEntity, value);
        }

        public bool HasEntitySelection => SelectedEntity != null;
        public int? SelectedEntityId => SelectedEntity?.Id;
        public string? SelectedEntityTag => SelectedEntity?.Tag;
        public bool? SelectedEntityActive => SelectedEntity?.Active;

        public IReadOnlyList<string>? SelectedEntityComponents =>
            SelectedEntity == null
                ? null
                : SelectedEntity.Components.Keys
                    .Select(t => t.Name)
                    .OrderBy(n => n)
                    .ToList();

        public void Select(Entity? entity)
        {
            SelectedEntity = entity;
            this.RaisePropertyChanged(nameof(HasEntitySelection));
            this.RaisePropertyChanged(nameof(SelectedEntityId));
            this.RaisePropertyChanged(nameof(SelectedEntityTag));
            this.RaisePropertyChanged(nameof(SelectedEntityActive));
            this.RaisePropertyChanged(nameof(SelectedEntityComponents));
        }
    }
}
