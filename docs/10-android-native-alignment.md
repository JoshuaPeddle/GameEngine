# Android native alignment report

On October 26, 2026, both delivered signed APKs passed `zipalign -c -P 16 -v 4` and `llvm-readelf -lW` inspection of every packaged native library. No dependency upgrade was required. Debug contains ARM64 libraries. Release contains ARM64 and x86-64 libraries, including every AOT assembly library. Compressed libraries are extracted before loading; their ELF segments must still meet the same 16 KB requirement. There were no exceptions or omitted libraries.

Inspection used Android build-tools 36.0.0 and NDK 27.1.12297006. The Release build used the local .NET SDK 10.0.403 and Android workload 36.1.69; Debug used SDK 10.0.302 with the same Android workload. The artifact filenames are `com.CompanyName.GameEngine.Runner.Avalonia-Signed.apk` under the respective `bin/Debug/net10.0-android` and `bin/Release/net10.0-android` directories. The unsigned intermediate APK is not the delivered artifact and is not zipaligned yet.

Reproduce with `python3 tools/check-android-alignment.py <signed-apk> --zipalign <sdk>/build-tools/36.0.0/zipalign --readelf <ndk>/toolchains/llvm/prebuilt/<host>/bin/llvm-readelf`. The script prints the ZIP verification and full ELF program headers before its summary and fails if any library is incompatible. CI checks the signed output before startup smoke testing and before publishing artifacts.

This proves static artifact alignment. It does not prove execution on a 16 KB emulator or phone, and it does not cover APKs produced from a future AAB. Those need their own inspection and runtime gate. See [Android's 16 KB guidance](https://developer.android.com/guide/practices/page-sizes).

## Debug signed APK

The APK SHA-256 is `519831202104c40e932efe08db97a92709d1e0f26749d7a9577dd23ca236bfec`. The delivered architectures contain 14 libraries for arm64-v8a. The ZIP alignment check passed. Every listed LOAD alignment is at least 16 KB and every file offset is congruent with its virtual address modulo 16 KB.

| Delivered library | LOAD alignments | ZIP storage |
|---|---|---|
| lib/arm64-v8a/libHarfBuzzSharp.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSkiaSharp.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Globalization.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.IO.Compression.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Security.Cryptography.Native.Android.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libarc.bin.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-debugger.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-hot_reload.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-marshal-ilgen.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonodroid.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonosgen-2.0.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libxamarin-app.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libxamarin-debug-app-helper.so | 0x4000, 0x4000, 0x4000 | compressed |

## Release signed APK

The APK SHA-256 is `23b4e39841774b194ceb2000f72260123b2221dca3cbbcc44ea9e113547abdbc`. The delivered architectures contain 79 libraries for arm64-v8a, 79 libraries for x86_64. The ZIP alignment check passed. Every listed LOAD alignment is at least 16 KB and every file offset is congruent with its virtual address modulo 16 KB.

| Delivered library | LOAD alignments | ZIP storage |
|---|---|---|
| lib/arm64-v8a/libHarfBuzzSharp.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSkiaSharp.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Globalization.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.IO.Compression.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Security.Cryptography.Native.Android.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Base.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Controls.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.DesignerSupport.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.HarfBuzz.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Markup.Xaml.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Metal.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.OpenGL.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Remote.Protocol.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Skia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.Vulkan.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Avalonia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-CommunityToolkit.Mvvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-GameEngine.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-GameEngine.Demo.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-GameEngine.Runner.Avalonia.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-GameEngine.Runner.Avalonia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-HarfBuzzSharp.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Java.Interop.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Mono.Android.Runtime.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Mono.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-SkiaSharp.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Collections.Concurrent.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Collections.NonGeneric.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Collections.Specialized.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Collections.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.ComponentModel.Primitives.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.ComponentModel.TypeConverter.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.ComponentModel.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Console.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Diagnostics.DiagnosticSource.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.IO.Compression.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.IO.Pipelines.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Linq.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Memory.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Net.NameResolution.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Net.Primitives.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Net.Sockets.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.ObjectModel.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Private.CoreLib.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Private.Uri.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Private.Xml.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Runtime.InteropServices.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Runtime.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Security.Cryptography.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Text.Encodings.Web.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Text.Json.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Text.RegularExpressions.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Threading.Thread.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.Threading.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-System.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Activity.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.AppCompat.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.CustomView.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.DrawerLayout.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Fragment.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Lifecycle.Common.Jvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Lifecycle.LiveData.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Lifecycle.ViewModel.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Loader.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.NavigationEvent.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.SavedState.SavedState.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.AndroidX.Window.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.Kotlin.StdLib.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-Xamarin.KotlinX.Coroutines.Core.Jvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libaot-_Microsoft.Android.Resource.Designer.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libarc.bin.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libassembly-store.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-marshal-ilgen.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonodroid.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonosgen-2.0.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libxamarin-app.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libHarfBuzzSharp.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libSkiaSharp.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libSystem.Globalization.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libSystem.IO.Compression.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libSystem.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libSystem.Security.Cryptography.Native.Android.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Base.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Controls.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.DesignerSupport.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.HarfBuzz.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Markup.Xaml.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Metal.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.OpenGL.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Remote.Protocol.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Skia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.Vulkan.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Avalonia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-CommunityToolkit.Mvvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-GameEngine.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-GameEngine.Demo.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-GameEngine.Runner.Avalonia.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-GameEngine.Runner.Avalonia.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-HarfBuzzSharp.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Java.Interop.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Mono.Android.Runtime.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Mono.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-SkiaSharp.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Collections.Concurrent.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Collections.NonGeneric.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Collections.Specialized.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Collections.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.ComponentModel.Primitives.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.ComponentModel.TypeConverter.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.ComponentModel.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Console.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Diagnostics.DiagnosticSource.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.IO.Compression.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.IO.Pipelines.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Linq.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Memory.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Net.NameResolution.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Net.Primitives.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Net.Sockets.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.ObjectModel.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Private.CoreLib.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Private.Uri.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Private.Xml.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Runtime.InteropServices.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Runtime.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Security.Cryptography.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Text.Encodings.Web.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Text.Json.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Text.RegularExpressions.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Threading.Thread.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.Threading.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-System.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Activity.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.AppCompat.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.CustomView.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.DrawerLayout.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Fragment.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Lifecycle.Common.Jvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Lifecycle.LiveData.Core.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Lifecycle.ViewModel.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Loader.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.NavigationEvent.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.SavedState.SavedState.Android.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.AndroidX.Window.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.Kotlin.StdLib.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-Xamarin.KotlinX.Coroutines.Core.Jvm.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libaot-_Microsoft.Android.Resource.Designer.dll.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libarc.bin.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libassembly-store.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libmono-component-marshal-ilgen.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libmonodroid.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libmonosgen-2.0.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/x86_64/libxamarin-app.so | 0x4000, 0x4000, 0x4000 | compressed |

## Copied template Debug signed APK

The copied template consumed locally packed engine packages and produced `com.example.GameTemplate-Signed.apk`. Its SHA-256 is `1b8a905d1a30604bbe42714e8dd29a3c7e1b616f4ea84a9a00e1acc1009ff510`. ZIP alignment and all LOAD segments in its 14 ARM64 native libraries passed the same checks.

| Delivered library | LOAD alignments | ZIP storage |
|---|---|---|
| lib/arm64-v8a/libHarfBuzzSharp.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSkiaSharp.so | 0x4000, 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Globalization.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.IO.Compression.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Native.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libSystem.Security.Cryptography.Native.Android.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libarc.bin.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-debugger.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-hot_reload.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmono-component-marshal-ilgen.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonodroid.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libmonosgen-2.0.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libxamarin-app.so | 0x4000, 0x4000, 0x4000 | compressed |
| lib/arm64-v8a/libxamarin-debug-app-helper.so | 0x4000, 0x4000, 0x4000 | compressed |
