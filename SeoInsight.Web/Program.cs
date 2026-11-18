using Serilog;
using SeoInsight.Web.Extensions;
using SeoInsight.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog (Console + File)
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File("Logs/seoinsight-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
});

// Configure Options with validation
builder.Services.AddSeoInsightConfiguration(builder.Configuration);

// HTTP clients with Polly resilience
builder.Services.AddSeoInsightHttpClients();

// Core Services: SERP, Scraper, Chunking, Vector Store, Repositories
builder.Services.AddSeoInsightCoreServices();

// Health checks
builder.Services.AddHealthChecks();

// MVC Controllers with Views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Global Exception & Correlation ID Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
