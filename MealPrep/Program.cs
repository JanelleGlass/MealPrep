using System.Security.Claims;
using MealPrep.Components;
using MealPrep.Data;
using MealPrep.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dataDir = Environment.GetEnvironmentVariable("MEALPREP_DATA_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "mealprep.db");
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
builder.Services.AddScoped<FoodLogService>();
builder.Services.AddScoped<BodyMeasurementService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(90);
        options.SlidingExpiration = true;
        options.Cookie.Name = "mealprep_auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Single-user password login. Password comes from MEALPREP_PASSWORD (dev fallback: "mealprep").
var appPassword = Environment.GetEnvironmentVariable("MEALPREP_PASSWORD") ?? "mealprep";

app.MapPost("/auth/login", async (HttpContext http) =>
{
    var form = await http.Request.ReadFormAsync();
    var supplied = form["password"].ToString();
    var ok = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(supplied),
        System.Text.Encoding.UTF8.GetBytes(appPassword));
    if (!ok)
        return Results.Redirect("/login?error=1");

    var identity = new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, "owner") },
        CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
    return Results.Redirect("/");
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure database is created (same manual-migration pattern the MAUI app used)
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

    // Manual migration: create FoodLogEntries table (Daily Log feature)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS FoodLogEntries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Date TEXT NOT NULL,
            Name TEXT NOT NULL,
            Calories TEXT NOT NULL DEFAULT '0',
            ProteinG TEXT NOT NULL DEFAULT '0',
            FiberG TEXT,
            IronMg TEXT,
            IsPlant INTEGER NOT NULL DEFAULT 0,
            IngredientId INTEGER REFERENCES Ingredients(Id) ON DELETE SET NULL,
            CreatedAt TEXT NOT NULL
        );
    ");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_FoodLogEntries_Date
        ON FoodLogEntries (Date);
    ");

    // Manual migration: create BodyMeasurements table
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS BodyMeasurements (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Date TEXT NOT NULL,
            WaistIn TEXT NOT NULL DEFAULT '0',
            HeightIn TEXT NOT NULL DEFAULT '0'
        );
    ");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_BodyMeasurements_Date
        ON BodyMeasurements (Date);
    ");

    // Manual migration: create QuickAddItems table
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS QuickAddItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Calories TEXT NOT NULL DEFAULT '0',
            ProteinG TEXT NOT NULL DEFAULT '0',
            FiberG TEXT NOT NULL DEFAULT '0',
            IronMg TEXT NOT NULL DEFAULT '0',
            IsPlant INTEGER NOT NULL DEFAULT 0,
            SortOrder INTEGER NOT NULL DEFAULT 0
        );
    ");

    // Seed default quick-add items on first run
    db.Database.ExecuteSqlRaw(@"
        INSERT INTO QuickAddItems (Name, Calories, ProteinG, FiberG, IronMg, IsPlant, SortOrder)
        SELECT * FROM (
            SELECT 'Protein coffee (1 scoop + milk)' AS Name, '170' AS Calories, '24' AS ProteinG, '0' AS FiberG, '0.2' AS IronMg, 0 AS IsPlant, 0 AS SortOrder
            UNION ALL SELECT 'Edamame, 1 cup', '190', '17', '8', '3.5', 1, 1
            UNION ALL SELECT 'Soft boiled egg', '70', '6', '0', '0.6', 0, 2
            UNION ALL SELECT 'Greek yogurt, 1/2 cup', '100', '10', '0', '0.1', 0, 3
        )
        WHERE NOT EXISTS (SELECT 1 FROM QuickAddItems);
    ");
}

app.Run();
