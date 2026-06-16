using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Jobs;
using CanastaCR.Scraper.Persistence;
using CanastaCR.Scraper.Scrapers;
using CanastaCR.Scraper.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var scraperConfig = builder.Configuration.GetSection("Scraper");

// EF Core — scraper's own context, shared DB
builder.Services.AddDbContext<ScraperDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// Hangfire with PostgreSQL storage
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// HTTP clients
builder.Services.AddHttpClient("vtex", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "CanastaCR-Scraper/1.0 (price comparison research)");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("megasuper", c =>
{
    c.BaseAddress = new Uri("https://www.megasuper.com");
    c.DefaultRequestHeaders.Add("User-Agent", "CanastaCR-Scraper/1.0 (price comparison research)");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("pricesmart", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "CanastaCR-Scraper/1.0 (price comparison research)");
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Core services
builder.Services.AddScoped<ProductMatcherService>();
builder.Services.AddScoped<IScrapeResultStore, PriceWriterService>();
builder.Services.AddScoped<ScrapeAllStoresJob>();

// Scrapers — each registered as IStoreScraper
builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["MaxiPaliBaseUrl"] ?? "https://www.maxipali.co.cr";
    return new VtexScraper("MaxiPalí Alajuela", baseUrl, factory.CreateClient("vtex"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["MasXMenosBaseUrl"] ?? "https://www.masxmenos.cr";
    return new VtexScraper("Más x Menos La Uruca", baseUrl, factory.CreateClient("vtex"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["WalmartBaseUrl"] ?? "https://www.walmart.co.cr";
    return new VtexScraper("Walmart San José", baseUrl, factory.CreateClient("vtex"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<JsonLdScraper>>();
    return new JsonLdScraper(factory.CreateClient("megasuper"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<CommerceToolsScraper>>();
    return new CommerceToolsScraper(factory.CreateClient("pricesmart"), logger);
});

var app = builder.Build();

// Hangfire dashboard — add auth middleware before deploying publicly
app.UseHangfireDashboard("/hangfire");

// Trigger API
var api = app.MapGroup("/api/scrape");

api.MapPost("/", (IBackgroundJobClient bgJob) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Scrape job enqueued — all stores" });
});

api.MapPost("/vtex", (IBackgroundJobClient bgJob) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "VTEX scrape enqueued (Walmart + MaxiPalí + Más x Menos)" });
});

api.MapPost("/walmart", (IBackgroundJobClient bgJob) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Walmart scrape enqueued" });
});

api.MapPost("/megasuper", (IBackgroundJobClient bgJob) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "MegaSuper scrape enqueued" });
});

api.MapPost("/pricesmart", (IBackgroundJobClient bgJob) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "PriceSmart scrape enqueued" });
});

api.MapGet("/status", () =>
{
    var stats = JobStorage.Current.GetMonitoringApi().GetStatistics();
    return Results.Ok(new
    {
        enqueued = stats.Enqueued,
        processing = stats.Processing,
        succeeded = stats.Succeeded,
        failed = stats.Failed,
        scheduled = stats.Scheduled
    });
});

// Nightly recurring job — runs at 4 AM Costa Rica time
RecurringJob.AddOrUpdate<ScrapeAllStoresJob>(
    "nightly-scrape",
    job => job.RunAsync(CancellationToken.None),
    scraperConfig["CronSchedule"] ?? "0 4 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica") });

app.Run();
