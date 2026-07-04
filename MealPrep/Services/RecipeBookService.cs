using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class RecipeBookService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public RecipeBookService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<RecipeBook>> GetAllBooksAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.RecipeBooks
            .Include(b => b.Entries)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<RecipeBook?> GetBookWithRecipesAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.RecipeBooks
            .Include(b => b.Entries)
                .ThenInclude(e => e.Recipe)
                    .ThenInclude(r => r!.Ingredients)
                        .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<RecipeBook> CreateBookAsync(string name, string? description = null)
    {
        using var db = await _factory.CreateDbContextAsync();
        var book = new RecipeBook { Name = name, Description = description };
        db.RecipeBooks.Add(book);
        await db.SaveChangesAsync();
        return book;
    }

    public async Task UpdateBookAsync(RecipeBook book)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.RecipeBooks.Update(book);
        await db.SaveChangesAsync();
    }

    public async Task DeleteBookAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var book = await db.RecipeBooks.FindAsync(id);
        if (book != null)
        {
            db.RecipeBooks.Remove(book);
            await db.SaveChangesAsync();
        }
    }

    public async Task AddRecipeToBookAsync(int bookId, int recipeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var exists = await db.RecipeBookEntries
            .AnyAsync(e => e.RecipeBookId == bookId && e.RecipeId == recipeId);
        if (!exists)
        {
            db.RecipeBookEntries.Add(new RecipeBookEntry { RecipeBookId = bookId, RecipeId = recipeId });
            await db.SaveChangesAsync();
        }
    }

    public async Task RemoveRecipeFromBookAsync(int bookId, int recipeId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var entry = await db.RecipeBookEntries
            .FirstOrDefaultAsync(e => e.RecipeBookId == bookId && e.RecipeId == recipeId);
        if (entry != null)
        {
            db.RecipeBookEntries.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<Recipe>> GetUngroupedRecipesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var groupedRecipeIds = await db.RecipeBookEntries
            .Select(e => e.RecipeId)
            .Distinct()
            .ToListAsync();
        return await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Where(r => !groupedRecipeIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .ToListAsync();
    }
}
