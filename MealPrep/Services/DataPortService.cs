using System.Text.Json;
using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class DataPortService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public DataPortService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<string> ExportAsync()
    {
        using var db = await _factory.CreateDbContextAsync();

        var export = new DataExport
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            Ingredients = await db.Ingredients.AsNoTracking().ToListAsync(),
            Recipes = await db.Recipes.AsNoTracking().ToListAsync(),
            RecipeIngredients = await db.RecipeIngredients.AsNoTracking().ToListAsync(),
            Meals = await db.Meals.AsNoTracking().ToListAsync(),
            MealIngredients = await db.MealIngredients.AsNoTracking().ToListAsync(),
            PantryItems = await db.PantryItems.AsNoTracking().ToListAsync(),
            RecipeBooks = await db.RecipeBooks.AsNoTracking().ToListAsync(),
            RecipeBookEntries = await db.RecipeBookEntries.AsNoTracking().ToListAsync(),
            MealRangeGroups = await db.MealRangeGroups.AsNoTracking().ToListAsync(),
            Stores = await db.Stores.AsNoTracking().ToListAsync(),
            StoreProducts = await db.StoreProducts.AsNoTracking().ToListAsync(),
            FoodLogEntries = await db.FoodLogEntries.AsNoTracking().ToListAsync(),
            BodyMeasurements = await db.BodyMeasurements.AsNoTracking().ToListAsync(),
            QuickAddItems = await db.QuickAddItems.AsNoTracking().ToListAsync()
        };

        // Null out navigation properties to avoid circular references
        foreach (var r in export.Recipes) r.Ingredients = new();
        foreach (var ri in export.RecipeIngredients) { ri.Recipe = null; ri.Ingredient = null; }
        foreach (var m in export.Meals) { m.Recipe = null; m.Ingredients = new(); }
        foreach (var mi in export.MealIngredients) { mi.Meal = null; mi.Ingredient = null; }
        foreach (var pi in export.PantryItems) pi.Ingredient = null;
        foreach (var rb in export.RecipeBooks) rb.Entries = new();
        foreach (var rbe in export.RecipeBookEntries) { rbe.RecipeBook = null; rbe.Recipe = null; }
        foreach (var mrg in export.MealRangeGroups) mrg.Meal = null;
        foreach (var sp in export.StoreProducts) { sp.Store = null; sp.Ingredient = null; }
        foreach (var fle in export.FoodLogEntries) { fle.Ingredient = null; fle.Meal = null; }

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<Dictionary<string, string>> ExportSplitAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var now = DateTime.UtcNow;
        var files = new Dictionary<string, string>();

        // Ingredients
        var ingredients = await db.Ingredients.AsNoTracking().ToListAsync();
        files["ingredients.json"] = JsonSerializer.Serialize(new IngredientsExport
        {
            Version = 1, ExportedAt = now, Ingredients = ingredients
        }, opts);

        // Meals
        var meals = await db.Meals.AsNoTracking().ToListAsync();
        var mealIngredients = await db.MealIngredients.AsNoTracking().ToListAsync();
        var mealRangeGroups = await db.MealRangeGroups.AsNoTracking().ToListAsync();
        foreach (var m in meals) { m.Recipe = null; m.Ingredients = new(); }
        foreach (var mi in mealIngredients) { mi.Meal = null; mi.Ingredient = null; }
        foreach (var mrg in mealRangeGroups) mrg.Meal = null;
        files["meals.json"] = JsonSerializer.Serialize(new MealsExport
        {
            Version = 1, ExportedAt = now, Meals = meals, MealIngredients = mealIngredients, MealRangeGroups = mealRangeGroups
        }, opts);

        // Pantry
        var pantryItems = await db.PantryItems.AsNoTracking().ToListAsync();
        foreach (var pi in pantryItems) pi.Ingredient = null;
        files["pantry.json"] = JsonSerializer.Serialize(new PantryExport
        {
            Version = 1, ExportedAt = now, PantryItems = pantryItems
        }, opts);

        // Recipes & Books
        var recipes = await db.Recipes.AsNoTracking().ToListAsync();
        var recipeIngredients = await db.RecipeIngredients.AsNoTracking().ToListAsync();
        var recipeBooks = await db.RecipeBooks.AsNoTracking().ToListAsync();
        var recipeBookEntries = await db.RecipeBookEntries.AsNoTracking().ToListAsync();
        foreach (var r in recipes) r.Ingredients = new();
        foreach (var ri in recipeIngredients) { ri.Recipe = null; ri.Ingredient = null; }
        foreach (var rb in recipeBooks) rb.Entries = new();
        foreach (var rbe in recipeBookEntries) { rbe.RecipeBook = null; rbe.Recipe = null; }
        files["recipes-and-books.json"] = JsonSerializer.Serialize(new RecipesAndBooksExport
        {
            Version = 1, ExportedAt = now, Recipes = recipes, RecipeIngredients = recipeIngredients,
            RecipeBooks = recipeBooks, RecipeBookEntries = recipeBookEntries
        }, opts);

        // Stores
        var storesList = await db.Stores.AsNoTracking().ToListAsync();
        var storeProducts = await db.StoreProducts.AsNoTracking().ToListAsync();
        foreach (var sp in storeProducts) { sp.Store = null; sp.Ingredient = null; }
        files["stores.json"] = JsonSerializer.Serialize(new StoresExport
        {
            Version = 1, ExportedAt = now, Stores = storesList, StoreProducts = storeProducts
        }, opts);

        // Daily log
        var foodLogEntries = await db.FoodLogEntries.AsNoTracking().ToListAsync();
        var bodyMeasurements = await db.BodyMeasurements.AsNoTracking().ToListAsync();
        var quickAddItems = await db.QuickAddItems.AsNoTracking().ToListAsync();
        foreach (var fle in foodLogEntries) { fle.Ingredient = null; fle.Meal = null; }
        files["daily-log.json"] = JsonSerializer.Serialize(new DailyLogExport
        {
            Version = 1, ExportedAt = now, FoodLogEntries = foodLogEntries,
            BodyMeasurements = bodyMeasurements, QuickAddItems = quickAddItems
        }, opts);

        return files;
    }

    public async Task<string> ExportMonthAsync(int year, int month)
    {
        using var db = await _factory.CreateDbContextAsync();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        var meals = await db.Meals.AsNoTracking()
            .Include(m => m.Ingredients)
            .Where(m => m.Date >= start && m.Date < end)
            .ToListAsync();

        var mealIngredients = meals.SelectMany(m => m.Ingredients).ToList();

        // Collect referenced ingredient IDs from meals and meal ingredients
        var ingredientIds = mealIngredients.Select(mi => mi.IngredientId).ToHashSet();

        // Collect referenced recipe IDs
        var recipeIds = meals.Where(m => m.RecipeId.HasValue).Select(m => m.RecipeId!.Value).ToHashSet();

        var recipes = await db.Recipes.AsNoTracking().Where(r => recipeIds.Contains(r.Id)).ToListAsync();
        var recipeIngredients = await db.RecipeIngredients.AsNoTracking().Where(ri => recipeIds.Contains(ri.RecipeId)).ToListAsync();

        // Also include ingredient IDs from recipe ingredients
        foreach (var ri in recipeIngredients)
            ingredientIds.Add(ri.IngredientId);

        var ingredients = await db.Ingredients.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToListAsync();

        var export = new MonthExport
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            SourceYear = year,
            SourceMonth = month,
            Ingredients = ingredients,
            Recipes = recipes,
            RecipeIngredients = recipeIngredients,
            Meals = meals.ToList(),
            MealIngredients = mealIngredients
        };

        // Null out navigation properties to avoid circular references
        foreach (var r in export.Recipes) r.Ingredients = new();
        foreach (var ri in export.RecipeIngredients) { ri.Recipe = null; ri.Ingredient = null; }
        foreach (var m in export.Meals) { m.Recipe = null; m.Ingredients = new(); }
        foreach (var mi in export.MealIngredients) { mi.Meal = null; mi.Ingredient = null; }

        // Export MealRangeGroups for meals in this month
        var mealIds = meals.Select(m => m.Id).ToHashSet();
        var mealRangeGroups = await db.MealRangeGroups.AsNoTracking()
            .Where(mrg => mealIds.Contains(mrg.MealId))
            .ToListAsync();
        foreach (var mrg in mealRangeGroups) mrg.Meal = null;
        export.MealRangeGroups = mealRangeGroups;

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<int> ImportMonthAsync(string json, int targetYear, int targetMonth)
    {
        var data = JsonSerializer.Deserialize<MonthExport>(json)
            ?? throw new InvalidOperationException("Invalid month export file.");

        using var db = await _factory.CreateDbContextAsync();
        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            // Map old IDs to new IDs
            var ingredientMap = new Dictionary<int, int>();
            var recipeMap = new Dictionary<int, int>();
            var mealMap = new Dictionary<int, int>();

            // Import ingredients — match by name+unit, or create new
            foreach (var ing in data.Ingredients)
            {
                var oldId = ing.Id;
                var existing = await db.Ingredients.FirstOrDefaultAsync(i => i.Name == ing.Name && i.Unit == ing.Unit);
                if (existing != null)
                {
                    ingredientMap[oldId] = existing.Id;
                }
                else
                {
                    ing.Id = 0;
                    db.Ingredients.Add(ing);
                    await db.SaveChangesAsync();
                    ingredientMap[oldId] = ing.Id;
                }
            }

            // Import recipes — match by name, or create new
            foreach (var recipe in data.Recipes)
            {
                var oldId = recipe.Id;
                var existing = await db.Recipes.FirstOrDefaultAsync(r => r.Name == recipe.Name);
                if (existing != null)
                {
                    recipeMap[oldId] = existing.Id;
                }
                else
                {
                    recipe.Id = 0;
                    recipe.Ingredients = new();
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync();
                    recipeMap[oldId] = recipe.Id;

                    // Add recipe ingredients for this new recipe
                    var riForRecipe = data.RecipeIngredients.Where(ri => ri.RecipeId == oldId);
                    foreach (var ri in riForRecipe)
                    {
                        ri.Id = 0;
                        ri.RecipeId = recipeMap[oldId];
                        ri.IngredientId = ingredientMap[ri.IngredientId];
                        ri.Recipe = null;
                        ri.Ingredient = null;
                        db.RecipeIngredients.Add(ri);
                    }
                    await db.SaveChangesAsync();
                }
            }

            // Calculate date shift from source month to target month
            int targetDays = DateTime.DaysInMonth(targetYear, targetMonth);
            int copied = 0;

            foreach (var meal in data.Meals)
            {
                int day = meal.Date.Day;
                if (day > targetDays) continue; // skip days that don't exist in target month

                var newMeal = new Meal
                {
                    Date = new DateTime(targetYear, targetMonth, day),
                    MealType = meal.MealType,
                    Title = meal.Title,
                    Description = meal.Description,
                    Servings = meal.Servings,
                    RecipeId = meal.RecipeId.HasValue && recipeMap.ContainsKey(meal.RecipeId.Value)
                        ? recipeMap[meal.RecipeId.Value]
                        : null
                };

                // Map meal ingredients
                var mealIngs = data.MealIngredients.Where(mi => mi.MealId == meal.Id).ToList();
                newMeal.Ingredients = mealIngs.Select(mi => new MealIngredient
                {
                    IngredientId = ingredientMap[mi.IngredientId],
                    Quantity = mi.Quantity
                }).ToList();

                db.Meals.Add(newMeal);
                await db.SaveChangesAsync();
                mealMap[meal.Id] = newMeal.Id;
                copied++;
            }

            // Import MealRangeGroups with remapped MealId
            if (data.MealRangeGroups != null)
            {
                foreach (var mrg in data.MealRangeGroups)
                {
                    if (!mealMap.ContainsKey(mrg.MealId)) continue;
                    mrg.Id = 0;
                    mrg.MealId = mealMap[mrg.MealId];
                    mrg.Meal = null;
                    db.MealRangeGroups.Add(mrg);
                }
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return copied;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<string> ExportBookAsync(int bookId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var book = await db.RecipeBooks
            .Include(b => b.Entries)
            .FirstOrDefaultAsync(b => b.Id == bookId)
            ?? throw new InvalidOperationException("Recipe book not found.");

        var recipeIds = book.Entries.Select(e => e.RecipeId).ToHashSet();
        var recipes = await db.Recipes.AsNoTracking().Where(r => recipeIds.Contains(r.Id)).ToListAsync();
        var recipeIngredients = await db.RecipeIngredients.AsNoTracking().Where(ri => recipeIds.Contains(ri.RecipeId)).ToListAsync();
        var ingredientIds = recipeIngredients.Select(ri => ri.IngredientId).ToHashSet();
        var ingredients = await db.Ingredients.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToListAsync();

        var export = new RecipeBookExport
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            BookName = book.Name,
            BookDescription = book.Description,
            Ingredients = ingredients,
            Recipes = recipes,
            RecipeIngredients = recipeIngredients
        };

        foreach (var r in export.Recipes) r.Ingredients = new();
        foreach (var ri in export.RecipeIngredients) { ri.Recipe = null; ri.Ingredient = null; }

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ImportBookAsync(string json)
    {
        var data = JsonSerializer.Deserialize<RecipeBookExport>(json)
            ?? throw new InvalidOperationException("Invalid recipe book export file.");

        using var db = await _factory.CreateDbContextAsync();
        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var ingredientMap = new Dictionary<int, int>();
            var recipeMap = new Dictionary<int, int>();

            // Import ingredients — match by name+unit, or create new
            foreach (var ing in data.Ingredients)
            {
                var oldId = ing.Id;
                var existing = await db.Ingredients.FirstOrDefaultAsync(i => i.Name == ing.Name && i.Unit == ing.Unit);
                if (existing != null)
                {
                    ingredientMap[oldId] = existing.Id;
                }
                else
                {
                    ing.Id = 0;
                    db.Ingredients.Add(ing);
                    await db.SaveChangesAsync();
                    ingredientMap[oldId] = ing.Id;
                }
            }

            // Import recipes — match by name, or create new
            foreach (var recipe in data.Recipes)
            {
                var oldId = recipe.Id;
                var existing = await db.Recipes.FirstOrDefaultAsync(r => r.Name == recipe.Name);
                if (existing != null)
                {
                    recipeMap[oldId] = existing.Id;
                }
                else
                {
                    recipe.Id = 0;
                    recipe.Ingredients = new();
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync();
                    recipeMap[oldId] = recipe.Id;

                    var riForRecipe = data.RecipeIngredients.Where(ri => ri.RecipeId == oldId);
                    foreach (var ri in riForRecipe)
                    {
                        ri.Id = 0;
                        ri.RecipeId = recipeMap[oldId];
                        ri.IngredientId = ingredientMap[ri.IngredientId];
                        ri.Recipe = null;
                        ri.Ingredient = null;
                        db.RecipeIngredients.Add(ri);
                    }
                    await db.SaveChangesAsync();
                }
            }

            // Find or create the book by name
            var book = await db.RecipeBooks.FirstOrDefaultAsync(b => b.Name == data.BookName);
            if (book == null)
            {
                book = new RecipeBook { Name = data.BookName, Description = data.BookDescription };
                db.RecipeBooks.Add(book);
                await db.SaveChangesAsync();
            }

            // Add recipes to the book (idempotent)
            foreach (var newRecipeId in recipeMap.Values)
            {
                var exists = await db.RecipeBookEntries
                    .AnyAsync(e => e.RecipeBookId == book.Id && e.RecipeId == newRecipeId);
                if (!exists)
                {
                    db.RecipeBookEntries.Add(new RecipeBookEntry { RecipeBookId = book.Id, RecipeId = newRecipeId });
                }
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return book.Name;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ImportSplitAsync(Dictionary<string, string> files)
    {
        using var db = await _factory.CreateDbContextAsync();
        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            // Clear all existing data in dependency order
            db.FoodLogEntries.RemoveRange(await db.FoodLogEntries.ToListAsync());
            db.BodyMeasurements.RemoveRange(await db.BodyMeasurements.ToListAsync());
            db.QuickAddItems.RemoveRange(await db.QuickAddItems.ToListAsync());
            db.StoreProducts.RemoveRange(await db.StoreProducts.ToListAsync());
            db.Stores.RemoveRange(await db.Stores.ToListAsync());
            db.MealRangeGroups.RemoveRange(await db.MealRangeGroups.ToListAsync());
            db.MealIngredients.RemoveRange(await db.MealIngredients.ToListAsync());
            db.RecipeBookEntries.RemoveRange(await db.RecipeBookEntries.ToListAsync());
            db.RecipeIngredients.RemoveRange(await db.RecipeIngredients.ToListAsync());
            db.PantryItems.RemoveRange(await db.PantryItems.ToListAsync());
            db.Meals.RemoveRange(await db.Meals.ToListAsync());
            db.RecipeBooks.RemoveRange(await db.RecipeBooks.ToListAsync());
            db.Recipes.RemoveRange(await db.Recipes.ToListAsync());
            db.Ingredients.RemoveRange(await db.Ingredients.ToListAsync());
            await db.SaveChangesAsync();

            var ingredientMap = new Dictionary<int, int>();
            var recipeMap = new Dictionary<int, int>();
            var mealMap = new Dictionary<int, int>();

            // 1. Import ingredients
            if (files.TryGetValue("ingredients.json", out var ingredientsJson))
            {
                var data = JsonSerializer.Deserialize<IngredientsExport>(ingredientsJson)!;
                foreach (var ing in data.Ingredients)
                {
                    var oldId = ing.Id;
                    ing.Id = 0;
                    db.Ingredients.Add(ing);
                    await db.SaveChangesAsync();
                    ingredientMap[oldId] = ing.Id;
                }
            }

            // 2. Import recipes & books
            if (files.TryGetValue("recipes-and-books.json", out var recipesJson))
            {
                var data = JsonSerializer.Deserialize<RecipesAndBooksExport>(recipesJson)!;

                foreach (var recipe in data.Recipes)
                {
                    var oldId = recipe.Id;
                    recipe.Id = 0;
                    recipe.Ingredients = new();
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync();
                    recipeMap[oldId] = recipe.Id;
                }

                foreach (var ri in data.RecipeIngredients)
                {
                    ri.Id = 0;
                    ri.RecipeId = recipeMap[ri.RecipeId];
                    ri.IngredientId = ingredientMap[ri.IngredientId];
                    ri.Recipe = null;
                    ri.Ingredient = null;
                    db.RecipeIngredients.Add(ri);
                }
                await db.SaveChangesAsync();

                var recipeBookMap = new Dictionary<int, int>();
                if (data.RecipeBooks != null)
                {
                    foreach (var rb in data.RecipeBooks)
                    {
                        var oldId = rb.Id;
                        rb.Id = 0;
                        rb.Entries = new();
                        db.RecipeBooks.Add(rb);
                        await db.SaveChangesAsync();
                        recipeBookMap[oldId] = rb.Id;
                    }
                }
                if (data.RecipeBookEntries != null)
                {
                    foreach (var rbe in data.RecipeBookEntries)
                    {
                        rbe.Id = 0;
                        rbe.RecipeBookId = recipeBookMap[rbe.RecipeBookId];
                        rbe.RecipeId = recipeMap[rbe.RecipeId];
                        rbe.RecipeBook = null;
                        rbe.Recipe = null;
                        db.RecipeBookEntries.Add(rbe);
                    }
                    await db.SaveChangesAsync();
                }
            }

            // 3. Import meals
            if (files.TryGetValue("meals.json", out var mealsJson))
            {
                var data = JsonSerializer.Deserialize<MealsExport>(mealsJson)!;

                foreach (var meal in data.Meals)
                {
                    var oldId = meal.Id;
                    meal.Id = 0;
                    meal.RecipeId = meal.RecipeId.HasValue && recipeMap.ContainsKey(meal.RecipeId.Value)
                        ? recipeMap[meal.RecipeId.Value]
                        : null;
                    meal.Recipe = null;
                    meal.Ingredients = new();
                    db.Meals.Add(meal);
                    await db.SaveChangesAsync();
                    mealMap[oldId] = meal.Id;
                }

                foreach (var mi in data.MealIngredients)
                {
                    mi.Id = 0;
                    mi.MealId = mealMap[mi.MealId];
                    mi.IngredientId = ingredientMap[mi.IngredientId];
                    mi.Meal = null;
                    mi.Ingredient = null;
                    db.MealIngredients.Add(mi);
                }
                await db.SaveChangesAsync();

                if (data.MealRangeGroups != null)
                {
                    foreach (var mrg in data.MealRangeGroups)
                    {
                        mrg.Id = 0;
                        mrg.MealId = mealMap[mrg.MealId];
                        mrg.Meal = null;
                        db.MealRangeGroups.Add(mrg);
                    }
                    await db.SaveChangesAsync();
                }
            }

            // 4. Import pantry
            if (files.TryGetValue("pantry.json", out var pantryJson))
            {
                var data = JsonSerializer.Deserialize<PantryExport>(pantryJson)!;
                foreach (var pi in data.PantryItems)
                {
                    pi.Id = 0;
                    pi.IngredientId = ingredientMap[pi.IngredientId];
                    pi.Ingredient = null;
                    db.PantryItems.Add(pi);
                }
                await db.SaveChangesAsync();
            }

            // 5. Import stores
            if (files.TryGetValue("stores.json", out var storesJson))
            {
                var data = JsonSerializer.Deserialize<StoresExport>(storesJson)!;
                var storeMap = new Dictionary<int, int>();

                foreach (var store in data.Stores)
                {
                    var oldId = store.Id;
                    store.Id = 0;
                    db.Stores.Add(store);
                    await db.SaveChangesAsync();
                    storeMap[oldId] = store.Id;
                }

                foreach (var sp in data.StoreProducts)
                {
                    sp.Id = 0;
                    sp.StoreId = storeMap.ContainsKey(sp.StoreId) ? storeMap[sp.StoreId] : 0;
                    sp.IngredientId = ingredientMap.ContainsKey(sp.IngredientId) ? ingredientMap[sp.IngredientId] : 0;
                    sp.Store = null;
                    sp.Ingredient = null;
                    if (sp.StoreId > 0 && sp.IngredientId > 0)
                        db.StoreProducts.Add(sp);
                }
                await db.SaveChangesAsync();
            }

            // 6. Import daily log
            if (files.TryGetValue("daily-log.json", out var dailyLogJson))
            {
                var data = JsonSerializer.Deserialize<DailyLogExport>(dailyLogJson)!;
                foreach (var fle in data.FoodLogEntries)
                {
                    fle.Id = 0;
                    fle.IngredientId = fle.IngredientId.HasValue && ingredientMap.ContainsKey(fle.IngredientId.Value)
                        ? ingredientMap[fle.IngredientId.Value]
                        : null;
                    fle.Ingredient = null;
                    fle.MealId = fle.MealId.HasValue && mealMap.ContainsKey(fle.MealId.Value)
                        ? mealMap[fle.MealId.Value]
                        : null;
                    fle.Meal = null;
                    db.FoodLogEntries.Add(fle);
                }
                foreach (var bm in data.BodyMeasurements)
                {
                    bm.Id = 0;
                    db.BodyMeasurements.Add(bm);
                }
                foreach (var qa in data.QuickAddItems)
                {
                    qa.Id = 0;
                    db.QuickAddItems.Add(qa);
                }
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ImportAsync(string json)
    {
        var data = JsonSerializer.Deserialize<DataExport>(json)
            ?? throw new InvalidOperationException("Invalid export file.");

        using var db = await _factory.CreateDbContextAsync();
        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            // Clear all existing data in dependency order
            db.FoodLogEntries.RemoveRange(await db.FoodLogEntries.ToListAsync());
            db.BodyMeasurements.RemoveRange(await db.BodyMeasurements.ToListAsync());
            db.QuickAddItems.RemoveRange(await db.QuickAddItems.ToListAsync());
            db.StoreProducts.RemoveRange(await db.StoreProducts.ToListAsync());
            db.Stores.RemoveRange(await db.Stores.ToListAsync());
            db.MealRangeGroups.RemoveRange(await db.MealRangeGroups.ToListAsync());
            db.MealIngredients.RemoveRange(await db.MealIngredients.ToListAsync());
            db.RecipeBookEntries.RemoveRange(await db.RecipeBookEntries.ToListAsync());
            db.RecipeIngredients.RemoveRange(await db.RecipeIngredients.ToListAsync());
            db.PantryItems.RemoveRange(await db.PantryItems.ToListAsync());
            db.Meals.RemoveRange(await db.Meals.ToListAsync());
            db.RecipeBooks.RemoveRange(await db.RecipeBooks.ToListAsync());
            db.Recipes.RemoveRange(await db.Recipes.ToListAsync());
            db.Ingredients.RemoveRange(await db.Ingredients.ToListAsync());
            await db.SaveChangesAsync();

            // Maps: old ID -> new ID
            var ingredientMap = new Dictionary<int, int>();
            var recipeMap = new Dictionary<int, int>();
            var mealMap = new Dictionary<int, int>();

            // Insert ingredients
            foreach (var ing in data.Ingredients)
            {
                var oldId = ing.Id;
                ing.Id = 0;
                db.Ingredients.Add(ing);
                await db.SaveChangesAsync();
                ingredientMap[oldId] = ing.Id;
            }

            // Insert recipes
            foreach (var recipe in data.Recipes)
            {
                var oldId = recipe.Id;
                recipe.Id = 0;
                recipe.Ingredients = new();
                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();
                recipeMap[oldId] = recipe.Id;
            }

            // Insert recipe ingredients
            foreach (var ri in data.RecipeIngredients)
            {
                ri.Id = 0;
                ri.RecipeId = recipeMap[ri.RecipeId];
                ri.IngredientId = ingredientMap[ri.IngredientId];
                ri.Recipe = null;
                ri.Ingredient = null;
                db.RecipeIngredients.Add(ri);
            }
            await db.SaveChangesAsync();

            // Insert meals
            foreach (var meal in data.Meals)
            {
                var oldId = meal.Id;
                meal.Id = 0;
                meal.RecipeId = meal.RecipeId.HasValue && recipeMap.ContainsKey(meal.RecipeId.Value)
                    ? recipeMap[meal.RecipeId.Value]
                    : null;
                meal.Recipe = null;
                meal.Ingredients = new();
                db.Meals.Add(meal);
                await db.SaveChangesAsync();
                mealMap[oldId] = meal.Id;
            }

            // Insert meal ingredients
            foreach (var mi in data.MealIngredients)
            {
                mi.Id = 0;
                mi.MealId = mealMap[mi.MealId];
                mi.IngredientId = ingredientMap[mi.IngredientId];
                mi.Meal = null;
                mi.Ingredient = null;
                db.MealIngredients.Add(mi);
            }
            await db.SaveChangesAsync();

            // Insert pantry items
            foreach (var pi in data.PantryItems)
            {
                pi.Id = 0;
                pi.IngredientId = ingredientMap[pi.IngredientId];
                pi.Ingredient = null;
                db.PantryItems.Add(pi);
            }
            await db.SaveChangesAsync();

            // Insert recipe books
            var recipeBookMap = new Dictionary<int, int>();
            if (data.RecipeBooks != null)
            {
                foreach (var rb in data.RecipeBooks)
                {
                    var oldId = rb.Id;
                    rb.Id = 0;
                    rb.Entries = new();
                    db.RecipeBooks.Add(rb);
                    await db.SaveChangesAsync();
                    recipeBookMap[oldId] = rb.Id;
                }
            }

            // Insert recipe book entries
            if (data.RecipeBookEntries != null)
            {
                foreach (var rbe in data.RecipeBookEntries)
                {
                    rbe.Id = 0;
                    rbe.RecipeBookId = recipeBookMap[rbe.RecipeBookId];
                    rbe.RecipeId = recipeMap[rbe.RecipeId];
                    rbe.RecipeBook = null;
                    rbe.Recipe = null;
                    db.RecipeBookEntries.Add(rbe);
                }
                await db.SaveChangesAsync();
            }

            // Insert meal range groups
            if (data.MealRangeGroups != null)
            {
                foreach (var mrg in data.MealRangeGroups)
                {
                    mrg.Id = 0;
                    mrg.MealId = mealMap[mrg.MealId];
                    mrg.Meal = null;
                    db.MealRangeGroups.Add(mrg);
                }
                await db.SaveChangesAsync();
            }

            // Insert stores
            var storeMap = new Dictionary<int, int>();
            if (data.Stores != null)
            {
                foreach (var store in data.Stores)
                {
                    var oldId = store.Id;
                    store.Id = 0;
                    db.Stores.Add(store);
                    await db.SaveChangesAsync();
                    storeMap[oldId] = store.Id;
                }
            }

            // Insert store products
            if (data.StoreProducts != null)
            {
                foreach (var sp in data.StoreProducts)
                {
                    sp.Id = 0;
                    sp.StoreId = storeMap.ContainsKey(sp.StoreId) ? storeMap[sp.StoreId] : 0;
                    sp.IngredientId = ingredientMap.ContainsKey(sp.IngredientId) ? ingredientMap[sp.IngredientId] : 0;
                    sp.Store = null;
                    sp.Ingredient = null;
                    if (sp.StoreId > 0 && sp.IngredientId > 0)
                        db.StoreProducts.Add(sp);
                }
                await db.SaveChangesAsync();
            }

            // Insert daily log data
            if (data.FoodLogEntries != null)
            {
                foreach (var fle in data.FoodLogEntries)
                {
                    fle.Id = 0;
                    fle.IngredientId = fle.IngredientId.HasValue && ingredientMap.ContainsKey(fle.IngredientId.Value)
                        ? ingredientMap[fle.IngredientId.Value]
                        : null;
                    fle.Ingredient = null;
                    fle.MealId = fle.MealId.HasValue && mealMap.ContainsKey(fle.MealId.Value)
                        ? mealMap[fle.MealId.Value]
                        : null;
                    fle.Meal = null;
                    db.FoodLogEntries.Add(fle);
                }
                await db.SaveChangesAsync();
            }
            if (data.BodyMeasurements != null)
            {
                foreach (var bm in data.BodyMeasurements)
                {
                    bm.Id = 0;
                    db.BodyMeasurements.Add(bm);
                }
                await db.SaveChangesAsync();
            }
            if (data.QuickAddItems != null)
            {
                foreach (var qa in data.QuickAddItems)
                {
                    qa.Id = 0;
                    db.QuickAddItems.Add(qa);
                }
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class IngredientsExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
}

public class MealsExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<Meal> Meals { get; set; } = new();
    public List<MealIngredient> MealIngredients { get; set; } = new();
    public List<MealRangeGroup>? MealRangeGroups { get; set; } = new();
}

public class PantryExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<PantryItem> PantryItems { get; set; } = new();
}

public class RecipesAndBooksExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<Recipe> Recipes { get; set; } = new();
    public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
    public List<RecipeBook>? RecipeBooks { get; set; } = new();
    public List<RecipeBookEntry>? RecipeBookEntries { get; set; } = new();
}

public class DataExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
    public List<Meal> Meals { get; set; } = new();
    public List<MealIngredient> MealIngredients { get; set; } = new();
    public List<PantryItem> PantryItems { get; set; } = new();
    public List<RecipeBook>? RecipeBooks { get; set; } = new();
    public List<RecipeBookEntry>? RecipeBookEntries { get; set; } = new();
    public List<MealRangeGroup>? MealRangeGroups { get; set; } = new();
    public List<Store>? Stores { get; set; } = new();
    public List<StoreProduct>? StoreProducts { get; set; } = new();
    public List<FoodLogEntry>? FoodLogEntries { get; set; } = new();
    public List<BodyMeasurement>? BodyMeasurements { get; set; } = new();
    public List<QuickAddItem>? QuickAddItems { get; set; } = new();
}

public class DailyLogExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<FoodLogEntry> FoodLogEntries { get; set; } = new();
    public List<BodyMeasurement> BodyMeasurements { get; set; } = new();
    public List<QuickAddItem> QuickAddItems { get; set; } = new();
}

public class MonthExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
    public List<Meal> Meals { get; set; } = new();
    public List<MealIngredient> MealIngredients { get; set; } = new();
    public List<MealRangeGroup>? MealRangeGroups { get; set; } = new();
}

public class RecipeBookExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public string BookName { get; set; } = string.Empty;
    public string? BookDescription { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
}

public class StoresExport
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<Store> Stores { get; set; } = new();
    public List<StoreProduct> StoreProducts { get; set; } = new();
}
