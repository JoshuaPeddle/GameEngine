using ReactiveUI;

namespace GameEngine.Editor.ViewModels
{
    public class EditorStatus : ReactiveObject
    {
        private string _message = "Idle";
        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }
    }
}
