using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GameEngine.Editor.Magic
{
    //_gameEngine.ChangeScene((Scene)Activator.CreateInstance(new Magic.SceneCompiler().CompiledSceneType!));
    public class SceneCompiler
    {
        private const string Code = @"
using System;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Editor.ViewModels;

class SceneLevelEditor : Scene
{
    public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
    {
        var secondEntity = entityManager.CreateEntity(""background"");
        secondEntity.AddComponent(new CTransform(new Vec2(500, 300)));
        secondEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), true, true));
    }
}
";

        public Type? CompiledSceneType { get; }

        public Assembly? CompiledAssembly { get; }

        public SceneCompiler()
        {
            // 1. Parse source
            var syntaxTree = CSharpSyntaxTree.ParseText(Code);

            // 2. Collect metadata references from already loaded assemblies (filter those without a physical location)
            var references =
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Distinct()
                    .Select(a =>
                    {
                        try { return (MetadataReference)MetadataReference.CreateFromFile(a.Location); }
                        catch { return null; }
                    })
                    .Where(r => r is not null)!
                    .ToList();

            // Fallback (in case nothing was captured)
            if (references.Count == 0)
            {
                references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            }

            // 3. Create compilation
            var compilation = CSharpCompilation.Create(
                assemblyName: "SceneLevelEditor.Dynamic",
                syntaxTrees: new[] { syntaxTree },
                references: references!,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release));

            // 4. Emit to in-memory stream
            using var peStream = new MemoryStream();
            var emitResult = compilation.Emit(peStream);

            if (!emitResult.Success)
            {
                var errors = string.Join(Environment.NewLine,
                    emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.ToString()));
                throw new InvalidOperationException("Scene compilation failed:" + Environment.NewLine + errors);
            }

            peStream.Position = 0;

            // 5. Load assembly (AssemblyLoadContext.Default is fine if you want reuse; for unloading create a custom ALC)
            CompiledAssembly = AssemblyLoadContext.Default.LoadFromStream(peStream);

            // 6. Get the generated type (global namespace)
            CompiledSceneType = CompiledAssembly.GetType("SceneLevelEditor");
        }
    }
}
