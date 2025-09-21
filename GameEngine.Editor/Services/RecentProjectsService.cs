using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GameEngine.Editor.Services
{
    public interface IRecentProjectsService
    {
        IReadOnlyList<string> Load();
        void Add(string projectPath);
    }

    public sealed class RecentProjectsService : IRecentProjectsService
    {
        private const int MaxItems = 10;
        private readonly string _filePath;
        private readonly object _lock = new();
        private List<string>? _cache;

        public RecentProjectsService()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameEngine.Editor");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "recentProjects.json");
        }

        public IReadOnlyList<string> Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        _cache = [];
                        return _cache;
                    }
                    var json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
                    // Filter out empties / duplicates (case-insensitive) / non-existing optional (keep even if missing, user can see path)
                    _cache = list
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxItems)
                        .ToList();
                    return _cache;
                }
                catch
                {
                    _cache = [];
                    return _cache;
                }
            }
        }

        public void Add(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath)) return;
            lock (_lock)
            {
                var list = Load().ToList();
                list.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, projectPath);
                if (list.Count > MaxItems)
                    list.RemoveRange(MaxItems, list.Count - MaxItems);
                _cache = list;
                try
                {
                    var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_filePath, json);
                }
                catch
                {
                    // Ignore persistence errors silently
                }
            }
        }
    }
}