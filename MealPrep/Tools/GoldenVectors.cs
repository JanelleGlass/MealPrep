using System.Text.Json;
using MealPrep.Data;
using MealPrep.Services;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Tools;

// Console mode: emits reference outputs of NutritionCalcService against a real
// database, used as golden vectors by the JS port in mealprep-mobile.
// Usage: dotnet run -- golden-vectors <dbPath> <outDir>
public static class GoldenVectors
{
    private class Factory : IDbContextFactory<MealPrepDbContext>
    {
        private readonly DbContextOptions<MealPrepDbContext> _options;
        public Factory(string dbPath) =>
            _options = new DbContextOptionsBuilder<MealPrepDbContext>()
                .UseSqlite($"Data Source={dbPath};Mode=ReadOnly").Options;
        public MealPrepDbContext CreateDbContext() => new(_options);
    }

    public static async Task GenerateAsync(string dbPath, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var factory = new Factory(dbPath);
        var calc = new NutritionCalcService(factory);
        var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var db = factory.CreateDbContext();
        var ingredients = await db.Ingredients.AsNoTracking().Include(i => i.Nutrition).OrderBy(i => i.Id).ToListAsync();
        var recipes = await db.Recipes.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        var meals = await db.Meals.AsNoTracking().OrderBy(m => m.Id).ToListAsync();

        static object ItemVector(NutrientItemResult i) => new
        {
            ingredientId = i.IngredientId,
            status = i.Status.ToString(),
            grams = i.Grams is decimal g ? Math.Round(g, 4) : (decimal?)null,
            calories = Math.Round(i.Calories, 4),
            proteinG = Math.Round(i.ProteinG, 4),
            fiberG = Math.Round(i.FiberG, 4),
            ironMg = Math.Round(i.IronMg, 4),
        };

        static object CompVector(NutrientComputation c) => new
        {
            calories = c.Calories,
            proteinG = c.ProteinG,
            fiberG = c.FiberG,
            ironMg = c.IronMg,
            hasApprox = c.HasApprox,
            uncountedNote = c.UncountedNote,
            items = c.Items.Select(ItemVector).ToList(),
        };

        var ingredientVectors = ingredients.Select(ing =>
        {
            var comp = NutritionCalcService.Compute(new[] { (ing, 1m) });
            return new { ingredientId = ing.Id, name = ing.Name, unit = ing.Unit, quantity = 1m, computation = CompVector(comp) };
        }).ToList();

        var recipeVectors = new List<object>();
        foreach (var r in recipes)
        {
            foreach (var servings in new[] { 1m, (decimal)(r.Servings > 0 ? r.Servings : 1) }.Distinct())
            {
                var comp = await calc.ComputeForRecipeAsync(r.Id, servings);
                recipeVectors.Add(new { recipeId = r.Id, name = r.Name, servingsEaten = servings, computation = CompVector(comp) });
            }
        }

        var mealVectors = new List<object>();
        foreach (var m in meals)
        {
            var comp = await calc.ComputeForMealAsync(m.Id);
            mealVectors.Add(new { mealId = m.Id, title = m.Title, computation = CompVector(comp) });
        }

        var vectors = new
        {
            generatedAt = DateTime.UtcNow,
            source = dbPath,
            ingredients = ingredientVectors,
            recipes = recipeVectors,
            meals = mealVectors,
        };
        await File.WriteAllTextAsync(Path.Combine(outDir, "vectors.json"), JsonSerializer.Serialize(vectors, json));

        // Fixture: everything the JS tests need to recompute the vectors offline.
        var nutritionIds = ingredients.Where(i => i.NutritionId.HasValue).Select(i => i.NutritionId!.Value).Distinct().ToList();
        var nutritions = await db.Nutritions.AsNoTracking().Where(n => nutritionIds.Contains(n.Id)).ToListAsync();
        var recipeIngredients = await db.RecipeIngredients.AsNoTracking().ToListAsync();
        var mealIngredients = await db.MealIngredients.AsNoTracking().ToListAsync();
        foreach (var ri in recipeIngredients) { ri.Recipe = null; ri.Ingredient = null; }
        foreach (var mi in mealIngredients) { mi.Meal = null; mi.Ingredient = null; }
        var fixture = new
        {
            nutritions,
            ingredients = ingredients.Select(i => new { i.Id, i.Name, i.Unit, i.NutritionId }).ToList(),
            recipes = recipes.Select(r => new { r.Id, r.Name, r.Servings }).ToList(),
            recipeIngredients = recipeIngredients.Select(ri => new { ri.RecipeId, ri.IngredientId, ri.Quantity }).ToList(),
            meals = meals.Select(m => new { m.Id, m.Title, m.Servings, m.RecipeId }).ToList(),
            mealIngredients = mealIngredients.Select(mi => new { mi.MealId, mi.IngredientId, mi.Quantity }).ToList(),
        };
        await File.WriteAllTextAsync(Path.Combine(outDir, "fixture.json"), JsonSerializer.Serialize(fixture, json));

        Console.WriteLine($"vectors: {ingredientVectors.Count} ingredients, {recipeVectors.Count} recipe runs, {mealVectors.Count} meals");
        Console.WriteLine($"fixture: {nutritions.Count} nutrition rows");
    }
}
