using MealPrep.Data;
using MealPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPrep;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mealprep.db");
        builder.Services.AddDbContextFactory<MealPrepDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddScoped<MealService>();
        builder.Services.AddScoped<RecipeService>();
        builder.Services.AddScoped<PantryService>();
        builder.Services.AddScoped<IngredientService>();
        builder.Services.AddScoped<DataPortService>();
        builder.Services.AddScoped<RecipeBookService>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddScoped<GitBackupService>();
        builder.Services.AddScoped<UserPreferenceService>();
        builder.Services.AddScoped<StoreService>();
        builder.Services.AddScoped<PriceScraperService>();
        builder.Services.AddScoped<NutritionService>();
        builder.Services.AddScoped<AnalyticsService>();

        var app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MealPrepDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.EnsureCreated();

            // Manual migration: create MealRangeGroups table if it doesn't exist
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS MealRangeGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RangeGroupId TEXT NOT NULL,
                    MealId INTEGER NOT NULL,
                    FOREIGN KEY (MealId) REFERENCES Meals(Id) ON DELETE CASCADE
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_MealRangeGroups_RangeGroupId_MealId
                ON MealRangeGroups (RangeGroupId, MealId);
            ");

            // Manual migration: create RecipeBooks and RecipeBookEntries tables if they don't exist
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS RecipeBooks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS RecipeBookEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RecipeBookId INTEGER NOT NULL,
                    RecipeId INTEGER NOT NULL,
                    FOREIGN KEY (RecipeBookId) REFERENCES RecipeBooks(Id) ON DELETE CASCADE,
                    FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_RecipeBookEntries_BookId_RecipeId
                ON RecipeBookEntries (RecipeBookId, RecipeId);
            ");

            // Manual migration: add PricePerUnit column to Ingredients if it doesn't exist
            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Ingredients ADD COLUMN PricePerUnit TEXT;");
            }
            catch { /* Column already exists */ }

            // Manual migration: create UserPreferences table
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS UserPreferences (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Key TEXT NOT NULL,
                    Value TEXT
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_UserPreferences_Key
                ON UserPreferences (""Key"");
            ");

            // Seed default DarkMode preference
            db.Database.ExecuteSqlRaw(@"
                INSERT OR IGNORE INTO UserPreferences (Key, Value)
                VALUES ('DarkMode', 'false');
            ");

            // Manual migration: create Stores and StoreProducts tables
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Stores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    StoreType TEXT NOT NULL DEFAULT '',
                    Location TEXT,
                    IsDefault INTEGER NOT NULL DEFAULT 0
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS StoreProducts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StoreId INTEGER NOT NULL,
                    IngredientId INTEGER NOT NULL,
                    ProductUrl TEXT NOT NULL DEFAULT '',
                    ProductName TEXT NOT NULL DEFAULT '',
                    Price TEXT NOT NULL DEFAULT '0',
                    PackageSize TEXT,
                    PackageUnit TEXT,
                    PricePerUnit TEXT,
                    LastUpdated TEXT NOT NULL,
                    FOREIGN KEY (StoreId) REFERENCES Stores(Id) ON DELETE CASCADE,
                    FOREIGN KEY (IngredientId) REFERENCES Ingredients(Id) ON DELETE CASCADE
                );
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_StoreProducts_StoreId_IngredientId
                ON StoreProducts (StoreId, IngredientId);
            ");

            // Manual migration: create Nutritions table
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Nutritions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    NDB_No TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    Water_g TEXT, Energy_Kcal TEXT, Protein_g TEXT, Lipid_Tot_g TEXT,
                    Ash_g TEXT, Carbohydrt_g TEXT, Fiber_TD_g TEXT, Sugar_Tot_g TEXT,
                    Calcium_mg TEXT, Iron_mg TEXT, Magnesium_mg TEXT, Phosphorus_mg TEXT,
                    Potassium_mg TEXT, Sodium_mg TEXT, Zinc_mg TEXT, Copper_mg TEXT,
                    Manganese_mg TEXT, Selenium_ug TEXT,
                    Vit_C_mg TEXT, Thiamin_mg TEXT, Riboflavin_mg TEXT, Niacin_mg TEXT,
                    Panto_Acid_mg TEXT, Vit_B6_mg TEXT, Folate_Tot_ug TEXT, Folic_Acid_ug TEXT,
                    Food_Folate_ug TEXT, Folate_DFE_ug TEXT, Choline_Tot_mg TEXT, Vit_B12_ug TEXT,
                    Vit_A_IU TEXT, Vit_A_RAE TEXT, Retinol_ug TEXT, Vit_E_mg TEXT,
                    Vit_D_ug TEXT, Vit_D_IU TEXT, Vit_K_ug TEXT,
                    Alpha_Carot_ug TEXT, Beta_Carot_ug TEXT, Beta_Crypt_ug TEXT,
                    Lycopene_ug TEXT, Lut_Zea_ug TEXT,
                    FA_Sat_g TEXT, FA_Mono_g TEXT, FA_Poly_g TEXT, Cholestrl_mg TEXT,
                    GmWt_1 TEXT, GmWt_Desc1 TEXT, GmWt_2 TEXT, GmWt_Desc2 TEXT,
                    Refuse_Pct TEXT, IsUSDA INTEGER NOT NULL DEFAULT 0
                );
            ");

            // Manual migration: add NutritionId FK to Ingredients
            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Ingredients ADD COLUMN NutritionId INTEGER REFERENCES Nutritions(Id);");
            }
            catch { /* Column already exists */ }
        }

        return app;
    }
}
