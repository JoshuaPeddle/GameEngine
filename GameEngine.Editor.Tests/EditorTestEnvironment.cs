using GameEngine.Editor.Services;
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Primitives.Concurrency;

namespace GameEngine.Editor.Tests;

[SetUpFixture]
public class EditorTestEnvironment
{
    [OneTimeSetUp]
    public void RunReactiveCommandsInline()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithCoreServices()
            .BuildApp();

        RxSchedulers.MainThreadScheduler = ImmediateSequencer.Instance;
        RxSchedulers.TaskpoolScheduler = ImmediateSequencer.Instance;
    }
}

public sealed class StubFilePicker : IFilePickerService
{
    public string? ImagePath { get; set; }

    public string? ProjectPath { get; set; }

    public string? FolderPath { get; set; }

    public Task<string?> PromptForImagePath() => Task.FromResult(ImagePath);

    public Task<string?> PromptForProjectPath() => Task.FromResult(ProjectPath);

    public Task<string?> PromptForFolderPath() => Task.FromResult(FolderPath);
}
