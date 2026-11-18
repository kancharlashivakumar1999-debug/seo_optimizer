using System.Collections.Concurrent;
using System.Text.Json;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Repositories.Interfaces;

namespace SeoInsight.Web.Repositories.Implementations;

/// <summary>
/// In-Memory and JSON file persistent repository for Phase 1.
/// </summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<string, RunReport> _cache = new();
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public TaskRepository(IWebHostEnvironment env)
    {
        var appData = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "reports.json");
        LoadFromFile();
    }

    public Task<List<RunReport>> GetAllReportsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_cache.Values.OrderByDescending(r => r.StartedAtUtc).ToList());
    }

    public Task<RunReport?> GetReportByIdAsync(string id, CancellationToken ct = default)
    {
        _cache.TryGetValue(id, out var report);
        return Task.FromResult(report);
    }

    public async Task SaveReportAsync(RunReport report, CancellationToken ct = default)
    {
        _cache[report.Id] = report;
        await SaveToFileAsync();
    }

    private void LoadFromFile()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<RunReport>>(json);
            if (list != null)
            {
                foreach (var r in list)
                {
                    _cache[r.Id] = r;
                }
            }
        }
        catch { }
    }

    private async Task SaveToFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_cache.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
