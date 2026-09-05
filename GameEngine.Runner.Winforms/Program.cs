using GameEngine.Audio.Sdl;
using GameEngine.Core.Systems;

namespace GameEngine.WinForms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            // Core has no audio integration of its own; a desktop head supplies one.
            AudioBackends.Factory = SdlAudioBackend.Create;

            ApplicationConfiguration.Initialize();
            Application.Run(new MainView());
        }
    }
}