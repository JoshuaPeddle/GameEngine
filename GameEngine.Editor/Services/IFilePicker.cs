using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public interface IFilePickerService
    {
        Task<string?> PromptForImagePath();
        Task<string?> PromptForProjectPath();
    }
}
