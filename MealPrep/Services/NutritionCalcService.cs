using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public enum NutrientItemStatus
{
    Counted,
    CountedApprox,
    Negligible,
    NoNutritionLink,
    NoConversion
}

public class NutrientItemResult
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public NutrientItemStatus Status { get; set; }
    public decimal? Grams { get; set; }
    public decimal Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FiberG { get; set; }
    public decimal IronMg { get; set; }
}

public class NutrientComputation
{
    public decimal Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FiberG { get; set; }
    public decimal IronMg { get; set; }
    public List<NutrientItemResult> Items { get; set; } = new();
    public IEnumerable<NutrientItemResult> Uncounted =>
        Items.Where(i => i.Status is NutrientItemStatus.NoNutritionLink or NutrientItemStatus.NoConversion);
    public bool HasUncounted => Uncounted.Any();
    public bool HasApprox => Items.Any(i => i.Status == NutrientItemStatus.CountedApprox);
    public string? UncountedNote { get; set; }
}

public class NutritionCalcService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public NutritionCalcService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    private static readonly Dictionary<string, decimal> MassUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = 1m,
        ["kg"] = 1000m,
        ["oz"] = 28.3495m,
        ["lb"] = 453.592m,
    };

    private static readonly Dictionary<string, decimal> VolumeUnitsMl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tsp"] = 4.92892m,
        ["tbsp"] = 14.7868m,
        ["fl oz"] = 29.5735m,
        ["cup"] = 236.588m,
        ["pt"] = 473.176m,
        ["qt"] = 946.353m,
        ["gal"] = 3785.41m,
        ["ml"] = 1m,
        ["l"] = 1000m,
    };

    private static readonly HashSet<string> NegligibleUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "pinch", "dash", "to taste"
    };

    // GmWt descriptor keyword -> ml, for deriving density from descriptors like "1 cup, diced"
    private static readonly Dictionary<string, decimal> VolumeKeywordsMl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tsp"] = 4.92892m, ["teaspoon"] = 4.92892m,
        ["tbsp"] = 14.7868m, ["tablespoon"] = 14.7868m,
        ["fl oz"] = 29.5735m,
        ["cup"] = 236.588m,
        ["pt"] = 473.176m, ["pint"] = 473.176m,
        ["qt"] = 946.353m, ["quart"] = 946.353m,
        ["gal"] = 3785.41m, ["gallon"] = 3785.41m,
        ["ml"] = 1m, ["milliliter"] = 1m,
        ["l"] = 1000m, ["liter"] = 1000m, ["litre"] = 1000m,
    };

    // Count-style unit -> descriptor keywords that mean "one of this unit"
    private static readonly Dictionary<string, string[]> CountUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["whole"] = new[] { "whole", "medium", "each", "fruit", "egg", "large", "small", "potato", "onion", "pepper", "banana", "apple", "muffin", "tortilla", "patty", "link" },
        ["slice"] = new[] { "slice" },
        ["clove"] = new[] { "clove" },
        ["piece"] = new[] { "piece", "each", "whole", "medium" },
        ["can"] = new[] { "can" },
        ["bunch"] = new[] { "bunch" },
        ["sprig"] = new[] { "sprig" },
        ["head"] = new[] { "head" },
        ["stalk"] = new[] { "stalk" },
    };

    public static decimal? TryConvertToGrams(decimal quantity, string unit, Nutrition n)
    {
        unit = (unit ?? string.Empty).Trim();

        if (NegligibleUnits.Contains(unit)) return 0m;

        if (MassUnits.TryGetValue(unit, out var gramsPerUnit))
            return quantity * gramsPerUnit;

        if (VolumeUnitsMl.TryGetValue(unit, out var mlPerUnit))
        {
            var density = DeriveDensityGPerMl(n);
            return density is decimal d ? quantity * mlPerUnit * d : null;
        }

        // Count-style or unknown unit: try to match a GmWt descriptor keyword
        var keywords = CountUnitKeywords.TryGetValue(unit, out var kw) ? kw : null;
        foreach (var (gmWt, desc) in GmWtPairs(n))
        {
            var parsed = ParseGmWtDesc(desc);
            if (parsed is null) continue;
            if (keywords != null && keywords.Contains(parsed.Value.Keyword, StringComparer.OrdinalIgnoreCase))
                return quantity * (gmWt / parsed.Value.Count);
        }

        // Fallback: GmWt_1 as the weight of one serving/unit (approximate)
        if (n.GmWt_1 is decimal g1 && g1 > 0)
            return quantity * g1;

        return null;
    }

    // True when TryConvertToGrams would have used the GmWt_1-as-1-serving fallback
    public static bool IsApproxConversion(string unit, Nutrition n)
    {
        unit = (unit ?? string.Empty).Trim();
        if (NegligibleUnits.Contains(unit) || MassUnits.ContainsKey(unit) || VolumeUnitsMl.ContainsKey(unit))
            return false;
        var keywords = CountUnitKeywords.TryGetValue(unit, out var kw) ? kw : null;
        foreach (var (_, desc) in GmWtPairs(n))
        {
            var parsed = ParseGmWtDesc(desc);
            if (parsed is null) continue;
            if (keywords != null && keywords.Contains(parsed.Value.Keyword, StringComparer.OrdinalIgnoreCase))
                return false;
        }
        return n.GmWt_1 is decimal g1 && g1 > 0;
    }

    private static IEnumerable<(decimal GmWt, string Desc)> GmWtPairs(Nutrition n)
    {
        if (n.GmWt_1 is decimal g1 && g1 > 0 && !string.IsNullOrWhiteSpace(n.GmWt_Desc1))
            yield return (g1, n.GmWt_Desc1!);
        if (n.GmWt_2 is decimal g2 && g2 > 0 && !string.IsNullOrWhiteSpace(n.GmWt_Desc2))
            yield return (g2, n.GmWt_Desc2!);
    }

    private static decimal? DeriveDensityGPerMl(Nutrition n)
    {
        foreach (var (gmWt, desc) in GmWtPairs(n))
        {
            var parsed = ParseGmWtDesc(desc);
            if (parsed is null) continue;
            var key = parsed.Value.Keyword;
            // "fl oz" arrives as keyword "fl" with the next token "oz" folded in by the parser
            if (VolumeKeywordsMl.TryGetValue(key, out var ml) && ml > 0)
                return gmWt / (parsed.Value.Count * ml);
        }
        return null;
    }

    // "1 cup, diced" -> (1, "cup"); ".5 cup" -> (0.5, "cup"); "1 pat,  (1"" sq..." -> (1, "pat")
    public static (decimal Count, string Keyword)? ParseGmWtDesc(string? desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return null;
        var head = desc.Split(',')[0].Trim();
        if (head.Length == 0) return null;

        var tokens = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        decimal count = 1m;
        int i = 0;

        if (i < tokens.Length && TryParseNumber(tokens[i], out var whole))
        {
            count = whole;
            i++;
            // mixed fraction: "1 1/2 cup"
            if (i < tokens.Length && tokens[i].Contains('/') && TryParseNumber(tokens[i], out var frac))
            {
                count += frac;
                i++;
            }
        }

        if (i >= tokens.Length) return null;
        var keyword = tokens[i].ToLowerInvariant().Trim('.', ')', '(');
        // "fl oz" is two tokens
        if (keyword == "fl" && i + 1 < tokens.Length && tokens[i + 1].StartsWith("oz", StringComparison.OrdinalIgnoreCase))
            keyword = "fl oz";
        if (keyword.Length > 1 && keyword.EndsWith('s')) keyword = keyword[..^1];
        if (keyword.Length == 0 || count <= 0) return null;
        return (count, keyword);
    }

    private static bool TryParseNumber(string token, out decimal value)
    {
        value = 0m;
        if (token.Contains('/'))
        {
            var parts = token.Split('/');
            if (parts.Length == 2
                && decimal.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var num)
                && decimal.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var den)
                && den != 0)
            {
                value = num / den;
                return true;
            }
            return false;
        }
        return decimal.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    public static NutrientComputation Compute(IEnumerable<(Ingredient Ingredient, decimal Quantity)> items)
    {
        var result = new NutrientComputation();
        foreach (var (ing, qty) in items)
        {
            var item = new NutrientItemResult
            {
                IngredientId = ing.Id,
                Name = ing.Name,
                Quantity = qty,
                Unit = ing.Unit,
            };

            var n = ing.Nutrition;
            if (n is null)
            {
                item.Status = NutrientItemStatus.NoNutritionLink;
            }
            else
            {
                var grams = TryConvertToGrams(qty, ing.Unit, n);
                if (grams is null)
                {
                    item.Status = NutrientItemStatus.NoConversion;
                }
                else if (grams == 0m && NegligibleUnits.Contains((ing.Unit ?? "").Trim()))
                {
                    item.Status = NutrientItemStatus.Negligible;
                    item.Grams = 0m;
                }
                else
                {
                    item.Status = IsApproxConversion(ing.Unit ?? "", n)
                        ? NutrientItemStatus.CountedApprox
                        : NutrientItemStatus.Counted;
                    item.Grams = grams;
                    var factor = grams.Value / 100m;
                    item.Calories = factor * (n.Energy_Kcal ?? 0m);
                    item.ProteinG = factor * (n.Protein_g ?? 0m);
                    item.FiberG = factor * (n.Fiber_TD_g ?? 0m);
                    item.IronMg = factor * (n.Iron_mg ?? 0m);
                    result.Calories += item.Calories;
                    result.ProteinG += item.ProteinG;
                    result.FiberG += item.FiberG;
                    result.IronMg += item.IronMg;
                }
            }
            result.Items.Add(item);
        }

        result.Calories = Math.Round(result.Calories, 1);
        result.ProteinG = Math.Round(result.ProteinG, 1);
        result.FiberG = Math.Round(result.FiberG, 1);
        result.IronMg = Math.Round(result.IronMg, 1);
        if (result.HasUncounted)
            result.UncountedNote = "Not counted: " + string.Join(", ", result.Uncounted.Select(u => u.Name));
        return result;
    }

    public async Task<NutrientComputation> ComputeForMealAsync(int mealId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var meal = await db.Meals.AsNoTracking()
            .Include(m => m.Ingredients).ThenInclude(mi => mi.Ingredient)!.ThenInclude(i => i!.Nutrition)
            .Include(m => m.Recipe)!.ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Ingredient)!.ThenInclude(i => i!.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == mealId);
        if (meal is null) return new NutrientComputation();

        // Same scaling convention as AnalyticsService: recipe-backed meals scale by servings ratio
        if (meal.Recipe != null && meal.Recipe.Ingredients.Any())
        {
            int recipeServings = meal.Recipe.Servings > 0 ? meal.Recipe.Servings : 1;
            int mealServings = meal.Servings > 0 ? meal.Servings : 1;
            decimal scale = (decimal)mealServings / recipeServings;
            return Compute(meal.Recipe.Ingredients
                .Where(ri => ri.Ingredient != null)
                .Select(ri => (ri.Ingredient!, ri.Quantity * scale)));
        }

        return Compute(meal.Ingredients
            .Where(mi => mi.Ingredient != null)
            .Select(mi => (mi.Ingredient!, mi.Quantity)));
    }

    public async Task<NutrientComputation> ComputeForRecipeAsync(int recipeId, decimal servingsEaten)
    {
        using var db = await _factory.CreateDbContextAsync();
        var recipe = await db.Recipes.AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)!.ThenInclude(i => i!.Nutrition)
            .FirstOrDefaultAsync(r => r.Id == recipeId);
        if (recipe is null) return new NutrientComputation();

        int recipeServings = recipe.Servings > 0 ? recipe.Servings : 1;
        decimal scale = servingsEaten / recipeServings;
        return Compute(recipe.Ingredients
            .Where(ri => ri.Ingredient != null)
            .Select(ri => (ri.Ingredient!, ri.Quantity * scale)));
    }

    public async Task<NutrientComputation> ComputeForIngredientAsync(int ingredientId, decimal quantity)
    {
        using var db = await _factory.CreateDbContextAsync();
        var ing = await db.Ingredients.AsNoTracking()
            .Include(i => i.Nutrition)
            .FirstOrDefaultAsync(i => i.Id == ingredientId);
        if (ing is null) return new NutrientComputation();
        return Compute(new[] { (ing, quantity) });
    }
}
