using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace GameEngine.Editor.Magic
{
    public sealed record SceneInfo(
        string Name,
        string FullName,
        string FilePath,
        string Source,
        DocumentId DocumentId
    );

    public sealed record SceneCompilationResult(
        Assembly Assembly,
        IReadOnlyList<Type> SceneTypes,
        SceneInfo? UpdatedSceneInfo = null
    );

    // Updated: allow specifying shared assemblies that must bind to Default ALC
    internal sealed class UnloadableSceneLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> _resolved = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _probingPaths;
        private readonly HashSet<string> _shared;

        public UnloadableSceneLoadContext(string[] probingPaths, IEnumerable<string> sharedAssemblyNames)
            : base(isCollectible: true)
        {
            _probingPaths = probingPaths;
            _shared = new HashSet<string>(sharedAssemblyNames, StringComparer.OrdinalIgnoreCase);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // If we mark it shared, let the runtime fall back to default context
            if (_shared.Contains(assemblyName.Name!))
                return null;

            if (_resolved.TryGetValue(assemblyName.Name!, out var asm))
                return asm;

            foreach (var dir in _probingPaths)
            {
                var candidate = Path.Combine(dir, assemblyName.Name + ".dll");
                if (!File.Exists(candidate)) continue;
                try
                {
                    var loaded = LoadFromAssemblyPath(candidate);
                    _resolved[assemblyName.Name!] = loaded;
                    return loaded;
                }
                catch
                {
                    // ignore and continue
                }
            }
            return null; // fallback
        }
    }

    public sealed class SceneProjectCompiler : IAsyncDisposable
    {
        private readonly string _projectPath;
        private MSBuildWorkspace? _workspace;
        private Project? _project;
        private Compilation? _lastCompilation;
        private UnloadableSceneLoadContext? _alc;
        private SceneCompilationResult? _lastResult;
        private bool _initialized;
        private readonly SemaphoreSlim _lock = new(1, 1);

        // Assemblies we never want duplicated
        private static readonly string[] SharedAssemblies =
        {
            "GameEngine.Core",
            "SkiaSharp",
            "Avalonia",
            "Avalonia.Base",
            "Avalonia.Controls",
            "Avalonia.Markup.Xaml",
            "System.Runtime",
            "System.Collections",
            "System.Runtime.InteropServices",
            "System.Text.Json"
        };

        public SceneProjectCompiler(string projectPath) => _projectPath = Path.GetFullPath(projectPath);

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;
            await _lock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                if (!MSBuildLocator.IsRegistered)
                    MSBuildLocator.RegisterDefaults();

                _workspace = MSBuildWorkspace.Create();
                _workspace.WorkspaceFailed += (_, e) =>
                {
                    // Optionally log e.Diagnostic
                };

                _project = await _workspace.OpenProjectAsync(_projectPath, cancellationToken: ct);
                _lastCompilation = await _project.GetCompilationAsync(ct)
                                   ?? throw new InvalidOperationException("Failed to create compilation.");

                _initialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<SceneInfo>> DiscoverScenesAsync(CancellationToken ct = default)
        {
            await EnsureInitialized(ct);
            var compilation = _lastCompilation!;
            var results = new List<SceneInfo>();

            var sceneBase = compilation.GetTypeByMetadataName("GameEngine.Core.Scene");
            if (sceneBase == null) return results;

            foreach (var doc in _project!.Documents)
            {
                ct.ThrowIfCancellationRequested();
                var semanticModel = await doc.GetSemanticModelAsync(ct);
                if (semanticModel == null) continue;
                var root = await semanticModel.SyntaxTree.GetRootAsync(ct);

                var classDecls = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();

                foreach (var decl in classDecls)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(decl, ct) as INamedTypeSymbol;
                    if (symbol is null || symbol.IsAbstract) continue;
                    if (SymbolEqualityComparer.Default.Equals(symbol, sceneBase)) continue;
                    if (!InheritsFrom(symbol, sceneBase)) continue;

                    var text = await doc.GetTextAsync(ct);
                    results.Add(new SceneInfo(
                        symbol.Name,
                        symbol.ToDisplayString(),
                        doc.FilePath ?? string.Empty,
                        text.ToString(),
                        doc.Id
                    ));
                }
            }

            return results;

            static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
            {
                for (var cur = symbol.BaseType; cur != null; cur = cur.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(cur, baseType)) return true;
                }
                return false;
            }
        }

        public async Task<SceneCompilationResult> CompileAsync(CancellationToken ct = default)
        {
            await EnsureInitialized(ct);
            return await EmitAndLoadAsync(_lastCompilation!, null, ct);
        }

        public async Task<SceneCompilationResult> UpdateSceneAsync(SceneInfo scene, string newSource, CancellationToken ct = default)
        {
            await EnsureInitialized(ct);

            var doc = _project!.GetDocument(scene.DocumentId)
                      ?? throw new InvalidOperationException("Document not found.");

            _project = doc.WithText(SourceText.From(newSource)).Project;
            _lastCompilation = await _project.GetCompilationAsync(ct)
                               ?? throw new InvalidOperationException("Recompilation failed.");

            return await EmitAndLoadAsync(_lastCompilation, scene with { Source = newSource }, ct);
        }

        private async Task<SceneCompilationResult> EmitAndLoadAsync(Compilation compilation, SceneInfo? updatedSceneInfo, CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            try
            {
                using var pe = new MemoryStream();
                using var pdb = new MemoryStream();
                var emit = compilation.Emit(pe, pdb, cancellationToken: ct);
                if (!emit.Success)
                {
                    var errors = string.Join(Environment.NewLine,
                        emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                    throw new InvalidOperationException("Compilation failed:" + Environment.NewLine + errors);
                }

                pe.Position = 0;
                pdb.Position = 0;

                // Unload old
                if (_alc != null)
                {
                    _lastResult = null;
                    _alc.Unload();
                    _alc = null;
                }

                var probing = compilation.References
                    .OfType<PortableExecutableReference>()
                    .Select(r => Path.GetDirectoryName(r.FilePath))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .Cast<string>()
                    .ToArray();

                _alc = new UnloadableSceneLoadContext(probing, SharedAssemblies);
                var asm = _alc.LoadFromStream(pe, pdb);

                var sceneTypes = asm.GetTypes()
                    .Where(t => !t.IsAbstract && IsSceneSubclass(t))
                    .ToList();

                _lastResult = new SceneCompilationResult(asm, sceneTypes, updatedSceneInfo);
                return _lastResult;

                static bool IsSceneSubclass(Type t)
                {
                    for (var cur = t.BaseType; cur != null; cur = cur.BaseType)
                    {
                        if (cur.FullName == "GameEngine.Core.Scene")
                            return true;
                    }
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task EnsureInitialized(CancellationToken ct)
        {
            if (!_initialized)
                await InitializeAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _workspace?.Dispose();
                if (_alc != null)
                {
                    _lastResult = null;
                    _alc.Unload();
                    _alc = null;
                }
            }
            finally
            {
                _lock.Release();
                _lock.Dispose();
            }
        }
    }
}