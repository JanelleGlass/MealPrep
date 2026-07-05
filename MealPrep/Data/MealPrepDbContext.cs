using Microsoft.EntityFrameworkCore;

namespace MealPrep.Data;

public class MealPrepDbContext : DbContext
{
    public MealPrepDbContext(DbContextOptions<MealPrepDbContext> options) : base(options) { }

    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealIngredient> MealIngredients => Set<MealIngredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<MealRangeGroup> MealRangeGroups => Set<MealRangeGroup>();
    public DbSet<RecipeBook> RecipeBooks => Set<RecipeBook>();
    public DbSet<RecipeBookEntry> RecipeBookEntries => Set<RecipeBookEntry>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreProduct> StoreProducts => Set<StoreProduct>();
    public DbSet<Nutrition> Nutritions => Set<Nutrition>();
    public DbSet<FoodLogEntry> FoodLogEntries => Set<FoodLogEntry>();
    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();
    public DbSet<QuickAddItem> QuickAddItems => Set<QuickAddItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Meal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.MealType).IsRequired();
            entity.Property(e => e.Servings).HasDefaultValue(1);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Ingredients).WithOne(i => i.Meal).HasForeignKey(i => i.MealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Servings).HasDefaultValue(1);
            entity.HasMany(e => e.Ingredients).WithOne(i => i.Recipe).HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.PricePerUnit).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Nutrition).WithMany().HasForeignKey(e => e.NutritionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Nutrition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NDB_No).HasMaxLength(10);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.GmWt_Desc1).HasMaxLength(200);
            entity.Property(e => e.GmWt_Desc2).HasMaxLength(200);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MealIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PantryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MealRangeGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RangeGroupId, e.MealId }).IsUnique();
            entity.HasOne(e => e.Meal).WithMany().HasForeignKey(e => e.MealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeBook>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasMany(e => e.Entries).WithOne(e => e.RecipeBook).HasForeignKey(e => e.RecipeBookId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeBookEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RecipeBookId, e.RecipeId }).IsUnique();
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StoreType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(200);
        });

        modelBuilder.Entity<StoreProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoreId, e.IngredientId }).IsUnique();
            entity.HasOne(e => e.Store).WithMany().HasForeignKey(e => e.StoreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
            entity.Property(e => e.PackageSize).HasColumnType("decimal(10,4)");
            entity.Property(e => e.PricePerUnit).HasColumnType("decimal(10,4)");
        });

        modelBuilder.Entity<FoodLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Meal).WithMany().HasForeignKey(e => e.MealId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<BodyMeasurement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<QuickAddItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });
    }
}
