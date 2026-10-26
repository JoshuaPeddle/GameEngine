using AVFoundation;
using Foundation;
using GameEngine.Core.Systems;
using UIKit;

namespace GameEngine.Audio.Apple;

public sealed class AppleAudioBackend : IAudioBackend
{
    private sealed class Voice(AVAudioPlayerNode node)
    {
        public AVAudioPlayerNode Node { get; } = node;
        public long Generation;
        public long ActiveGeneration;
    }

    private readonly object gate = new();
    private readonly AVAudioSession session = AVAudioSession.SharedInstance();
    private readonly AVAudioEngine engine = new();
    private readonly Dictionary<string, AVAudioPcmBuffer> buffers = [];
    private readonly Voice[] voices = new Voice[AudioSystem.MaximumEffectVoices + 1];
    private readonly List<NSObject> observers = [];
    private readonly string assetRoot;
    private AVQueuePlayer? music;
    private AVPlayerLooper? looper;
    private bool musicRequested;
    private bool musicHeld;
    private bool interrupted;
    private bool backgrounded;
    private bool disposed;
    private string? error;

    public AppleAudioBackend(string? assetRoot = null, AVAudioSessionCategory category = AVAudioSessionCategory.Ambient,
        AVAudioSessionCategoryOptions categoryOptions = 0)
    {
        this.assetRoot = assetRoot ?? Path.Combine(NSBundle.MainBundle.BundlePath, "assets");
        try
        {
            var categoryError = session.SetCategory(category, categoryOptions);
            if (categoryError != null)
                throw new InvalidOperationException(categoryError.LocalizedDescription);
            for (int i = 1; i < voices.Length; i++)
            {
                var node = new AVAudioPlayerNode();
                engine.AttachNode(node);
                engine.Connect(node, engine.MainMixerNode, null);
                voices[i] = new Voice(node);
            }
            observers.Add(AVAudioSession.Notifications.ObserveInterruption((_, args) =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    if (args.InterruptionType == AVAudioSessionInterruptionType.Began)
                    {
                        interrupted = true;
                        Suspend();
                    }
                    else
                    {
                        interrupted = false;
                        if (!args.Option.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume)) musicHeld = true;
                        Resume();
                    }
                }
            }));
            observers.Add(AVAudioSession.Notifications.ObserveRouteChange((_, args) =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    if (args.Reason == AVAudioSessionRouteChangeReason.OldDeviceUnavailable)
                    {
                        musicHeld = true;
                        music?.Pause();
                    }
                }
            }));
            observers.Add(UIApplication.Notifications.ObserveDidEnterBackground((_, _) =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    backgrounded = true;
                    Suspend();
                    session.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
                }
            }));
            observers.Add(UIApplication.Notifications.ObserveWillEnterForeground((_, _) =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    backgrounded = false;
                    Resume();
                }
            }));
        }
        catch (Exception exception)
        {
            error = $"AVFoundation could not start: {exception.Message}";
        }
    }

    public string Name => "AVFoundation";
    public bool IsAvailable => !disposed && error == null;
    public string? UnavailableReason => disposed ? "The Apple audio backend has been disposed." : error;

    private bool StartEngine()
    {
        if (engine.Running) return true;
        if (!session.SetActive(true, out var sessionError))
        {
            error = $"Cannot activate the audio session: {sessionError?.LocalizedDescription}";
            return false;
        }
        if (engine.StartAndReturnError(out var engineError)) { error = null; return true; }
        error = $"Cannot start AVAudioEngine: {engineError?.LocalizedDescription}";
        return false;
    }

    public void Preload(string soundName, string path, SoundType soundType)
    {
        lock (gate)
        {
            if (!IsAvailable || soundType == SoundType.BGM) return;
            GetBuffer(path);
        }
    }

    private AVAudioPcmBuffer GetBuffer(string path)
    {
        if (buffers.TryGetValue(path, out var cached)) return cached;
        using var url = NSUrl.FromFilename(Path.Combine(assetRoot, path));
        using var file = new AVAudioFile(url, out var openError)
            ?? throw new InvalidDataException(openError?.LocalizedDescription ?? path);
        if (file.Length <= 0 || file.Length > uint.MaxValue) throw new InvalidDataException("Invalid effect length.");
        var buffer = new AVAudioPcmBuffer(file.ProcessingFormat, (uint)file.Length);
        if (!file.ReadIntoBuffer(buffer, out var readError))
        {
            buffer.Dispose();
            throw new InvalidDataException(readError?.LocalizedDescription ?? path);
        }
        buffers.Add(path, buffer);
        return buffer;
    }

    public bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority)
    {
        lock (gate)
        {
            if (!IsAvailable || backgrounded || interrupted) return false;
            if (soundType == SoundType.BGM)
            {
                using var url = NSUrl.FromFilename(Path.Combine(assetRoot, path));
                using var item = AVPlayerItem.FromUrl(url);
                StopMusic();
                music = new AVQueuePlayer();
                looper = AVPlayerLooper.FromPlayer(music, item);
                music.Volume = volume;
                musicRequested = true;
                musicHeld = false;
                if (!StartEngine()) return false;
                music.Play();
                return true;
            }
            if (voice < 1 || voice >= voices.Length) return false;
            var buffer = GetBuffer(path);
            var state = voices[voice];
            StopEffect(state);
            engine.DisconnectNodeOutput(state.Node);
            engine.Connect(state.Node, engine.MainMixerNode, buffer.Format);
            if (!StartEngine()) return false;
            long generation = ++state.Generation;
            Interlocked.Exchange(ref state.ActiveGeneration, generation);
            state.Node.Volume = volume;
            state.Node.ScheduleBuffer(buffer, null, 0, AVAudioPlayerNodeCompletionCallbackType.PlayedBack, _ =>
            {
                Interlocked.CompareExchange(ref state.ActiveGeneration, 0, generation);
            });
            state.Node.Play();
            return true;
        }
    }

    public bool IsPlaying(int voice)
    {
        lock (gate)
            return IsAvailable && (voice == 0 ? musicRequested : voice > 0 && voice < voices.Length && Interlocked.Read(ref voices[voice].ActiveGeneration) != 0);
    }

    public void SetVolume(int voice, float volume)
    {
        lock (gate)
        {
            if (disposed) return;
            if (voice == 0) { if (music != null) music.Volume = volume; }
            else if (voice > 0 && voice < voices.Length) voices[voice].Node.Volume = volume;
        }
    }

    private static void StopEffect(Voice state)
    {
        Interlocked.Increment(ref state.Generation);
        Interlocked.Exchange(ref state.ActiveGeneration, 0);
        state.Node.Stop();
    }

    public void StopVoice(int voice)
    {
        lock (gate)
        {
            if (disposed) return;
            if (voice == 0) StopMusic();
            else if (voice > 0 && voice < voices.Length && voices[voice] != null) StopEffect(voices[voice]);
        }
    }

    private void StopMusic()
    {
        musicRequested = false;
        music?.Pause();
        looper?.DisableLooping();
        looper?.Dispose();
        music?.RemoveAllItems();
        music?.Dispose();
        looper = null;
        music = null;
    }

    private void Suspend()
    {
        music?.Pause();
        foreach (var voice in voices.Skip(1))
            if (voice != null) StopEffect(voice);
        engine.Pause();
    }

    private void Resume()
    {
        if (backgrounded || interrupted || disposed) return;
        if (StartEngine() && musicRequested && !musicHeld) music?.Play();
    }

    public void StopAll()
    {
        lock (gate)
        {
            if (disposed) return;
            Suspend();
            StopMusic();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            StopAll();
            disposed = true;
            foreach (var observer in observers) observer.Dispose();
            observers.Clear();
            engine.Stop();
            foreach (var voice in voices.Skip(1)) voice?.Node.Dispose();
            engine.Dispose();
            foreach (var buffer in buffers.Values) buffer.Dispose();
            buffers.Clear();
            session.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
        }
    }
}
