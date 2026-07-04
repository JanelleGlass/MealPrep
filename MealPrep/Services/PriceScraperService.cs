using System.Text.Json;
using System.Text.RegularExpressions;

namespace MealPrep.Services;

public record ScrapedPrice(decimal Price, string? ProductName);

public class PriceScraperService
{
    private readonly HttpClient _http;

    public PriceScraperService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ScrapedPrice?> ScrapePriceAsync(string url, string storeType)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept", "text/html");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync();

            // 1. Try JSON-LD structured data (works for any store)
            var jsonLdResult = TryParseJsonLd(html);
            if (jsonLdResult != null) return jsonLdResult;

            // 2. Try meta tags (works for any store)
            var metaResult = TryParseMetaTags(html);
            if (metaResult != null) return metaResult;

            // 3. Try store-specific fallback
            if (storeType.Equals("walmart", StringComparison.OrdinalIgnoreCase))
            {
                var walmartResult = TryParseWalmart(html);
                if (walmartResult != null) return walmartResult;
            }

            // 4. Try generic fallback (common e-commerce patterns)
            return TryParseGeneric(html);
        }
        catch
        {
            return null;
        }
    }

    private static ScrapedPrice? TryParseJsonLd(string html)
    {
        var pattern = @"<script[^>]*type=""application/ld\+json""[^>]*>(.*?)</script>";
        var matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            try
            {
                var json = match.Groups[1].Value.Trim();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var elements = root.ValueKind == JsonValueKind.Array
                    ? root.EnumerateArray().ToList()
                    : new List<JsonElement> { root };

                foreach (var el in elements)
                {
                    if (!el.TryGetProperty("@type", out var typeProp)) continue;
                    var type = typeProp.GetString();
                    if (type != "Product") continue;

                    string? name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

                    if (el.TryGetProperty("offers", out var offers))
                    {
                        var offer = offers.ValueKind == JsonValueKind.Array
                            ? offers.EnumerateArray().FirstOrDefault()
                            : offers;

                        if (offer.ValueKind != JsonValueKind.Undefined &&
                            offer.TryGetProperty("price", out var priceProp))
                        {
                            decimal price;
                            if (priceProp.ValueKind == JsonValueKind.Number)
                                price = priceProp.GetDecimal();
                            else if (decimal.TryParse(priceProp.GetString(), out var parsed))
                                price = parsed;
                            else
                                continue;

                            return new ScrapedPrice(price, name);
                        }
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private static ScrapedPrice? TryParseMetaTags(string html)
    {
        var priceMatch = Regex.Match(html, @"<meta[^>]*property=""product:price:amount""[^>]*content=""([^""]+)""", RegexOptions.IgnoreCase);
        if (!priceMatch.Success)
            priceMatch = Regex.Match(html, @"<meta[^>]*content=""([^""]+)""[^>]*property=""product:price:amount""", RegexOptions.IgnoreCase);

        if (priceMatch.Success && decimal.TryParse(priceMatch.Groups[1].Value, out var price))
        {
            var nameMatch = Regex.Match(html, @"<meta[^>]*property=""og:title""[^>]*content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (!nameMatch.Success)
                nameMatch = Regex.Match(html, @"<meta[^>]*content=""([^""]+)""[^>]*property=""og:title""", RegexOptions.IgnoreCase);

            return new ScrapedPrice(price, nameMatch.Success ? nameMatch.Groups[1].Value : null);
        }
        return null;
    }

    private static ScrapedPrice? TryParseWalmart(string html)
    {
        var name = ExtractTitle(html);

        // itemprop="price" with content attribute
        var match = Regex.Match(html, @"<[^>]*itemprop=""price""[^>]*content=""([^""]+)""", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var price))
            return new ScrapedPrice(price, name);

        // data-price attribute
        match = Regex.Match(html, @"data-price=""(\d+\.?\d*)""", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out price))
            return new ScrapedPrice(price, name);

        // Walmart price span patterns (price-characteristic / price-mantissa)
        match = Regex.Match(html, @"<span[^>]*class=""[^""]*price-characteristic[^""]*""[^>]*>(\d+)</span>", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var dollars = match.Groups[1].Value;
            var centsMatch = Regex.Match(html, @"<span[^>]*class=""[^""]*price-mantissa[^""]*""[^>]*>(\d+)</span>", RegexOptions.IgnoreCase);
            var cents = centsMatch.Success ? centsMatch.Groups[1].Value : "00";
            if (decimal.TryParse($"{dollars}.{cents}", out price))
                return new ScrapedPrice(price, name);
        }

        return null;
    }

    private static ScrapedPrice? TryParseGeneric(string html)
    {
        var name = ExtractTitle(html);

        // itemprop="price" with content attribute
        var match = Regex.Match(html, @"<[^>]*itemprop=""price""[^>]*content=""([^""]+)""", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var price))
            return new ScrapedPrice(price, name);

        // itemprop="price" with inner text
        match = Regex.Match(html, @"<[^>]*itemprop=""price""[^>]*>\s*\$?(\d+\.?\d*)", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out price))
            return new ScrapedPrice(price, name);

        // data-price attribute
        match = Regex.Match(html, @"data-price=""(\d+\.?\d*)""", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out price))
            return new ScrapedPrice(price, name);

        // Common price class patterns
        match = Regex.Match(html, @"<[^>]*class=""[^""]*(?:product[_-]?price|current[_-]?price|sale[_-]?price|regular[_-]?price)[^""]*""[^>]*>\s*\$?(\d+\.\d{2})", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out price))
            return new ScrapedPrice(price, name);

        // Last resort: $X.XX regex
        match = Regex.Match(html, @"\$(\d+\.\d{2})", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out price))
            return new ScrapedPrice(price, name);

        return null;
    }

    private static string? ExtractTitle(string html)
    {
        var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
        return titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : null;
    }
}
