using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using GameEngine.Core;
using GameEngine.Core.Systems;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Globalization;

namespace GameEngine.Audio.Android;

public sealed class AndroidAudioBackend : IAudioBackend
{
    private sealed record Sample(int Id, double Duration) { public bool Ready; public bool Failed; }
    private sealed class Voice(Sample sample, float volume, int priority)
    {
        public Sample Sample { get; } = sample;
        public float Volume = volume;
        public int Priority { get; } = priority;
        public int Stream;
        public double Requested = Now;
        public double Ends;
    }
    private sealed class FocusListener(Action<AudioFocus> changed) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange) => changed(focusChange);
    }
    private sealed class Lifecycle(AndroidAudioBackend owner) : Java.Lang.Object, Application.IActivityLifecycleCallbacks
    {
        public void OnActivityPaused(Activity activity) => owner.SetBackgrounded(true);
        public void OnActivityResumed(Activity activity) => owner.SetBackgrounded(false);
        public void OnActivityCreated(Activity activity, Bundle? state) { }
        public void OnActivityDestroyed(Activity activity) { }
        public void OnActivitySaveInstanceState(Activity activity, Bundle state) { }
        public void OnActivityStarted(Activity activity) { }
        public void OnActivityStopped(Activity activity) { }
    }

    private readonly object gate = new();
    private readonly Application application;
    private readonly IAssetSource source;
    private readonly AudioManager manager;
    private readonly AudioAttributes attributes;
    private readonly SoundPool pool;
    private readonly FocusListener focusListener;
    private readonly AudioFocusRequestClass? focusRequest;
    private readonly Lifecycle lifecycle;
    private readonly string cacheDirectory;
    private readonly Dictionary<string, string> files = [];
    private readonly Dictionary<string, Sample> samples = [];
    private readonly Dictionary<int, Voice> voices = [];
    private MediaPlayer? music;
    private bool musicReady;
    private bool musicRequested;
    private bool backgrounded;
    private bool focusPaused;
    private bool hasFocus;
    private bool disposed;
    private float musicVolume = 1;
    private float duck = 1;
    private string? error;
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public AndroidAudioBackend(Application application, IAssetSource source, int maxStreams = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStreams, 1);
        this.application = application;
        this.source = source;
        manager = (AudioManager)application.GetSystemService(Context.AudioService)!;
        attributes = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Sonification)!.Build()!;
        pool = new SoundPool.Builder().SetMaxStreams(maxStreams)!.SetAudioAttributes(attributes)!.Build()!;
        pool.LoadComplete += OnLoadComplete;
        focusListener = new FocusListener(OnFocusChanged);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain).SetAudioAttributes(attributes)!
                .SetOnAudioFocusChangeListener(focusListener, new Handler(Looper.MainLooper!))!
                .SetWillPauseWhenDucked(true)!.Build();
        cacheDirectory = Path.Combine(application.CacheDir!.AbsolutePath, "game-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDirectory);
        lifecycle = new Lifecycle(this);
        application.RegisterActivityLifecycleCallbacks(lifecycle);
    }

    public string Name => "Android SoundPool/MediaPlayer";
    public bool IsAvailable => !disposed;
    public string? UnavailableReason => disposed ? "The Android audio backend has been disposed." : error;

    private string GetFile(string path)
    {
        if (files.TryGetValue(path, out var file)) return file;
        file = Path.Combine(cacheDirectory, files.Count.ToString(CultureInfo.InvariantCulture) + Path.GetExtension(path));
        using (var input = source.Open(path))
        using (var output = File.Create(file)) input.CopyTo(output);
        files.Add(path, file);
        return file;
    }

    private Sample GetSample(string path)
    {
        if (samples.TryGetValue(path, out var sample)) return sample;
        string file = GetFile(path);
        using var metadata = new MediaMetadataRetriever();
        metadata.SetDataSource(file);
        double duration = double.Parse(metadata.ExtractMetadata(MetadataKey.Duration) ?? "0", CultureInfo.InvariantCulture) / 1000;
        if (duration <= 0) throw new InvalidDataException($"Cannot read effect duration: {path}");
        int id = pool.Load(file, 1);
        sample = new Sample(id, duration) { Failed = id == 0 };
        samples.Add(path, sample);
        return sample;
    }

    public void Preload(string soundName, string path, SoundType soundType)
    {
        lock (gate)
        {
            if (!IsAvailable) return;
            if (soundType == SoundType.SoundEffect) GetSample(path);
            else GetFile(path);
        }
    }

    private bool RequestFocus()
    {
        if (backgrounded || focusPaused) return false;
        if (hasFocus) return true;
        AudioFocusRequest granted;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            granted = manager.RequestAudioFocus(focusRequest!);
        else
#pragma warning disable CS0618, CA1422
            granted = manager.RequestAudioFocus(focusListener, global::Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CS0618, CA1422
        hasFocus = granted == AudioFocusRequest.Granted;
        return hasFocus;
    }

    public bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority)
    {
        lock (gate)
        {
            if (!IsAvailable) return false;
            if (soundType == SoundType.BGM)
            {
                StopMusic();
                musicVolume = volume;
                musicRequested = true;
                focusPaused = false;
                var player = new MediaPlayer();
                music = player;
                player.SetAudioAttributes(attributes);
                player.Looping = true;
                player.Prepared += (_, _) =>
                {
                    lock (gate)
                    {
                        if (disposed || music != player) return;
                        musicReady = true;
                        ResumeMusic();
                    }
                };
                player.Error += (_, args) =>
                {
                    lock (gate)
                    {
                        if (music == player) { musicRequested = false; musicReady = false; error = $"MediaPlayer failed: {args.What}/{args.Extra}"; }
                        args.Handled = true;
                    }
                };
                player.SetDataSource(GetFile(path));
                player.PrepareAsync();
                return true;
            }
            if (!RequestFocus()) return false;
            StopVoice(voice);
            var sample = GetSample(path);
            if (sample.Failed) return false;
            var state = new Voice(sample, volume, priority);
            voices.Add(voice, state);
            if (sample.Ready) StartVoice(state);
            return state.Stream != 0 || !sample.Ready;
        }
    }

    private void StartVoice(Voice voice)
    {
        voice.Stream = pool.Play(voice.Sample.Id, voice.Volume * duck, voice.Volume * duck, Math.Max(0, voice.Priority), 0, 1);
        voice.Ends = voice.Stream == 0 ? 0 : Now + voice.Sample.Duration;
    }

    private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs args)
    {
        lock (gate)
        {
            if (disposed) return;
            var sample = samples.Values.FirstOrDefault(value => value.Id == args.SampleId);
            if (sample == null) return;
            sample.Ready = args.Status == 0;
            sample.Failed = args.Status != 0;
            if (sample.Failed) error = $"SoundPool could not decode sample {args.SampleId}: {args.Status}";
            foreach (var voice in voices.Values.Where(value => value.Sample == sample && value.Stream == 0))
                if (sample.Ready && !backgrounded && !focusPaused && Now - voice.Requested <= 0.25)
                    StartVoice(voice);
        }
    }

    public bool IsPlaying(int voice)
    {
        lock (gate)
        {
            if (!IsAvailable) return false;
            if (voice == 0) return musicRequested;
            if (!voices.TryGetValue(voice, out var state)) return false;
            return state.Stream != 0 ? Now < state.Ends : !state.Sample.Failed && Now - state.Requested <= 0.25;
        }
    }

    public void SetVolume(int voice, float volume)
    {
        lock (gate)
        {
            if (disposed) return;
            if (voice == 0) { musicVolume = volume; music?.SetVolume(volume * duck, volume * duck); }
            else if (voices.TryGetValue(voice, out var state))
            {
                state.Volume = volume;
                if (state.Stream != 0) pool.SetVolume(state.Stream, volume * duck, volume * duck);
            }
        }
    }

    public void StopVoice(int voice)
    {
        lock (gate)
        {
            if (disposed) return;
            if (voice == 0) StopMusic();
            else if (voices.Remove(voice, out var state) && state.Stream != 0) pool.Stop(state.Stream);
        }
    }

    private void StopMusic()
    {
        musicRequested = false;
        musicReady = false;
        music?.Release();
        music?.Dispose();
        music = null;
    }

    private void StopEffects()
    {
        foreach (var state in voices.Values)
            if (state.Stream != 0) pool.Stop(state.Stream);
        voices.Clear();
    }

    private void Pause()
    {
        if (musicReady && music?.IsPlaying == true) music.Pause();
        StopEffects();
    }

    private void ResumeMusic()
    {
        if (musicRequested && musicReady && RequestFocus())
        {
            music!.SetVolume(musicVolume * duck, musicVolume * duck);
            music.Start();
        }
    }

    private void OnFocusChanged(AudioFocus focus)
    {
        lock (gate)
        {
            if (disposed) return;
            if (focus == AudioFocus.Gain)
            {
                hasFocus = true;
                focusPaused = false;
                duck = 1;
                ResumeMusic();
                foreach (var (id, state) in voices) SetVolume(id, state.Volume);
            }
            else if (focus == AudioFocus.LossTransientCanDuck)
            {
                duck = 0.2f;
                music?.SetVolume(musicVolume * duck, musicVolume * duck);
                foreach (var (id, state) in voices) SetVolume(id, state.Volume);
            }
            else
            {
                hasFocus = false;
                focusPaused = true;
                Pause();
                if (focus == AudioFocus.Loss) musicRequested = false;
            }
        }
    }

    private void AbandonFocus()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) manager.AbandonAudioFocusRequest(focusRequest!);
        else
#pragma warning disable CS0618, CA1422
            manager.AbandonAudioFocus(focusListener);
#pragma warning restore CS0618, CA1422
        hasFocus = false;
    }

    private void SetBackgrounded(bool value)
    {
        lock (gate)
        {
            if (disposed) return;
            backgrounded = value;
            if (value) { Pause(); AbandonFocus(); }
            else { focusPaused = false; duck = 1; ResumeMusic(); }
        }
    }

    public void StopAll()
    {
        lock (gate)
        {
            if (disposed) return;
            StopEffects();
            StopMusic();
            focusPaused = false;
            AbandonFocus();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            StopAll();
            disposed = true;
            application.UnregisterActivityLifecycleCallbacks(lifecycle);
            pool.LoadComplete -= OnLoadComplete;
            pool.Release();
            pool.Dispose();
            focusRequest?.Dispose();
            focusListener.Dispose();
            lifecycle.Dispose();
            attributes.Dispose();
            Directory.Delete(cacheDirectory, true);
        }
    }
}
