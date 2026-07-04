using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class MealService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public MealService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Meal>> GetMealsForMonthAsync(int year, int month)
    {
        using var db = await _factory.CreateDbContextAsync();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        return await db.Meals
            .Include(m => m.Ingredients).ThenInclude(i => i.Ingredient)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Where(m => m.Date >= start && m.Date < end)
            .OrderBy(m => m.Date).ThenBy(m => m.MealType)
            .ToListAsync();
    }

    public async Task<List<Meal>> GetMealsForDateAsync(DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Meals
            .Include(m => m.Ingredients).ThenInclude(i => i.Ingredient)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Where(m => m.Date == date.Date)
            .OrderBy(m => m.MealType)
            .ToListAsync();
    }

    public async Task<Meal> CreateMealAsync(Meal meal, List<MealIngredient> ingredients, Guid? rangeGroupId = null)
    {
        using var db = await _factory.CreateDbContextAsync();
        meal.Date = meal.Date.Date;
        meal.Ingredients = ingredients;
        db.Meals.Add(meal);
        await db.SaveChangesAsync();

        if (rangeGroupId.HasValue)
        {
            db.MealRangeGroups.Add(new MealRangeGroup
            {
                RangeGroupId = rangeGroupId.Value,
                MealId = meal.Id
            });
            await db.SaveChangesAsync();
        }

        return meal;
    }

    public async Task UpdateMealAsync(Meal meal, List<MealIngredient> ingredients)
    {
        using var db = await _factory.CreateDbContextAsync();
        meal.Date = meal.Date.Date;

        var old = await db.MealIngredients.Where(i => i.MealId == meal.Id).ToListAsync();
        db.MealIngredients.RemoveRange(old);

        foreach (var ing in ingredients)
        {
            ing.Id = 0;
            ing.MealId = meal.Id;
            db.MealIngredients.Add(ing);
        }

        db.Meals.Update(meal);
        await db.SaveChangesAsync();
    }

    public async Task DeleteMealAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var meal = await db.Meals.Include(m => m.Ingredients).FirstOrDefaultAsync(m => m.Id == id);
        if (meal != null)
        {
            db.Meals.Remove(meal);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Guid?> GetRangeGroupIdAsync(int mealId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var group = await db.MealRangeGroups.FirstOrDefaultAsync(g => g.MealId == mealId);
        return group?.RangeGroupId;
    }

    public async Task<int> DeleteMealRangeAsync(Guid rangeGroupId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var groups = await db.MealRangeGroups
            .Where(g => g.RangeGroupId == rangeGroupId)
            .ToListAsync();

        var mealIds = groups.Select(g => g.MealId).ToList();
        var meals = await db.Meals
            .Include(m => m.Ingredients)
            .Where(m => mealIds.Contains(m.Id))
            .ToListAsync();

        db.MealRangeGroups.RemoveRange(groups);
        db.Meals.RemoveRange(meals);
        await db.SaveChangesAsync();
        return meals.Count;
    }

    public async Task UpdateMealRangeAsync(Meal template, List<MealIngredient> ingredients, Guid rangeGroupId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var mealIds = await db.MealRangeGroups
            .Where(g => g.RangeGroupId == rangeGroupId)
            .Select(g => g.MealId)
            .ToListAsync();

        var meals = await db.Meals
            .Include(m => m.Ingredients)
            .Where(m => mealIds.Contains(m.Id))
            .ToListAsync();

        foreach (var meal in meals)
        {
            meal.Title = template.Title;
            meal.Description = template.Description;
            meal.Servings = template.Servings;
            meal.MealType = template.MealType;
            meal.RecipeId = template.RecipeId;

            db.MealIngredients.RemoveRange(meal.Ingredients);
            foreach (var ing in ingredients)
            {
                db.MealIngredients.Add(new MealIngredient
                {
                    MealId = meal.Id,
                    IngredientId = ing.IngredientId,
                    Quantity = ing.Quantity
                });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> CopyMonthAsync(int sourceYear, int sourceMonth, int targetYear, int targetMonth)
    {
        using var db = await _factory.CreateDbContextAsync();
        var start = new DateTime(sourceYear, sourceMonth, 1);
        var end = start.AddMonths(1);
        var sourceMeals = await db.Meals
            .Include(m => m.Ingredients)
            .Where(m => m.Date >= start && m.Date < end)
            .ToListAsync();

        int targetDays = DateTime.DaysInMonth(targetYear, targetMonth);
        int copied = 0;

        foreach (var meal in sourceMeals)
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
                RecipeId = meal.RecipeId
            };
            newMeal.Ingredients = meal.Ingredients.Select(i => new MealIngredient
            {
                IngredientId = i.IngredientId,
                Quantity = i.Quantity
            }).ToList();

            db.Meals.Add(newMeal);
            copied++;
        }

        await db.SaveChangesAsync();
        return copied;
    }

    public async Task<List<IngredientTally>> GetMonthlyTallyAsync(int year, int month)
    {
        using var db = await _factory.CreateDbContextAsync();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        var meals = await db.Meals
            .Include(m => m.Ingredients).ThenInclude(i => i.Ingredient)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Where(m => m.Date >= start && m.Date < end)
            .ToListAsync();

        var tallyDict = new Dictionary<int, IngredientTally>();

        foreach (var meal in meals)
        {
            // Use recipe ingredients (scaled) if linked, otherwise meal's own ingredients
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
                    if (tallyDict.TryGetValue(ing.Id, out var existing))
                        existing.TotalQuantity += qty;
                    else
                        tallyDict[ing.Id] = new IngredientTally
                        {
                            IngredientId = ing.Id, Name = ing.Name, Unit = ing.Unit,
                            TotalQuantity = qty, PricePerUnit = ing.PricePerUnit
                        };
                }
            }
            else
            {
                foreach (var mi in meal.Ingredients)
                {
                    var ing = mi.Ingredient;
                    if (ing == null) continue;
                    if (tallyDict.TryGetValue(ing.Id, out var existing))
                        existing.TotalQuantity += mi.Quantity;
                    else
                        tallyDict[ing.Id] = new IngredientTally
                        {
                            IngredientId = ing.Id, Name = ing.Name, Unit = ing.Unit,
                            TotalQuantity = mi.Quantity, PricePerUnit = ing.PricePerUnit
                        };
                }
            }
        }

        return tallyDict.Values.OrderBy(t => t.Name).ToList();
    }
}

public class IngredientTally
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal? PricePerUnit { get; set; }
}
