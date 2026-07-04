using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class StoreService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public StoreService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Store>> GetAllStoresAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Stores.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Store?> GetDefaultStoreAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Stores.FirstOrDefaultAsync(s => s.IsDefault)
            ?? await db.Stores.FirstOrDefaultAsync();
    }

    public async Task<Store> CreateStoreAsync(Store store)
    {
        using var db = await _factory.CreateDbContextAsync();
        var anyExist = await db.Stores.AnyAsync();
        if (!anyExist) store.IsDefault = true;
        db.Stores.Add(store);
        await db.SaveChangesAsync();
        return store;
    }

    public async Task UpdateStoreAsync(Store store)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Stores.Update(store);
        await db.SaveChangesAsync();
    }

    public async Task DeleteStoreAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var store = await db.Stores.FindAsync(id);
        if (store != null)
        {
            db.Stores.Remove(store);
            await db.SaveChangesAsync();

            if (store.IsDefault)
            {
                var next = await db.Stores.FirstOrDefaultAsync();
                if (next != null)
                {
                    next.IsDefault = true;
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    public async Task SetDefaultStoreAsync(int storeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var stores = await db.Stores.ToListAsync();
        foreach (var s in stores)
            s.IsDefault = s.Id == storeId;
        await db.SaveChangesAsync();
    }

    public async Task<List<StoreProduct>> GetProductsForStoreAsync(int storeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.StoreProducts
            .Include(sp => sp.Ingredient)
            .Include(sp => sp.Store)
            .Where(sp => sp.StoreId == storeId)
            .OrderBy(sp => sp.Ingredient!.Name)
            .ToListAsync();
    }

    public async Task<List<Ingredient>> GetUnmappedIngredientsAsync(int storeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var mappedIds = await db.StoreProducts
            .Where(sp => sp.StoreId == storeId)
            .Select(sp => sp.IngredientId)
            .ToListAsync();
        return await db.Ingredients
            .Where(i => !mappedIds.Contains(i.Id))
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<StoreProduct> CreateProductAsync(StoreProduct product)
    {
        using var db = await _factory.CreateDbContextAsync();
        product.LastUpdated = DateTime.UtcNow;
        if (product.PackageSize.HasValue && product.PackageSize > 0)
            product.PricePerUnit = product.Price / product.PackageSize;
        db.StoreProducts.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public async Task UpdateProductAsync(StoreProduct product)
    {
        using var db = await _factory.CreateDbContextAsync();
        product.LastUpdated = DateTime.UtcNow;
        if (product.PackageSize.HasValue && product.PackageSize > 0)
            product.PricePerUnit = product.Price / product.PackageSize;
        db.StoreProducts.Update(product);
        await db.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var product = await db.StoreProducts.FindAsync(id);
        if (product != null)
        {
            db.StoreProducts.Remove(product);
            await db.SaveChangesAsync();
        }
    }

    public async Task SyncPriceToIngredientAsync(int storeProductId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var product = await db.StoreProducts.FindAsync(storeProductId);
        if (product?.PricePerUnit == null) return;

        var ingredient = await db.Ingredients.FindAsync(product.IngredientId);
        if (ingredient != null)
        {
            ingredient.PricePerUnit = product.PricePerUnit;
            await db.SaveChangesAsync();
        }
    }

    public async Task SyncAllPricesToIngredientsAsync(int storeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var products = await db.StoreProducts
            .Where(sp => sp.StoreId == storeId && sp.PricePerUnit != null)
            .ToListAsync();

        var ingredientIds = products.Select(p => p.IngredientId).ToList();
        var ingredients = await db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToListAsync();

        foreach (var ing in ingredients)
        {
            var product = products.First(p => p.IngredientId == ing.Id);
            ing.PricePerUnit = product.PricePerUnit;
        }
        await db.SaveChangesAsync();
    }
}
