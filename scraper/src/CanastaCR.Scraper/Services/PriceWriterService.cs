using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Scraper.Services;

public class PriceWriterService(
    ScraperDbContext db,
    ProductMatcherService matcher,
    ILogger<PriceWriterService> logger) : IScrapeResultStore
{
    private static readonly TimeSpan PriceExpiry = TimeSpan.FromDays(3);

    public async Task WriteAsync(string storeName, IReadOnlyList<ScrapedProduct> products, CancellationToken ct = default)
    {
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Name == storeName, ct);
        if (store is null)
        {
            logger.LogWarning("Store '{StoreName}' not found in database — skipping write", storeName);
            return;
        }

        var written = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var scraped in products)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var product = await matcher.FindOrCreateProductAsync(scraped, ct);

                // Skip unavailable products but still record them so we know price dropped/gone
                if (!scraped.IsAvailable)
                {
                    logger.LogDebug("Product '{Name}' unavailable at {Store} — skipping price report", scraped.Name, storeName);
                    continue;
                }

                var report = new PriceReport
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    StoreId = store.Id,
                    Price = scraped.Price,
                    Currency = scraped.Currency,
                    Source = PriceSource.Scraped,
                    ReportedAt = now,
                    ExpiresAt = now.Add(PriceExpiry)
                };

                db.PriceReports.Add(report);
                written++;

                // Flush every 100 records to avoid huge in-memory change tracker
                if (written % 100 == 0)
                {
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("{Store}: saved {Count} price reports so far", storeName, written);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write price for '{Name}' at {Store}", scraped.Name, storeName);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Store}: finished — {Written}/{Total} prices written", storeName, written, products.Count);
    }
}
