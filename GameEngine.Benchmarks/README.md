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

## GE-23 — component storage

`Entity.Components` is a `ConcurrentDictionary<Type, Component>`. The editor no longer
reads components off the engine thread, so the only remaining question is what the
concurrent type costs. `ComponentStorageBenchmarks` exercises both types with the access
patterns `Entity` actually uses — 1000 stores of three components each.

Apple M4, macOS 26.5, .NET 10. Creation and enumeration are `ShortRun`; lookups were
re-run with the default job because they were the one figure arguing against a swap.

| Pattern | `ConcurrentDictionary` | `Dictionary` | Verdict |
| --- | ---: | ---: | --- |
| Create 1000 stores | 162.4 us / 1,104,000 B | 34.6 us / 376,000 B | Dictionary — 4.7x faster, 728 B/entity less |
| 4000 lookups | 13.97 us / 0 B | 17.41 us / 0 B | **Concurrent — ~20% faster** |
| Enumerate keys | 73.1 us / 104,000 B | 3.86 us / 0 B | Dictionary — 19x faster, allocation-free |

Two of the three favour the plain dictionary, and the lookup result is real rather than
noise — repeated with tight error bars.

The reason enumeration is so lopsided: `ConcurrentDictionary.Keys` materialises a snapshot
list on every access. `EntityManager.Update` walks `entity.Components.Keys` for each removed
entity, and `Entity.Capture()` does the same per pick, so that allocation is on live paths.

**The answer is workload-dependent.** A static scene does far more lookups than creations
or removals, where the concurrent type is marginally ahead (~3.4 us per frame at 1000
entities — negligible). A spawn-heavy scene pays 728 bytes and ~128 ns per entity created,
plus the enumeration cost on every despawn. Deciding it properly means measuring the swap
end to end against an entity-churn benchmark, not just these microbenchmarks.

### End-to-end A/B

`EntityChurnBenchmarks` and `EngineBenchmarks` measured through the real `EntityManager`
and `Entity` accessors, both variants in one session on the same machine:

| Benchmark | `ConcurrentDictionary` | `Dictionary` | Change |
| --- | ---: | ---: | ---: |
| Spawn 1000 entities | 401.2 us / 1,651,580 B | 208.5 us / 843,559 B | -48% time, -49% bytes |
| Spawn then despawn 1000 | 535.1 us / 1,772,151 B | 254.9 us / 860,127 B | -52% time, -51% bytes |
| Steady-state lookups (4000) | 12.69 us | 12.09 us | -5% |
| BuildRenderSnapshot | 159.7 us | 153.0 us | -4% |
| PhysicsUpdate | 75.65 us | 71.62 us | -5% |

**Every path improves, including lookups.** The concurrent type's read advantage in the
isolated microbenchmark does not survive going through `TryGetComponent<T>()` on real
entities — different working set, and `typeof(T)` is a constant there. That reversal is why
the end-to-end run mattered: the microbenchmark alone would have argued for keeping it.

`Entity.Components` is a plain `Dictionary` as of this measurement.

*Note on comparing runs:* the dependency-upgrade table above was recorded in an earlier
session and its absolute numbers are not comparable with these — the same
`BuildRenderSnapshot` benchmark measures 300 us there and 160 us here on unchanged rendering
code. Always measure both sides of a comparison in one sitting.
