# Third-Party Notices

GameEngine is distributed under the MIT License (see [`LICENSE`](LICENSE)). It
depends on, and in one case redistributes, the following third-party software.

## Redistributed binaries

### SDL2_mixer

`GameEngine.Core/SDL2_mixer.dll` is a prebuilt Windows x64 binary of SDL2_mixer,
redistributed under the zlib license.

> Copyright (C) 1997-2024 Sam Lantinga and contributors
>
> This software is provided 'as-is', without any express or implied warranty. In
> no event will the authors be held liable for any damages arising from the use
> of this software.
>
> Permission is granted to anyone to use this software for any purpose,
> including commercial applications, and to alter it and redistribute it freely,
> subject to the following restrictions:
>
> 1. The origin of this software must not be misrepresented; you must not claim
>    that you wrote the original software. If you use this software in a product,
>    an acknowledgment in the product documentation would be appreciated but is
>    not required.
> 2. Altered source versions must be plainly marked as such, and must not be
>    misrepresented as being the original software.
> 3. This notice may not be removed or altered from any source distribution.

SDL2 itself is licensed under the same terms and reaches this project through
`ppy.SDL2-CS`, which ships the native SDL2 binaries as NuGet content.

## Package dependencies

| Package | License |
|---|---|
| Avalonia, Avalonia.Android, Avalonia.Browser, Avalonia.Desktop, Avalonia.iOS, Avalonia.Skia, Avalonia.Themes.Fluent | MIT |
| Avalonia.AvaloniaEdit, AvaloniaEdit.TextMate | MIT |
| BenchmarkDotNet | MIT |
| CommunityToolkit.Mvvm | MIT |
| coverlet.collector | MIT |
| Microsoft.Build.\*, Microsoft.CodeAnalysis.\*, Microsoft.Extensions.\*, Microsoft.NET.\* | MIT |
| NUnit, NUnit.Analyzers, NUnit3TestAdapter | MIT |
| ppy.SDL2-CS | MIT (wrapping SDL2, zlib) |
| ReactiveUI.Avalonia, System.Reactive | MIT |
| SkiaSharp and its native asset packages | MIT (wrapping Skia, BSD-3-Clause) |
| System.CodeDom, System.Text.Json | MIT |
| TextMateSharp.Grammars | MIT (grammars carry their own upstream licenses) |
| Tmds.DBus.Protocol | MIT |
| Xamarin.AndroidX.Core.SplashScreen | MIT |

All of the above are compatible with redistributing GameEngine under MIT.

## Assets

Demo art and audio under `GameEngine.Demo/assets/` are covered by
[`GameEngine.Demo/assets/LICENSE`](GameEngine.Demo/assets/LICENSE).
