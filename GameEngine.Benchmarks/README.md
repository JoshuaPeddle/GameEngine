# GameEngine performance benchmarks

Run the engine, physics, and Skia render microbenchmarks in Release mode:

```shell
dotnet run -c Release --project GameEngine.Benchmarks -- --join
```

BenchmarkDotNet writes detailed artifacts under `BenchmarkDotNet.Artifacts/`.
Results are machine-specific; compare dependency revisions on the same machine and
look at both mean time and allocated bytes.

## Dependency upgrade measurements

Measured with BenchmarkDotNet's `ShortRun` job on an Apple M4 running macOS 26.5
and .NET 10.0. The baseline is `master` with SkiaSharp 2.88.9; the upgraded run
uses SkiaSharp 4.151.0 and linear, non-mipmapped sampling for frame-sized sprites.

| Benchmark | Baseline | Upgraded | Change | Allocated |
| --- | ---: | ---: | ---: | ---: |
| BuildRenderSnapshot | 289.5 us | 300.0 us | +3.6% | 64 B |
| PhysicsUpdate | 142.0 us | 140.6 us | -1.0% | 0 B |
| DrawFrame | 38.44 ms | 11.63 ms | -69.8% | 101.56 KB |

The engine and physics differences are within short-run noise. The render result is
the meaningful change: it removes the expensive cubic resampling that had been
applied to every same-sized animation frame while retaining linear filtering.
