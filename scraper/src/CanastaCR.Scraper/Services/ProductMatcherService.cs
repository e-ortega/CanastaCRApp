using CanastaCR.Core.Entities;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CanastaCR.Scraper.Services;

public partial class ProductMatcherService(ScraperDbContext db, ILogger<ProductMatcherService> logger)
{
    private const double AutoMergeThreshold = 0.90;
    private const double StagingThreshold = 0.70;

    public async Task<Product> FindOrCreateProductAsync(ScrapedProduct scraped, CancellationToken ct)
    {
        // Priority 1: EAN / barcode exact match
        if (!string.IsNullOrEmpty(scraped.Barcode))
        {
            var byBarcode = await db.Products
                .FirstOrDefaultAsync(p => p.Barcode == scraped.Barcode, ct);

            if (byBarcode is not null)
                return byBarcode;

            // New product with known barcode — insert directly
            return await CreateProductAsync(scraped, ct);
        }

        // Priority 2: fuzzy name + brand match
        // Candidate fetch uses the raw first word (lowercased only — translatable by EF) rather
        // than a prefix of the fully-Normalize()'d name: stopword removal shifts later words
        // together (e.g. "ACEITE DE GIRASOL" → "aceite girasol"), so a normalized prefix often
        // isn't a contiguous substring of the raw stored name. The first word of a product name
        // is essentially never a stopword, so it survives as a reliable anchor.
        // Normalize() itself must never run inside the Where() below — EF Core cannot translate
        // its Regex/Unicode normalization into SQL.
        var firstWord = scraped.Name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToLowerInvariant() ?? "";

        var candidates = firstWord.Length == 0
            ? []
            : await db.Products
                .Where(p => p.Name.ToLower().Contains(firstWord))
                .Take(20)
                .ToListAsync(ct);

        var best = candidates
            .Select(p => (product: p, score: FuzzyScore(scraped, p)))
            .Where(x => x.score >= StagingThreshold)
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (best.product is not null)
        {
            if (best.score >= AutoMergeThreshold)
            {
                logger.LogDebug("Auto-merged '{Scraped}' → '{Existing}' (score {Score:P0})",
                    scraped.Name, best.product.Name, best.score);
                return best.product;
            }

            logger.LogInformation("Ambiguous match for '{Scraped}' → '{Existing}' (score {Score:P0}); creating new product",
                scraped.Name, best.product.Name, best.score);
        }

        return await CreateProductAsync(scraped, ct);
    }

    // internal (not private) so the duplicate-barcode race-handling path can be exercised
    // directly in tests without needing real thread concurrency — see ProductMatcherServiceMatchingTests
    internal async Task<Product> CreateProductAsync(ScrapedProduct scraped, CancellationToken ct)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Barcode = scraped.Barcode,
            Name = scraped.Name,
            Brand = scraped.Brand,
            Category = scraped.Category,
            ImageUrl = scraped.ImageUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Products.Add(product);

        try
        {
            await db.SaveChangesAsync(ct);
            return product;
        }
        catch (DbUpdateException) when (!string.IsNullOrEmpty(scraped.Barcode))
        {
            // Multiple stores scrape concurrently (each with its own DbContext), and the same
            // EAN-keyed product (e.g. a nationally-distributed brand sold at both MaxiPalí and
            // Más x Menos) can be found "not yet in the DB" by two of them at nearly the same
            // moment — both then try to insert it, and the unique index on Barcode correctly
            // rejects the loser. This is an expected, benign race in a system designed for
            // concurrent writers, not a real failure: detach our half-saved entity and use
            // whichever insert actually won instead of throwing. Confirmed live 2026-06-16 —
            // unhandled, this corrupted the change tracker for the rest of that store's run
            // (every subsequent SaveChanges on this DbContext kept re-throwing the same
            // duplicate-key error, 10,956 times in one incident, ultimately losing the entire
            // store's writes for that run).
            db.Entry(product).State = EntityState.Detached;
            var winner = await db.Products.FirstOrDefaultAsync(p => p.Barcode == scraped.Barcode, ct);
            if (winner is not null)
            {
                logger.LogDebug("Lost insert race for barcode '{Barcode}' — using the concurrently-inserted product instead",
                    scraped.Barcode);
                return winner;
            }
            throw; // not actually a barcode collision — something else is wrong, surface it
        }
    }

    private static double FuzzyScore(ScrapedProduct scraped, Product existing)
    {
        var nameScore = JaccardSimilarity(Normalize(scraped.Name), Normalize(existing.Name));

        if (string.IsNullOrEmpty(scraped.Brand) || string.IsNullOrEmpty(existing.Brand))
            return nameScore;

        var brandMatch = Normalize(scraped.Brand) == Normalize(existing.Brand) ? 1.0 : 0.0;
        return nameScore * 0.75 + brandMatch * 0.25;
    }

    private static double JaccardSimilarity(string a, string b)
    {
        var setA = new HashSet<string>(Tokenize(a));
        var setB = new HashSet<string>(Tokenize(b));
        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static IEnumerable<string> Tokenize(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Where(t => t.Length > 1);

    public static string Normalize(string s)
    {
        // Lowercase, strip accents, collapse unit variants, trim stopwords
        s = s.ToLowerInvariant();
        s = RemoveAccents(s);
        s = UnitNormalizerRegex().Replace(s, m =>
        {
            var num = m.Groups[1].Value;
            // Order matters: more-specific patterns before sub-patterns (grs before gr, ltr before lt)
            var unit = m.Groups[2].Value.ToLower() switch
            {
                "grs" => "g",
                "gr"  => "g",
                "ltr" => "l",
                "lt"  => "l",
                var u => u
            };
            return $"{num}{unit}";
        });
        s = StopWordRegex().Replace(s, " ");
        return CollapseSpacesRegex().Replace(s, " ").Trim();
    }

    private static string RemoveAccents(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(kg|g|gr|grs|ml|l|lt|ltr|cc|oz|lb)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnitNormalizerRegex();

    [GeneratedRegex(@"\b(de|la|el|los|las|un|una|con|sin|para|del)\b")]
    private static partial Regex StopWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseSpacesRegex();
}
