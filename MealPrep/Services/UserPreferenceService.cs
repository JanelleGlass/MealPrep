using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class UserPreferenceService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public UserPreferenceService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<string?> GetAsync(string key)
    {
        using var db = _factory.CreateDbContext();
        var pref = await db.UserPreferences.FirstOrDefaultAsync(p => p.Key == key);
        return pref?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        using var db = _factory.CreateDbContext();
        var pref = await db.UserPreferences.FirstOrDefaultAsync(p => p.Key == key);
        if (pref is null)
        {
            db.UserPreferences.Add(new UserPreference { Key = key, Value = value });
        }
        else
        {
            pref.Value = value;
        }
        await db.SaveChangesAsync();
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var value = await GetAsync(key);
        return value is not null ? value == "true" : defaultValue;
    }
}
