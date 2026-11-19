using SeoInsight.Web.Models.Dtos;

namespace SeoInsight.Web.Services.Interfaces;

public interface IRagAnalysisService
{
    Task<SeoAnalysisOutputDto> RunCompetitiveAnalysisAsync(RagInputDto ragInput, CancellationToken ct = default);
}
