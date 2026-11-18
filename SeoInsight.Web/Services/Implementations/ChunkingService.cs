using Microsoft.Extensions.Options;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed class ChunkingService : IChunkingService
{
    private readonly IOptionsMonitor<RagOptions> _ragOptions;

    public ChunkingService(IOptionsMonitor<RagOptions> ragOptions)
    {
        _ragOptions = ragOptions;
    }

    public List<ContentChunk> ChunkPage(CompetitorPage page, string keyword, string source, int rankPosition)
    {
        var chunks = new List<ContentChunk>();
        var opt = _ragOptions.CurrentValue;
        int maxTokens = opt.ChunkSizeTokens > 0 ? opt.ChunkSizeTokens : 700;
        int overlapPercent = opt.ChunkOverlapPercent;
        int approxWordsPerChunk = (int)(maxTokens * 0.75);
        int overlapWords = (int)(approxWordsPerChunk * (overlapPercent / 100.0));

        // Create meta chunk with Title and Meta Description
        var metaContent = $"Page Title: {page.Title}. Meta Description: {page.MetaDescription}";
        chunks.Add(new ContentChunk
        {
            Url = page.Url,
            Domain = page.Domain,
            Keyword = keyword,
            Source = source,
            RankPosition = rankPosition,
            SectionHeading = "Title & Meta",
            Content = metaContent,
            TokenEstimate = (int)(metaContent.Length / 4.0),
            FetchedDate = page.ScrapedAtUtc
        });

        // Split body content by headings or word windows
        var words = page.CleanedBodyText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return chunks;
        }

        int step = Math.Max(1, approxWordsPerChunk - overlapWords);
        for (int i = 0; i < words.Length; i += step)
        {
            var slice = words.Skip(i).Take(approxWordsPerChunk).ToArray();
            var text = string.Join(" ", slice);

            // Find closest heading
            var heading = page.Headings.FirstOrDefault(h => text.Contains(h.Text, StringComparison.OrdinalIgnoreCase))?.Text
                          ?? (page.Headings.Count > 0 ? page.Headings[0].Text : "Main Content");

            chunks.Add(new ContentChunk
            {
                Url = page.Url,
                Domain = page.Domain,
                Keyword = keyword,
                Source = source,
                RankPosition = rankPosition,
                SectionHeading = heading,
                Content = text,
                TokenEstimate = (int)(text.Length / 4.0),
                FetchedDate = page.ScrapedAtUtc
            });

            if (i + approxWordsPerChunk >= words.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
