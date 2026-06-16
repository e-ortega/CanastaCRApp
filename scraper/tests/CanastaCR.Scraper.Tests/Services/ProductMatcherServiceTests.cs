using CanastaCR.Scraper.Services;

namespace CanastaCR.Scraper.Tests.Services;

public class ProductMatcherServiceTests
{
    [Theory]
    [InlineData("Leche Dos Pinos 1L", "leche dos pinos 1l")]
    [InlineData("ACEITE DE GIRASOL 1 LT", "aceite girasol 1l")]
    [InlineData("Arroz Tío Pelón 2 Kg", "arroz tio pelon 2kg")]
    [InlineData("Café Britt Molido 500 Grs", "cafe britt molido 500g")]
    public void Normalize_ProducesExpectedOutput(string input, string expected)
    {
        var result = ProductMatcherService.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_RemovesAccents()
    {
        var result = ProductMatcherService.Normalize("Aceité con Ñ");
        Assert.DoesNotContain("é", result);
        Assert.DoesNotContain("Ñ", result);
    }

    [Fact]
    public void Normalize_CollapsesUnitVariants()
    {
        var lt = ProductMatcherService.Normalize("1 lt");
        var l = ProductMatcherService.Normalize("1l");
        var ltr = ProductMatcherService.Normalize("1 ltr");

        Assert.Equal(lt, l);
        Assert.Equal(l, ltr);
    }

    [Fact]
    public void Normalize_CollapsesGramVariants()
    {
        var gr = ProductMatcherService.Normalize("500 gr");
        var g = ProductMatcherService.Normalize("500g");
        var grs = ProductMatcherService.Normalize("500 grs");

        Assert.Equal(gr, g);
        Assert.Equal(g, grs);
    }
}
