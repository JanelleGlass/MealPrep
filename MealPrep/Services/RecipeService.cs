using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class RecipeService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public RecipeService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Recipe>> GetAllRecipesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Recipe> CreateRecipeAsync(Recipe recipe)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    public async Task UpdateRecipeAsync(Recipe recipe, List<RecipeIngredient> ingredients)
    {
        using var db = await _factory.CreateDbContextAsync();

        var old = await db.RecipeIngredients.Where(i => i.RecipeId == recipe.Id).ToListAsync();
        db.RecipeIngredients.RemoveRange(old);

        foreach (var ing in ingredients)
        {
            ing.Id = 0;
            ing.RecipeId = recipe.Id;
            db.RecipeIngredients.Add(ing);
        }

        db.Recipes.Update(recipe);
        await db.SaveChangesAsync();
    }

    public async Task DeleteRecipeAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var recipe = await db.Recipes.Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.Id == id);
        if (recipe != null)
        {
            db.Recipes.Remove(recipe);
            await db.SaveChangesAsync();
        }
    }
}
