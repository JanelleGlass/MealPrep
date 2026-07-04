using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class IngredientTrend
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public int MealCount { get; set; }
    public decimal AvgPerMonth { get; set; }
    public decimal? EstMonthlyCost { get; set; }
}

public class RecipeTrend
{
    public int RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsed { get; set; }
    public decimal AvgPerMonth { get; set; }
}

public class MonthlyUsage
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class ShoppingPrediction
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal PredictedNeed { get; set; }
    public decimal PantryQuantity { get; set; }
    public decimal ToBuy { get; set; }
    public decimal? EstCost { get; set; }
    public decimal Confidence { get; set; }
}

public class AnalyticsService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public AnalyticsService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    private async Task<List<Meal>> GetMealsInRangeAsync(MealPrepDbContext db, DateTime start, DateTime end)
    {
        return await db.Meals
            .Include(m => m.Ingredients).ThenInclude(i => i.Ingredient)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Where(m => m.Date >= start && m.Date < end)
            .ToListAsync();
    }

    private static void TallyMeal(Meal meal, Dictionary<int, (string Name, string Unit, decimal Qty, int Count, decimal? PricePerUnit)> dict)
    {
        if (meal.Recipe != null && meal.Recipe.Ingredients.Any())
        {
            int recipeServings = meal.Recipe.Servings > 0 ? meal.Recipe.Servings : 1;
            int mealServings = meal.Servings > 0 ? meal.Servings : 1;
            decimal scale = (decimal)mealServings / recipeServings;

            foreach (var ri in meal.Recipe.Ingredients)
            {
                var ing = ri.Ingredient;
                if (ing == null) continue;
                decimal qty = Math.Round(ri.Quantity * scale, 2);
                if (dict.TryGetValue(ing.Id, out var existing))
                    dict[ing.Id] = (existing.Name, existing.Unit, existing.Qty + qty, existing.Count + 1, existing.PricePerUnit ?? ing.PricePerUnit);
                else
                    dict[ing.Id] = (ing.Name, ing.Unit, qty, 1, ing.PricePerUnit);
            }
        }
        else
        {
            foreach (var mi in meal.Ingredients)
            {
                var ing = mi.Ingredient;
                if (ing == null) continue;
                if (dict.TryGetValue(ing.Id, out var existing))
                    dict[ing.Id] = (existing.Name, existing.Unit, existing.Qty + mi.Quantity, existing.Count + 1, existing.PricePerUnit ?? ing.PricePerUnit);
                else
                    dict[ing.Id] = (ing.Name, ing.Unit, mi.Quantity, 1, ing.PricePerUnit);
            }
        }
    }

    public async Task<List<IngredientTrend>> GetIngredientTrendsAsync(int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var meals = await GetMealsInRangeAsync(db, start, end);

        var dict = new Dictionary<int, (string Name, string Unit, decimal Qty, int Count, decimal? PricePerUnit)>();
        foreach (var meal in meals)
            TallyMeal(meal, dict);

        decimal months = Math.Max(monthsBack, 1);
        return dict.Select(kv => new IngredientTrend
        {
            IngredientId = kv.Key,
            Name = kv.Value.Name,
            Unit = kv.Value.Unit,
            TotalQuantity = Math.Round(kv.Value.Qty, 2),
            MealCount = kv.Value.Count,
            AvgPerMonth = Math.Round(kv.Value.Qty / months, 2),
            EstMonthlyCost = kv.Value.PricePerUnit.HasValue
                ? Math.Round(kv.Value.Qty / months * kv.Value.PricePerUnit.Value, 2)
                : null
        }).OrderByDescending(t => t.MealCount).ToList();
    }

    public async Task<List<RecipeTrend>> GetRecipeTrendsAsync(int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);

        var meals = await db.Meals
            .Include(m => m.Recipe)
            .Where(m => m.Date >= start && m.Date < end && m.RecipeId != null)
            .ToListAsync();

        decimal months = Math.Max(monthsBack, 1);
        return meals.GroupBy(m => m.RecipeId!.Value)
            .Select(g => new RecipeTrend
            {
                RecipeId = g.Key,
                Name = g.First().Recipe?.Name ?? "Unknown",
                UsageCount = g.Count(),
                LastUsed = g.Max(m => m.Date),
                AvgPerMonth = Math.Round(g.Count() / months, 2)
            })
            .OrderByDescending(r => r.UsageCount)
            .ToList();
    }

    public async Task<List<MonthlyUsage>> GetIngredientMonthlyHistoryAsync(int ingredientId, int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var meals = await GetMealsInRangeAsync(db, start, end);

        var monthlyData = new Dictionary<(int Year, int Month), decimal>();

        // Initialize all months to 0
        for (int i = 0; i <= monthsBack; i++)
        {
            var d = start.AddMonths(i);
            monthlyData[(d.Year, d.Month)] = 0;
        }

        foreach (var meal in meals)
        {
            var key = (meal.Date.Year, meal.Date.Month);
            if (!monthlyData.ContainsKey(key)) continue;

            if (meal.Recipe != null && meal.Recipe.Ingredients.Any())
            {
                int recipeServings = meal.Recipe.Servings > 0 ? meal.Recipe.Servings : 1;
                int mealServings = meal.Servings > 0 ? meal.Servings : 1;
                decimal scale = (decimal)mealServings / recipeServings;

                foreach (var ri in meal.Recipe.Ingredients.Where(ri => ri.IngredientId == ingredientId))
                    monthlyData[key] += Math.Round(ri.Quantity * scale, 2);
            }
            else
            {
                foreach (var mi in meal.Ingredients.Where(mi => mi.IngredientId == ingredientId))
                    monthlyData[key] += mi.Quantity;
            }
        }

        return monthlyData
            .OrderBy(kv => kv.Key.Year).ThenBy(kv => kv.Key.Month)
            .Select(kv => new MonthlyUsage
            {
                Year = kv.Key.Year,
                Month = kv.Key.Month,
                Label = new DateTime(kv.Key.Year, kv.Key.Month, 1).ToString("MMM yyyy"),
                Quantity = Math.Round(kv.Value, 2)
            }).ToList();
    }

    public async Task<List<ShoppingPrediction>> GetShoppingPredictionsAsync(int monthsForward = 1, int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var meals = await GetMealsInRangeAsync(db, start, end);

        // Build per-month tallies per ingredient
        var monthlyTallies = new Dictionary<int, Dictionary<int, decimal>>(); // ingredientId -> (monthIndex -> qty)
        var ingredientInfo = new Dictionary<int, (string Name, string Unit, decimal? PricePerUnit)>();

        foreach (var meal in meals)
        {
            int monthIdx = (meal.Date.Year - start.Year) * 12 + (meal.Date.Month - start.Month);
            var items = new List<(int Id, string Name, string Unit, decimal Qty, decimal? Price)>();

            if (meal.Recipe != null && meal.Recipe.Ingredients.Any())
            {
                int recipeServings = meal.Recipe.Servings > 0 ? meal.Recipe.Servings : 1;
                int mealServings = meal.Servings > 0 ? meal.Servings : 1;
                decimal scale = (decimal)mealServings / recipeServings;
                foreach (var ri in meal.Recipe.Ingredients)
                {
                    if (ri.Ingredient == null) continue;
                    items.Add((ri.Ingredient.Id, ri.Ingredient.Name, ri.Ingredient.Unit,
                        Math.Round(ri.Quantity * scale, 2), ri.Ingredient.PricePerUnit));
                }
            }
            else
            {
                foreach (var mi in meal.Ingredients)
                {
                    if (mi.Ingredient == null) continue;
                    items.Add((mi.Ingredient.Id, mi.Ingredient.Name, mi.Ingredient.Unit,
                        mi.Quantity, mi.Ingredient.PricePerUnit));
                }
            }

            foreach (var (id, name, unit, qty, price) in items)
            {
                if (!monthlyTallies.ContainsKey(id))
                    monthlyTallies[id] = new Dictionary<int, decimal>();
                if (!monthlyTallies[id].ContainsKey(monthIdx))
                    monthlyTallies[id][monthIdx] = 0;
                monthlyTallies[id][monthIdx] += qty;
                ingredientInfo[id] = (name, unit, price);
            }
        }

        // Weighted moving average prediction
        decimal[] weights = { 1m, 1.5m, 2m, 2.5m, 3m, 3.5m };
        int totalMonths = monthsBack + 1; // include current month

        // Get pantry stock
        var pantryItems = await db.PantryItems
            .Include(p => p.Ingredient)
            .ToListAsync();
        var pantryDict = pantryItems.ToDictionary(p => p.IngredientId, p => p.Quantity);

        var predictions = new List<ShoppingPrediction>();

        foreach (var (ingredientId, monthData) in monthlyTallies)
        {
            // Build monthly values array (oldest to newest)
            var values = new decimal[totalMonths];
            for (int i = 0; i < totalMonths; i++)
            {
                monthData.TryGetValue(i, out values[i]);
            }

            // Weighted average using recent months
            int numWeights = Math.Min(weights.Length, totalMonths);
            decimal weightedSum = 0;
            decimal weightTotal = 0;
            for (int i = 0; i < numWeights; i++)
            {
                int idx = totalMonths - numWeights + i;
                weightedSum += values[idx] * weights[weights.Length - numWeights + i];
                weightTotal += weights[weights.Length - numWeights + i];
            }
            decimal predicted = weightTotal > 0 ? Math.Round(weightedSum / weightTotal * monthsForward, 2) : 0;

            // Confidence: 1 - stddev/mean, clamped
            decimal mean = values.Average();
            decimal confidence = 0.5m;
            if (mean > 0)
            {
                double variance = values.Select(v => Math.Pow((double)(v - mean), 2)).Average();
                decimal stddev = (decimal)Math.Sqrt(variance);
                confidence = Math.Clamp(1m - stddev / mean, 0.1m, 1.0m);
            }

            var info = ingredientInfo[ingredientId];
            decimal pantryQty = pantryDict.GetValueOrDefault(ingredientId, 0);
            decimal toBuy = Math.Max(predicted - pantryQty, 0);

            if (predicted <= 0) continue;

            predictions.Add(new ShoppingPrediction
            {
                IngredientId = ingredientId,
                Name = info.Name,
                Unit = info.Unit,
                PredictedNeed = predicted,
                PantryQuantity = pantryQty,
                ToBuy = Math.Round(toBuy, 2),
                EstCost = info.PricePerUnit.HasValue ? Math.Round(toBuy * info.PricePerUnit.Value, 2) : null,
                Confidence = Math.Round(confidence, 2)
            });
        }

        return predictions.OrderByDescending(p => p.ToBuy).ToList();
    }

    public async Task<Dictionary<string, int>> GetMealTypeDistributionAsync(int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);

        var meals = await db.Meals
            .Where(m => m.Date >= start && m.Date < end)
            .ToListAsync();

        return meals.GroupBy(m => m.MealType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<(List<string> Labels, List<(string Name, List<decimal> Values)> Series)> GetTopIngredientsMonthlyAsync(int topN = 5, int monthsBack = 6)
    {
        using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var meals = await GetMealsInRangeAsync(db, start, end);

        // Tally totals to find top N
        var totalDict = new Dictionary<int, (string Name, string Unit, decimal Qty, int Count, decimal? PricePerUnit)>();
        foreach (var meal in meals)
            TallyMeal(meal, totalDict);

        var topIds = totalDict.OrderByDescending(kv => kv.Value.Count)
            .Take(topN).Select(kv => kv.Key).ToHashSet();

        // Build monthly labels
        var labels = new List<string>();
        int totalMonths = monthsBack + 1;
        for (int i = 0; i < totalMonths; i++)
        {
            var d = start.AddMonths(i);
            labels.Add(d.ToString("MMM yyyy"));
        }

        // Monthly breakdown per top ingredient
        var monthlyByIngredient = new Dictionary<int, decimal[]>();
        foreach (var id in topIds)
            monthlyByIngredient[id] = new decimal[totalMonths];

        foreach (var meal in meals)
        {
            int monthIdx = (meal.Date.Year - start.Year) * 12 + (meal.Date.Month - start.Month);
            if (monthIdx < 0 || monthIdx >= totalMonths) continue;

            var items = new List<(int Id, decimal Qty)>();
            if (meal.Recipe != null && meal.Recipe.Ingredients.Any())
            {
                int recipeServings = meal.Recipe.Servings > 0 ? meal.Recipe.Servings : 1;
                int mealServings = meal.Servings > 0 ? meal.Servings : 1;
                decimal scale = (decimal)mealServings / recipeServings;
                foreach (var ri in meal.Recipe.Ingredients)
                {
                    if (ri.Ingredient == null) continue;
                    items.Add((ri.Ingredient.Id, Math.Round(ri.Quantity * scale, 2)));
                }
            }
            else
            {
                foreach (var mi in meal.Ingredients)
                {
                    if (mi.Ingredient == null) continue;
                    items.Add((mi.Ingredient.Id, mi.Quantity));
                }
            }

            foreach (var (id, qty) in items)
            {
                if (monthlyByIngredient.ContainsKey(id))
                    monthlyByIngredient[id][monthIdx] += qty;
            }
        }

        var series = topIds.Select(id => (
            Name: totalDict[id].Name,
            Values: monthlyByIngredient[id].Select(v => Math.Round(v, 2)).ToList()
        )).ToList();

        return (labels, series);
    }
}
