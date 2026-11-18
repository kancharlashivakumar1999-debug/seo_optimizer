using System.Collections.Concurrent;
using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Repositories.Interfaces;

public interface ITaskRepository
{
    Task<List<RunReport>> GetAllReportsAsync(CancellationToken ct = default);
    Task<RunReport?> GetReportByIdAsync(string id, CancellationToken ct = default);
    Task SaveReportAsync(RunReport report, CancellationToken ct = default);
}
