# Platform audio (GE-77)

Core owns event policy and mixing controls. A host sets `AudioBackends.Factory` before
constructing its first engine. Each engine owns one backend across scene changes and
disposes it when the engine is disposed. No additional third-party native library was
introduced for mobile or the browser. Apple uses the .NET AVFoundation bindings, Android
uses the .NET Android framework bindings, and the browser uses built-in Web Audio APIs.
SDL2_mixer remains the desktop backend.

## Platforms and formats

| Host | Implementation | Content and limits |
| --- | --- | --- |
| iOS | `GameEngine.Audio.Apple`, `net10.0-ios`: AVAudioEngine and a pool of 31 AVAudioPlayerNode effect voices; AVQueuePlayer with AVPlayerLooper for streaming music | Prefer AAC in M4A for music and short PCM WAV effects. Effects are decoded once into cached AVAudioPcmBuffers. Music is read by AVFoundation without decoding the entire track into an effect buffer. OGG/Vorbis is not a supported iOS content choice. |
| Android | `GameEngine.Audio.Android`, `net10.0-android`: SoundPool effects, MediaPlayer looping music | Prefer OGG/Vorbis or AAC/M4A music and short WAV or OGG effects. SoundPool defaults to 16 streams, configurable in the backend constructor. Keep each effect below SoundPool's approximately 1 MB decoded sample limit. MediaPlayer streams from a private cache file. |
| Browser | WebAudioBackend in the browser head, with `wwwroot/audioInterop.js` | Prefer AAC/M4A music for the supported browsers, with WAV effects. OGG support depends on the browser and codec. Effects use cached decodeAudioData buffers; music uses looping HTMLAudioElement streams routed through Web Audio gain nodes. Playback waits for a pointer or keyboard gesture. |
| Windows desktop | GameEngine.Audio.Sdl | The existing SDL2_mixer DLL and decoder build determine formats. WAV remains the demo baseline. Music still uses a decoded, looping mixer channel. |
| Linux and macOS desktop | GameEngine.Audio.Sdl when the host supplies SDL2_mixer | Native mixer installation/distribution remains a separate open part of GE-77. The repository still supplies only a Windows mixer DLL. |

The demo keeps its existing CC0 WAV files so every backend exercises the same content.
Production mobile games should convert long WAV music to AAC/M4A or OGG as above and
change their manifest entries. A host can supply a different manifest through IAssetSource
when platform format choices differ. Build success establishes API compatibility, not
codec support or audible playback on a device.

## Host registration and lifecycle

The iOS AppDelegate registers AppleAudioBackend. Its default asset root is the bundle's
`assets` directory, matching the head's BundleResource layout. The default session category
is Ambient with no options. It respects the silent switch and mixes with other apps.
A host can pass a different category and options to the constructor, for example Playback
with MixWithOthers when playing despite the silent switch is a deliberate product choice.
The library does not request background audio privileges.

Apple interruption notifications pause music and discard effect voices. Music resumes
from its retained position only after the interruption ends with ShouldResume and the app
is in the foreground. Background notifications pause playback and deactivate the session;
foreground notifications reactivate the session and engine. Removing headphones pauses
music and holds it paused across backgrounding and interruption recovery. An explicit
`Play(name, SoundType.BGM)` starts music again. Notification subscriptions, nodes, buffers,
loopers and players are released on disposal. Music preparation happens at playback;
Apple's Preload warms effect buffers only.

The Android application registers AndroidAudioBackend with the application's asset source.
The backend copies requested assets to a private, per-engine cache directory because APK
assets may be compressed and cannot always be passed to a native player as file descriptors.
Files retain their codec extensions and are removed after native players are released.
SoundPool loading is asynchronous; Preload starts decoding before combat. A first play
while loading can wait for at most 0.25 seconds, then it is discarded to avoid late effects.
SoundPool has no completion callback for individual streams, so the backend tracks their
lifetimes from media duration metadata and a monotonic clock. Keep effects short and
verify the decoded duration on target devices.

Android requests audio focus on demand with game audio attributes. API 26 and later use
AudioFocusRequest; API 23 through 25 use the legacy framework call. Transient focus loss
pauses music and discards effects. Duck requests apply a 0.2 volume multiplier, preserving
Core's mixer values; gain restores them and resumes eligible music. Permanent loss clears
the music resume intent. An explicit music play can request focus again. Application
activity callbacks pause playback and abandon focus on pause, and request focus again on
resume. Focus denial produces no effects; prepared music waits for a later eligible resume.
No foreground service or background music permission is added.

The browser registers WebAudioBackend before starting Avalonia. `main.js` installs the
interop module before managed startup. Sound files are published as static content under
`audio/sounds`, independently of Avalonia's embedded image and level assets. Preserve this
directory when deploying, including deployments under a URL prefix. Music waits for a real
pointer or keyboard gesture to resume AudioContext. Effect requests older than 0.25 seconds
are discarded, including effects emitted before unlock. Visibility changes pause music,
discard effects and suspend the context. Returning to the page resumes eligible music
subject to browser autoplay policy; another gesture can unlock it again. Contexts, event
listeners, gain nodes and media elements are released with the engine.

GameView now enables audio by default on all platforms. `AudioEnabled="False"` remains
available for previews and headless hosts. A missing registration still creates an
unavailable service with a diagnostic rather than failing engine startup. Backend
UnavailableReason also reports platform playback errors where available. IsAvailable
reports capability, not whether an interruption, focus decision, mute or autoplay gate
currently permits audible output.

## Shared controls

Use the AudioSystem supplied to a scene. Calls belong on the engine thread; UI controls
should enqueue changes through the engine's existing work queue.

```csharp
audio.ConfigureSound("Hit", new SoundPlaybackOptions(
    maxConcurrentVoices: 3, minimumRetriggerInterval: 0.06, priority: 2));
audio.Preload("Hit");
audio.Preload("Level1", SoundType.BGM);
audio.MasterVolume = 0.8f;
audio.MusicVolume = 0.35f;
audio.SfxVolume = 0.7f;
audio.Muted = false;
audio.Play("Level1", SoundType.BGM);
audio.Play("Hit", SoundType.SoundEffect);
```

Music starts immediately and loops on voice zero. Effect requests accumulate until the
next AudioSystem.Update. Because system updates precede scene updates, a scene's effects
are submitted on the following simulation tick. This adds at most one simulation step of
latency. Many identical requests become one event by default. SoundPlaybackOptions can
allow several events per frame; the per-sound concurrent cap still applies. Retrigger
intervals use simulation seconds. StopAll clears queued requests and retrigger history as
well as native playback. Scene changes retain the service; scenes that should silence the
previous scene should call StopAll explicitly.

The default global effect cap is 16 and the hard ceiling is 31. An explicitly constructed
AudioSystem can choose a smaller or larger cap within that ceiling. Match Android's
maxStreams to it. Defaults per sound are two concurrent voices, a 0.05 second minimum
retrigger interval, priority zero, and one event per update. Events in a frame are ranked
by priority before voice allocation. Higher-priority events can steal the lowest-priority,
oldest eligible voice; equal and lower priorities are dropped when full. A per-sound cap
can only steal a voice of that same sound. At most 128 distinct sounds can be pending;
a higher-priority request can replace a lower-priority pending sound at that bound.

Master, music and effect volumes are finite values from zero to one. Effective gain is
master multiplied by the relevant category. Mute sets effective gain to zero without
losing the configured volumes or music position. Changes affect active voices and future
playback. Preload resolves the strict sound manifest without decoding unrelated textures,
and is idempotent per sound and category. It does not play anything. Native buffer/file
caches outlive scenes and are released with their backend.

IAudioBackend now accepts explicit voice slots through TryPlay, reports IsPlaying, and
supports Preload, SetVolume and StopVoice as well as StopAll. Custom backends must implement
these operations. Returning false from TryPlay means the request consumed no voice or
retrigger budget. A true result reserves that slot until IsPlaying becomes false or it is
stopped. Volume changes must work for pending as well as active playback. SilentAudioBackend
remains suitable for headless use. SDL uses its native channel completion state and volume
API with the same shared policy.

Void Bastion preloads Hit and Level1, loops Level1 on entry and plays Hit for kills. Select
it from the demo menu on either mobile head. The demo test harness verifies these requests
through a recording backend and actual Engine.Tick calls.

## Verification and device checks

Release builds of both backend projects and the browser head are required. Core tests
cover event coalescing, configured burst limits, concurrent and global caps, completion,
priority stealing, pending priority order, live cap reduction, retrigger timing, preload reuse, live volumes,
mute, invalid gain values, stop/reset and failed playback. The demo suite verifies Void
Bastion's preload, music and kill-effect path without a device.

Before advertising audible support, run these checks on a physical iPhone and Android
phone. Listen to music and overlapping effects in Void Bastion, cause a burst of kills,
change all mixer gains, and mute/unmute while music is playing. Verify a call, Siri or
voice assistant interruption, and another app requesting audio focus. Check foreground,
background and repeated scene/view disposal. Music must retain position on eligible resume,
short effects must not burst on return, and no audio may play while backgrounded. On iOS,
check both silent switch positions, another app's music, Bluetooth route changes, and
headphone removal. Headphone removal must hold music paused until explicit playback.
On Android, verify both modern focus handling and the API 23 to 25 fallback if supported.
Measure cold preload time and memory, and check SoundPool truncation with production assets.

In Chrome and Safari, serve the published browser bundle, including from a subdirectory.
Verify initial silence before interaction, first-gesture unlock, actual nonzero output,
looping music, overlapping effects, hidden-tab suspension, a second engine after disposal,
404/unsupported-codec diagnostics and gain/mute changes. Browser automation can establish
context state and signal generation, but a listener still needs to check speaker output
and seamless-enough looping with the chosen production codec.

The implementation follows Apple's [interruption guidance](https://developer.apple.com/documentation/avfaudio/handling-audio-interruptions)
and [route-change guidance](https://developer.apple.com/library/archive/documentation/Audio/Conceptual/AudioSessionProgrammingGuide/HandlingAudioHardwareRouteChanges/HandlingAudioHardwareRouteChanges.html),
Android's [audio focus guidance](https://developer.android.com/media/optimize/audio-focus)
and [SoundPool contract](https://developer.android.com/reference/android/media/SoundPool),
and the [Web Audio API](https://www.w3.org/TR/webaudio/).

## Local verification on 2026-10-26

The Release solution build passed with zero warnings. The solution's Any CPU configuration
continues to exclude the iOS application, and now excludes the Apple backend as well;
Apple is built explicitly on macOS in the native-audio CI job. The Android head produced
a signed Release APK. Both standalone backend target builds passed with zero warnings:

```sh
dotnet build GameEngine.Audio.Apple -c Release -f net10.0-ios
dotnet build GameEngine.Audio.Android -c Release -f net10.0-android
dotnet build GameEngine.sln -c Release
dotnet test -c Release --no-build
node --test scripts/audio-interop.test.mjs
```

The complete .NET suite passed: 315 Core, 239 Demo, 57 Editor and 8 Avalonia runner tests,
619 total with no skips. Five JavaScript state tests passed using Web Audio and DOM test
doubles. They cover deployment prefixes, cached preload, gesture unlock, stale-effect
expiry, overlapping voices, gains, completion/replacement, visibility suspension and
disposal. CI runs these tests and compiles both native backend targets on macOS.

The browser head built in Release with zero warnings. A static Release publish with
RunAOTCompilation=false and WasmStripILAfterAOT=false completed and included the sound
files under wwwroot/audio/sounds. The normal Release browser build also passed; browser
AOT publishing was not exercised. A real-browser smoke check could not run: Playwright's
Chromium process exited during startup and the in-app browser was unavailable. Codec
output, autoplay enforcement, real page visibility events and audible playback remain
manual checks.

Building the iOS head for iossimulator-arm64 compiled the managed integration, then stopped
because the installed iOS workload 26.5.10315 requires Xcode 26.6 while this Mac has Xcode
27.0. No simulator app, physical-device archive or audible mobile check was produced.
Use a matched workload and Xcode before performing device acceptance.

Sandbox-compatible builds redirected NuGet packages, HTTP cache, CLI state and the
Android temporary signing key into /tmp. Existing NuGet packages were supplied as a
read-only RestoreFallbackFolders source. Android builds explicitly selected the installed
JDK 17 and Android SDK. Avalonia build telemetry was disabled through UsedAvaloniaProducts.
These are local invocation settings, not project defaults. No commit or device install
was made.
