using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GameEngine.Editor.Magic
{
    public sealed record CompileDiagnostic(
        string Id,
        string Severity,
        string Message,
        string FilePath,
        int Line,
        int Column)
    {
        public string Location => FilePath.Length == 0
            ? string.Empty
            : $"{Path.GetFileName(FilePath)}({Line},{Column})";

        public bool IsError => Severity == "error";

        public override string ToString() =>
            Location.Length == 0
                ? $"{Severity} {Id}: {Message}"
                : $"{Location}: {Severity} {Id}: {Message}";

        public static CompileDiagnostic From(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetLineSpan();
            var start = span.StartLinePosition;

            return new CompileDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.GetMessage(),
                diagnostic.Location.IsInSource ? span.Path : string.Empty,
                start.Line + 1,
                start.Character + 1);
        }
    }

    // Carries the diagnostics rather than flattening them into a string: the editor is a
    // compile-and-preview tool, so what the compiler said is the thing the user needs to see.
    public sealed class SceneCompilationException : Exception
    {
        public SceneCompilationException(IReadOnlyList<CompileDiagnostic> diagnostics)
            : base(FormatMessage(diagnostics))
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<CompileDiagnostic> Diagnostics { get; }

        private static string FormatMessage(IReadOnlyList<CompileDiagnostic> diagnostics)
        {
            var errors = diagnostics.Where(d => d.IsError).ToList();
            var headline = errors.Count == 1 ? "1 compile error" : $"{errors.Count} compile errors";

            return string.Join(Environment.NewLine,
                new[] { headline + ":" }.Concat(errors.Select(d => "  " + d)));
        }
    }
}
