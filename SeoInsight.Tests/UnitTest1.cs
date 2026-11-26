using System.Text.Json;
using SeoInsight.Web.Infrastructure.Data;
using SeoInsight.Web.Models.Dtos;
using SeoInsight.Web.Services.Implementations;
using Xunit;

namespace SeoInsight.Tests;

public class UnitTest1
{
    [Fact]
    public void TestFlexiblePositionStringConverter_With50Plus_Returns50Plus()
    {
        var json = @"{ ""baselinePosition"": ""50+"", ""projectedPosition"": 4.5, ""confidenceScore"": 0.85 }";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<RankPredictionDto>(json, options);

        Assert.NotNull(result);
        Assert.Equal("50+", result.BaselinePosition);
        Assert.Equal(4.5, result.ProjectedPosition);
    }

    [Fact]
    public void TestFlexiblePositionStringConverter_WithAbove50_Returns50Plus()
    {
        var json = @"{ ""baselinePosition"": ""Above 50"", ""projectedPosition"": 4.5, ""confidenceScore"": 0.85 }";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<RankPredictionDto>(json, options);

        Assert.NotNull(result);
        Assert.Equal("50+", result.BaselinePosition);
    }

    [Fact]
    public void TestFlexiblePositionStringConverter_WithNumericRank_PreservesRank()
    {
        var json = @"{ ""baselinePosition"": 14.5, ""projectedPosition"": 4.0, ""confidenceScore"": 0.90 }";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<RankPredictionDto>(json, options);

        Assert.NotNull(result);
        Assert.Equal("14.5", result.BaselinePosition);
    }

    [Fact]
    public void TestBuildPrompt_WhenUnrankedIn5Loops_Sets50Plus()
    {
        var input = new RagInputDto
        {
            Keyword = "cloud erp",
            Location = "US",
            TargetWebsite = "mycompany.com",
            OwnRankPosition = null,
            TopCompetitors = new List<RagCompetitorItemDto>
            {
                new() { Position = 1, Domain = "competitor.com", Url = "https://competitor.com" }
            }
        };

        var prompt = RagAnalysisService.BuildPrompt(input);

        Assert.Contains("50+", prompt);
        Assert.Contains("\"currentPosition\": \"50+\"", prompt);
        Assert.Contains("\"baselinePosition\": \"50+\"", prompt);
    }
}

