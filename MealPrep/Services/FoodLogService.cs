using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class FoodLogService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public FoodLogService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<FoodLogEntry>> GetEntriesForDateAsync(DateTime date)
    {
        using var db = await _factory.CreateDbContextAsync();
        var day = date.Date;
        return await db.FoodLogEntries
            .Where(e => e.Date == day)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<FoodLogEntry>> GetEntriesForDateRangeAsync(DateTime start, DateTime end)
    {
        using var db = await _factory.CreateDbContextAsync();
        var s = start.Date;
        var e2 = end.Date;
        return await db.FoodLogEntries
            .Where(e => e.Date >= s && e.Date <= e2)
            .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<FoodLogEntry> AddEntryAsync(FoodLogEntry entry)
    {
        using var db = await _factory.CreateDbContextAsync();
        entry.Date = entry.Date.Date;
        entry.CreatedAt = DateTime.Now;
        db.FoodLogEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task DeleteEntryAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var entry = await db.FoodLogEntries.FindAsync(id);
        if (entry != null)
        {
            db.FoodLogEntries.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> GetWeeklyPlantCountAsync(DateTime asOfDate)
    {
        using var db = await _factory.CreateDbContextAsync();
        var end = asOfDate.Date;
        var start = end.AddDays(-6);
        return await db.FoodLogEntries
            .CountAsync(e => e.IsPlant && e.Date >= start && e.Date <= end);
    }

    public async Task<List<QuickAddItem>> GetQuickAddItemsAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.QuickAddItems.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
    }

    public async Task<QuickAddItem> AddQuickAddItemAsync(QuickAddItem item)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.QuickAddItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateQuickAddItemAsync(QuickAddItem item)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.QuickAddItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteQuickAddItemAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var item = await db.QuickAddItems.FindAsync(id);
        if (item != null)
        {
            db.QuickAddItems.Remove(item);
            await db.SaveChangesAsync();
        }
    }
}
