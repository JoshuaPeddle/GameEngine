# Game UI kit (GE-160)

`GameEngine.UI.Avalonia` is an optional .NET 10 NuGet package for game HUDs and menus. It supplies its own control templates, embedded typography, procedural Skia surfaces and short, event-driven motion. It works without an application-wide Fluent theme.

The kit is a separate package because its Avalonia controls and fonts belong to presentation, while the runner owns engine hosting and input. A game can use the kit with an existing Avalonia shell or alongside `GameView`. Core has no reference to the kit or Avalonia, and installing the runner does not force a game to use these controls. The demo references both to exercise the integration.

## Theme and typography

`GameTheme` is an immutable record containing a `GamePalette`, `GameTypeScale`, `GameSpacing`, `GameRadii`, stroke width, glow strength, `GameMotion`, and display/text font families. The palette includes background, surface, raised surface, ink, muted ink, accent, ink on accent, danger, rim and three rarity colours. Motion durations are seconds; easing choices are linear, cubic out and cubic in/out.

Set the inherited theme on a root control. Descendants update when that theme changes:

```csharp
using Avalonia.Media;
using GameEngine.UI.Avalonia;

var coast = GameTheme.NightCoast;
var theme = coast with
{
    Name = "My coast",
    Palette = coast.Palette with { Accent = Color.Parse("#A5EDD5") },
    Motion = coast.Motion with { Counter = .3 }
};
var screen = new GameScreen();
GameUi.SetTheme(screen, theme);
```

The supplied themes are dark **Night coast** and bright **Daybreak**. Both are tested for at least 4.5:1 contrast for normal, muted, accent, danger and rarity text against all three surface tokens, plus ink on accent. A custom palette needs the same checks using `GameTheme.Contrast`. Rendered button text bands are also checked across interaction states.

Oxanium Medium and SemiBold supply titles, buttons and counters. Atkinson Hyperlegible Regular supplies body text and descriptions. Both are embedded Avalonia resources under the SIL Open Font License 1.1. Their copyright notices and source links are in [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md); the complete licences ship in the package. `GameLabel` enables tabular figures, and tests verify equal digit advances for the counter face. Games may override either font family in their theme.

## Components

| Control | Behaviour |
| --- | --- |
| `GameLabel` | Caption, body, title and display roles; semantic ink colours; wrapping and inherited text scale |
| `GameSurface`, `GamePanel` | Procedural frames, optional ornaments, layered fills, rim and inner light, deterministic fine noise |
| `GameButton` | Primary, secondary and icon variants; hover, press, focus and disabled response; press scaling |
| `GameTabs`, `GameTab` | Themed tab headers, selection and keyboard navigation |
| `GameItemCard`, `GameItemRow` | Icon, title, description, price, balance, lock and affordability state; `PurchaseRequested` event |
| `ResourceCounter` | Animated number interpolation and vertical roll; tabular digits; K/M/B/T/Qa/Qi formatting, then scientific notation |
| `GameProgress` | Labelled percentage bar using native progress semantics |
| `GameCooldown` | Remaining-time bar; `Start` and `Advance` use simulation seconds supplied by the game |
| `GameBadge` | Compact label with semantic rarity or status ink |
| `GameToggle`, `GameSlider` | Native toggle/range semantics with kit templates, keyboard input and feedback |
| `GameSheet` | Bottom sheet, scrim, enter/exit motion, scrolling body, focus cycling, Escape/scrim dismissal and focus restoration |
| `GameToast` | Polite notification, enter/exit motion, one expiry timer while visible |
| `GameInfoPopover` | Toggleable information panel with Escape dismissal |
| `GameScreen`, `GameScrollViewer` | Safe-area root and vertical scrolling without a global control theme |
| `GameIcon` | Menu, pause, play, speed, settings, sound, close, back, upgrade, coin, lock, check, info, trophy, research and prestige |

Surfaces use cached 240 by 240 Skia tiles at 3x scale and nine-patch drawing, preserving corners as a panel grows. The cache key includes the immutable theme, surface kind, interaction state and ornaments. Its capacity is 64 tiles; eviction and `SurfaceCache.Clear()` dispose retained native images. Keep theme records stable instead of creating a new theme every frame. The sixteen built-in icons and game-defined icon sources are vector paths; the kit requires no raster artwork.

## Using it in a game

Add `GameEngine.UI.Avalonia` at the same release version as the other engine packages. Existing Avalonia Skia setup loads the embedded fonts without registration. Use a `GameScreen` above or around the game's `GameView`, with a grid for HUD controls and a sheet/toast layer last in the grid so it overlays the scene.

```csharp
using Avalonia.Controls;
using GameEngine.UI.Avalonia;

var wallet = new ResourceCounter { Label = "SALVAGE", Value = 12840 };
var upgrade = new GameItemCard
{
    Title = "Prism relay",
    Description = "Each pulse finds a second target.",
    Icon = GameIconKind.Prestige,
    Cost = 2400,
    Balance = 12840
};
var sheet = new GameSheet { Title = "Workshop", Body = upgrade };
var open = new GameButton { Content = "Workshop", Icon = GameIconKind.Upgrade };
open.Click += (_, _) => sheet.Open();
var root = new GameScreen
{
    Child = new Grid
    {
        Children = { new StackPanel { Children = { wallet, open } }, sheet }
    }
};
GameUi.SetTheme(root, GameTheme.NightCoast);
```

Wire `PurchaseRequested` and other actions to game commands. For engine-owned state, post mutations through `engine.Post(...)`, publish an immutable presentation state, and marshal UI refreshes onto `Dispatcher.UIThread`. The kit's affordability check prevents an unavailable button action; the game must still validate a purchase against its current balance. Update counter values and card balances when that state changes. `SceneUiGallery` and `UiGalleryView` show this full path through a real engine.

`GameUi.SetFeedback(root, callback)` supplies a single inherited hook with `Activate`, `Toggle`, `Adjust` and `Dismiss` cues. Map these to the game's chosen sound and haptic services and preferences. The UI package does not own audio playback or device haptics.

Interactive targets are at least 48 dp. Give icon-only buttons an `AutomationProperties.SetName` label. Set `GameUi.SetTextScale(root, scale)` from the host's text-size preference, from 1 through 3. Set `GameUi.SetReducedMotion(root, true)` from the accessibility preference; active non-essential motion finishes immediately and new transitions snap to their final state. Cooldown time still advances from supplied simulation seconds, and a toast still expires. These preferences are inherited but are not automatically connected to OS settings.

`GameScreen` reads `TopLevel.InsetsManager.SafeAreaPadding` and observes changes. `SafeInsets` can supply additional application insets; each edge uses the greater of the native and supplied inset. Do not also add the same padding in the parent shell.

For Tower Idle, replace `GamePage`'s local text/button/card helpers with these controls, wrap its overlay stack in `GameScreen`, and map `ArtDirection` colours to a stable theme. Replace direct counter string updates with `ResourceCounter.Value`, map workshop/research rows to cards, and use `GameSheet` for menu panels. Preserve `GameHost`'s command, save and lifecycle rules. Connect its sound/haptic and accessibility settings to the inherited hooks. This engine change does not modify Tower Idle.

## Game-defined vector icons

`GameIconSource.FromPath(path, viewport: 24)` parses and retains an immutable vector source once. Store sources in static fields or a game-owned catalog and reuse them. The viewport is a square in the path's coordinate units; use 64 for existing 64-unit game artwork. Invalid paths and nonpositive or nonfinite viewports fail at creation. Sources own their private parsed geometry, require no registration or disposal, and leave no global cache entries behind.

Set `GameIcon.Source` or `IconSource` on `GameItemCard`, `GameItemRow`, `GameButton`, `GameBadge` and `ResourceCounter`. A source takes precedence over the existing enum. Clearing it restores the enum; no existing enum property or call site changes. Badges also accept an optional built-in `Icon`. Cards and rows expose `IconForeground` for game-specific tints, while standalone icons use `Foreground`. Button icons follow primary, disabled and normal button ink, and badge icons follow their semantic ink.

```csharp
static readonly GameIconSource Beacon = GameIconSource.FromPath(
    "M8 21L10 8H14L16 21Z M8 5H16V8H8Z M7 5L12 1 17 5");
var beacon = new GameItemCard { Title = "Beacon lens", IconSource = Beacon };
var travel = new GameButton { Content = "Sail", IconSource = Beacon };
```

Both built-in and custom icons use the same rounded vector strokes, theme `Stroke` and restrained `Glow`. Rendering centres the square viewport within the available bounds and remains vector at every display density. No path parsing or raster cache generation occurs during painting. The gallery's Prism relay uses a game-defined prism path. Buttons also accept control content for icon tiles; strings retain their normal themed text template. `CustomIconTests` verifies all hosts, fallback, tint, theme response, density rendering, reuse and invalid input.

## Gallery and reproduction

Launch the interactive gallery with:

```sh
dotnet run --project GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Desktop -- --ui-gallery
```

Workshop, Settings and Library show the components, and the reward sheet demonstrates the overlay. The settings icon switches themes. The headless tests drive purchases through `SceneHarness`, render every component, check input and disabled states, exercise reduced motion, text scaling and safe areas, and verify cache reuse and bounds.

The ten committed PNGs in [`ui-kit/`](ui-kit/) are 1080 by 2400 pixels, rendered from a 360 by 800 dp gallery at 3x density. Each theme has workshop, settings, library, sheet and scrolled-details captures:

| Theme | Workshop | Settings | Library | Sheet | Details |
| --- | --- | --- | --- | --- | --- |
| Night coast | [PNG](ui-kit/night-coast-workshop.png) | [PNG](ui-kit/night-coast-settings.png) | [PNG](ui-kit/night-coast-library.png) | [PNG](ui-kit/night-coast-sheet.png) | [PNG](ui-kit/night-coast-details.png) |
| Daybreak | [PNG](ui-kit/daybreak-workshop.png) | [PNG](ui-kit/daybreak-settings.png) | [PNG](ui-kit/daybreak-library.png) | [PNG](ui-kit/daybreak-sheet.png) | [PNG](ui-kit/daybreak-details.png) |

Regenerate captures and the complete-gallery repaint measurement with:

```sh
UI_KIT_CAPTURE_DIR="$PWD/docs/ui-kit" dotnet test GameEngine.Demo.Tests -c Release --filter FullyQualifiedName~UiGalleryTests
dotnet run --project GameEngine.Benchmarks -c Release -- --ui
```

## Measurements

Measured on macOS 26.6.2, Arm64, .NET 10.0.10. These are desktop CPU/raster measurements, with no claim about phone GPU time, thermal behaviour or a whole game's frame rate.

The complete-gallery measurement forces 120 raster repaints after 20 warmup paints at 1080 by 2400. [`gallery-measurements.json`](ui-kit/gallery-measurements.json) records:

| Theme | Mean repaint | p95 repaint | Managed bytes per repaint |
| --- | --- | --- | --- |
| Night coast | 3.620 ms | 3.818 ms | 81,967 |
| Daybreak | 3.570 ms | 3.602 ms | 81,944 |

These numbers include Avalonia's immediate bitmap-render path and text/control drawing. No new surface tiles are generated during warm repainting. This is the cost of requesting a repaint, not the cost of leaving a menu idle.

The focused `--ui` surface benchmark on 2026-11-12 draws one panel, three rows and four buttons at the same pixel size, with 60 warmup draws and 300 measured draws. Night coast averaged 3.428 ms (p95 3.498 ms), Daybreak 3.438 ms (p95 3.495 ms). Both allocated zero managed bytes per warm draw and generated zero new tiles. Cold first draws were 28.410 ms and 5.878 ms respectively, including first-use work. One million calls to an inactive tween's guard took 0.936 ms with zero managed allocation; production controls do not poll that guard while idle.

For actual idle observation, `SettledGallerySchedulesNoAnimationTicks` opens a real headless gallery with normal motion enabled. It visits workshop, settings, library and an open sheet in each theme, waits 700 ms for transitions to settle, then measures a 1-second asynchronous idle interval with the dispatcher alive. It does not call `RenderTargetBitmap.Render`, force frames, or run the engine loop during the interval. [`idle-measurements.json`](ui-kit/idle-measurements.json) records wall time, whole-test-host process CPU time, active kit animation timers and animation tick deltas for all eight observations. Every observation had **zero active animation timers and zero animation ticks**. Process CPU ranged from 5.466 to 19.363 ms per approximately 1002 ms, or 0.55% to 1.93% of one core. This includes Avalonia, the test runner and runtime work and is not an isolated kit CPU estimate. The zero-tick result is asserted; noisy CPU percentages are reported without a performance threshold.

Motion drivers stop their timers on completion, reduced-motion changes and detach. Cooldowns have no internal timer. A visible toast uses one expiry timer until dismissed. The game and host may still request their own frames independently.

## Build, tests and packaging

Verification on 2026-11-12 used `dotnet build GameEngine.sln -c Release` with zero warnings and zero errors, followed by `dotnet test -c Release --no-build`. All 795 tests passed: 409 Core, 312 Demo, 58 Editor and 16 Runner. The Demo suite includes 60 UI tests. The opt-in frame-offer profiling fixture remains explicit and is not part of the normal suite. The solution's existing Any CPU configuration does not build the iOS head or Apple audio backend. The installed Android and browser workloads built successfully; Android used a temporary Temurin JDK 21 because the application's bundled JDK 25 is unsupported by the installed Android workload.

The managed sandbox required temporary writable .NET/NuGet caches and `-p:UsedAvaloniaProducts=` to skip Avalonia's build telemetry, which otherwise writes outside the workspace. These are verification-environment settings, not repository changes. Android SDK and Java SDK paths were supplied explicitly.

`dotnet pack GameEngine.UI.Avalonia -c Release` produced both `.nupkg` and `.snupkg` without warnings. Inspection verified the .NET 10 assembly, MIT package metadata, README, third-party notices and both full OFL licences. A separate scratch app restored the local package, opened all three embedded font resources, resolved Oxanium and Atkinson Hyperlegible glyph faces, and rendered a counter at 1080 by 2400. It used the package rather than a project reference.

The NuGet workflow packs the kit with the same release version as Core, audio, runner and template packages. The template smoke workflow also packs it into the local feed. Both additions reference the existing project path; package upload/push globbing already includes the new package. No workflow was dispatched and no package or branch was pushed during this verification.
