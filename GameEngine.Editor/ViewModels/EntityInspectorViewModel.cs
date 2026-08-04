using System.Collections.Generic;
using GameEngine.Core;
using ReactiveUI;

namespace GameEngine.Editor.ViewModels
{
    public class EntityInspectorViewModel : ViewModelBase
    {
        private EntitySnapshot? _selectedEntity;
        public EntitySnapshot? SelectedEntity
        {
            get => _selectedEntity;
            private set => this.RaiseAndSetIfChanged(ref _selectedEntity, value);
        }

        public bool HasEntitySelection => SelectedEntity != null;
        public int? SelectedEntityId => SelectedEntity?.Id;
        public string? SelectedEntityTag => SelectedEntity?.Tag;
        public bool? SelectedEntityActive => SelectedEntity?.Active;

        public IReadOnlyList<string>? SelectedEntityComponents => SelectedEntity?.ComponentTypes;

        public void Select(EntitySnapshot? entity)
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
