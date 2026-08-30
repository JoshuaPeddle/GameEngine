using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public sealed record NewProjectResult(bool Success, string Message, string? ProjectPath = null);

    // "New Game" runs the same dotnet new template a user would run from a terminal, so an
    // editor-created project and a hand-created one are the same thing.
    public static class GameProjectCreator
    {
        public const string TemplateShortName = "gameengine-game";
        public const string TemplatePackageId = "GameEngine.Templates";

        public static string InstallHint =>
            $"Install the template first: dotnet new install {TemplatePackageId}";

        public static string? Validate(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Give the game a name.";

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
                return "A game name cannot contain path separators or other invalid file characters.";

            if (!char.IsLetter(name[0]) && name[0] != '_')
                return "A game name has to start with a letter, because it becomes a C# namespace.";

            if (!name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                return "A game name can only contain letters, digits, underscores and dots.";

            return null;
        }

        public static string ProjectPathFor(string parentFolder, string name) =>
            Path.Combine(parentFolder, name, "src", name, name + ".csproj");

        public static async Task<NewProjectResult> CreateAsync(string parentFolder, string name)
        {
            var invalid = Validate(name);
            if (invalid != null)
                return new NewProjectResult(false, invalid);

            if (!Directory.Exists(parentFolder))
                return new NewProjectResult(false, $"'{parentFolder}' does not exist.");

            var target = Path.Combine(parentFolder, name);
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                return new NewProjectResult(false, $"'{target}' already exists and is not empty.");

            var (exitCode, output) = await RunAsync(
                "dotnet", ["new", TemplateShortName, "-n", name, "-o", target], parentFolder);

            if (exitCode != 0)
            {
                var message = output.Contains("No templates found", StringComparison.OrdinalIgnoreCase)
                    ? $"The {TemplateShortName} template is not installed. {InstallHint}"
                    : $"dotnet new failed: {FirstLine(output)}";

                return new NewProjectResult(false, message);
            }

            var projectPath = ProjectPathFor(parentFolder, name);
            return File.Exists(projectPath)
                ? new NewProjectResult(true, $"Created {name}.", projectPath)
                : new NewProjectResult(false, $"The template ran but {projectPath} is missing.");
        }

        private static async Task<(int ExitCode, string Output)> RunAsync(
            string fileName, string[] arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                    return (-1, "could not start the dotnet CLI");

                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return (process.ExitCode, stdout + stderr);
            }
            catch (Exception ex)
            {
                return (-1, ex.Message);
            }
        }

        private static string FirstLine(string text) =>
            text.Split('\n').FirstOrDefault(line => line.Trim().Length > 0)?.Trim() ?? "no output";
    }
}
