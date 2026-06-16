using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Jobs;
using CanastaCR.Scraper.Persistence;
using CanastaCR.Scraper.Scrapers;
using CanastaCR.Scraper.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Store names — single source of truth shared between scraper registration below and the
// single-store trigger endpoints, so they can never drift out of sync with each other.
const string MaxiPaliStore = "MaxiPalí Alajuela";
const string MasXMenosStore = "Más x Menos La Uruca";
const string WalmartStore = "Walmart San José";
const string MegaSuperStore = "MegaSuper Tibás";
const string PriceSmartStore = "PriceSmart San José";

// Minimal bootstrap logger — only used for errors during host startup, before configuration
// (and the real, fully-configured logger below) is available.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Reads the existing Logging:LogLevel section from appsettings.json/appsettings.Development.json
// (same config every other part of this app already uses) rather than hardcoding levels here —
// otherwise editing appsettings' Logging section silently does nothing, which is exactly the
// kind of trap that makes an incident harder to diagnose, not easier.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // Rolling daily file under scraper/src/CanastaCR.Scraper/logs/ (gitignored). This is what
    // survives after the terminal closes — the section 7 incident's detail trail (why a scrape
    // wrote zero rows) only existed in console scrollback and was gone within minutes. Run
    // `.\scripts\run.ps1 scraper:logs:tail` to follow it live, or open the dated file after the
    // fact. See docs/ARCHITECTURE.md section 9 for the full observability runbook.
    .WriteTo.File(
        path: "logs/scrape-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

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
builder.Services.AddScoped<ScrapeStoreJob>();
builder.Services.AddScoped<ScrapeAllStoresJob>();

// Scrapers — each registered as IStoreScraper
builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["MaxiPaliBaseUrl"] ?? "https://www.maxipali.co.cr";
    return new VtexScraper(MaxiPaliStore, baseUrl, factory.CreateClient("vtex"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["MasXMenosBaseUrl"] ?? "https://www.masxmenos.cr";
    return new VtexScraper(MasXMenosStore, baseUrl, factory.CreateClient("vtex"), logger);
});

builder.Services.AddScoped<IStoreScraper>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VtexScraper>>();
    var baseUrl = scraperConfig["WalmartBaseUrl"] ?? "https://www.walmart.co.cr";
    return new VtexScraper(WalmartStore, baseUrl, factory.CreateClient("vtex"), logger);
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

// maxProducts caps each store's scrape — handy for a quick smoke test before a full nightly run.
// "/" and "/vtex" fan out to multiple independent jobs (one per matching store) via
// ScrapeAllStoresJob. The per-store endpoints below enqueue a single ScrapeStoreJob directly —
// each store can be triggered and tracked completely on its own in the Hangfire dashboard.
api.MapPost("/", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(maxProducts, null, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Fanning out independent jobs for all stores", maxProducts });
});

api.MapPost("/vtex", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeAllStoresJob>(j => j.RunAsync(maxProducts, "vtex", CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Fanning out independent jobs for Walmart + MaxiPalí + Más x Menos", maxProducts });
});

api.MapPost("/maxipali", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeStoreJob>(j => j.RunAsync(MaxiPaliStore, maxProducts, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "MaxiPalí scrape enqueued", maxProducts });
});

api.MapPost("/masxmenos", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeStoreJob>(j => j.RunAsync(MasXMenosStore, maxProducts, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Más x Menos scrape enqueued", maxProducts });
});

api.MapPost("/walmart", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeStoreJob>(j => j.RunAsync(WalmartStore, maxProducts, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "Walmart scrape enqueued", maxProducts });
});

api.MapPost("/megasuper", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeStoreJob>(j => j.RunAsync(MegaSuperStore, maxProducts, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "MegaSuper scrape enqueued", maxProducts });
});

api.MapPost("/pricesmart", (IBackgroundJobClient bgJob, int? maxProducts) =>
{
    bgJob.Enqueue<ScrapeStoreJob>(j => j.RunAsync(PriceSmartStore, maxProducts, CancellationToken.None));
    return Results.Accepted("/api/scrape/status", new { message = "PriceSmart scrape enqueued", maxProducts });
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

// Nightly recurring job — runs at 4 AM Costa Rica time, fans out to all stores in parallel
RecurringJob.AddOrUpdate<ScrapeAllStoresJob>(
    "nightly-scrape",
    job => job.RunAsync(null, null, CancellationToken.None),
    scraperConfig["CronSchedule"] ?? "0 4 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica") });

app.Run();
