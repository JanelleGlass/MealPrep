using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class IngredientService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public IngredientService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Ingredient>> GetAllAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Ingredients.OrderBy(i => i.Name).ToListAsync();
    }

    public async Task<Ingredient> CreateAsync(Ingredient ingredient)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return ingredient;
    }

    public async Task UpdateAsync(Ingredient ingredient)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Ingredients.Update(ingredient);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var item = await db.Ingredients.FindAsync(id);
        if (item != null)
        {
            db.Ingredients.Remove(item);
            await db.SaveChangesAsync();
        }
    }
}
