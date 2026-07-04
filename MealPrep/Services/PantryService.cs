using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class PantryService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public PantryService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<PantryItem>> GetAllAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.PantryItems
            .Include(p => p.Ingredient)
            .OrderBy(p => p.Ingredient!.Name)
            .ToListAsync();
    }

    public async Task<PantryItem> CreateAsync(PantryItem item)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.PantryItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(PantryItem item)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.PantryItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var item = await db.PantryItems.FindAsync(id);
        if (item != null)
        {
            db.PantryItems.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task DeductAsync(Dictionary<int, decimal> ingredientAmounts)
    {
        using var db = await _factory.CreateDbContextAsync();
        var pantryItems = await db.PantryItems.ToListAsync();

        foreach (var pantryItem in pantryItems)
        {
            if (ingredientAmounts.TryGetValue(pantryItem.IngredientId, out var needed))
            {
                pantryItem.Quantity -= needed;
                if (pantryItem.Quantity <= 0)
                    db.PantryItems.Remove(pantryItem);
            }
        }

        await db.SaveChangesAsync();
    }
}
