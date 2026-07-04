using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class BodyMeasurementService
{
    public const decimal DefaultHeightIn = 71m;

    private readonly IDbContextFactory<MealPrepDbContext> _factory;
    private readonly UserPreferenceService _preferences;

    public BodyMeasurementService(IDbContextFactory<MealPrepDbContext> factory, UserPreferenceService preferences)
    {
        _factory = factory;
        _preferences = preferences;
    }

    public async Task<List<BodyMeasurement>> GetAllAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.BodyMeasurements
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .ToListAsync();
    }

    public async Task<BodyMeasurement?> GetLatestAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.BodyMeasurements
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<BodyMeasurement> AddAsync(BodyMeasurement measurement)
    {
        using var db = await _factory.CreateDbContextAsync();
        measurement.Date = measurement.Date.Date;
        db.BodyMeasurements.Add(measurement);
        await db.SaveChangesAsync();
        return measurement;
    }

    public async Task<decimal> GetFallbackHeightAsync()
    {
        var latest = await GetLatestAsync();
        if (latest != null && latest.HeightIn > 0) return latest.HeightIn;
        return await _preferences.GetDecimalAsync("LogHeightIn", DefaultHeightIn);
    }
}
