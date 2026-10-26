namespace GameEngine.Audio.Android;

internal static class AndroidAudioFocus
{
    public static bool Request(ref bool hasFocus, bool backgrounded, bool focusPaused,
        Func<bool> request, Action resumeMusic)
    {
        if (backgrounded || focusPaused) return false;
        if (!hasFocus) hasFocus = request();
        if (hasFocus) resumeMusic();
        return hasFocus;
    }
}
