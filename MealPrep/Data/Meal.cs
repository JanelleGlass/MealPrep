namespace MealPrep.Data;

public class Meal
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public MealType MealType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 1;
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public List<MealIngredient> Ingredients { get; set; } = new();
}

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 1;
    public List<RecipeIngredient> Ingredients { get; set; } = new();
}

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? PricePerUnit { get; set; }
    public int? NutritionId { get; set; }
    public Nutrition? Nutrition { get; set; }
}

public class Nutrition
{
    public int Id { get; set; }
    public string NDB_No { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Macros (per 100g)
    public decimal? Water_g { get; set; }
    public decimal? Energy_Kcal { get; set; }
    public decimal? Protein_g { get; set; }
    public decimal? Lipid_Tot_g { get; set; }
    public decimal? Ash_g { get; set; }
    public decimal? Carbohydrt_g { get; set; }
    public decimal? Fiber_TD_g { get; set; }
    public decimal? Sugar_Tot_g { get; set; }

    // Minerals
    public decimal? Calcium_mg { get; set; }
    public decimal? Iron_mg { get; set; }
    public decimal? Magnesium_mg { get; set; }
    public decimal? Phosphorus_mg { get; set; }
    public decimal? Potassium_mg { get; set; }
    public decimal? Sodium_mg { get; set; }
    public decimal? Zinc_mg { get; set; }
    public decimal? Copper_mg { get; set; }
    public decimal? Manganese_mg { get; set; }
    public decimal? Selenium_ug { get; set; }

    // Vitamins
    public decimal? Vit_C_mg { get; set; }
    public decimal? Thiamin_mg { get; set; }
    public decimal? Riboflavin_mg { get; set; }
    public decimal? Niacin_mg { get; set; }
    public decimal? Panto_Acid_mg { get; set; }
    public decimal? Vit_B6_mg { get; set; }
    public decimal? Folate_Tot_ug { get; set; }
    public decimal? Folic_Acid_ug { get; set; }
    public decimal? Food_Folate_ug { get; set; }
    public decimal? Folate_DFE_ug { get; set; }
    public decimal? Choline_Tot_mg { get; set; }
    public decimal? Vit_B12_ug { get; set; }
    public decimal? Vit_A_IU { get; set; }
    public decimal? Vit_A_RAE { get; set; }
    public decimal? Retinol_ug { get; set; }
    public decimal? Vit_E_mg { get; set; }
    public decimal? Vit_D_ug { get; set; }
    public decimal? Vit_D_IU { get; set; }
    public decimal? Vit_K_ug { get; set; }

    // Carotenoids
    public decimal? Alpha_Carot_ug { get; set; }
    public decimal? Beta_Carot_ug { get; set; }
    public decimal? Beta_Crypt_ug { get; set; }
    public decimal? Lycopene_ug { get; set; }
    public decimal? Lut_Zea_ug { get; set; }

    // Fatty Acids
    public decimal? FA_Sat_g { get; set; }
    public decimal? FA_Mono_g { get; set; }
    public decimal? FA_Poly_g { get; set; }
    public decimal? Cholestrl_mg { get; set; }

    // Serving info
    public decimal? GmWt_1 { get; set; }
    public string? GmWt_Desc1 { get; set; }
    public decimal? GmWt_2 { get; set; }
    public string? GmWt_Desc2 { get; set; }
    public decimal? Refuse_Pct { get; set; }

    public bool IsUSDA { get; set; }
}

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
}

public class MealIngredient
{
    public int Id { get; set; }
    public int MealId { get; set; }
    public Meal? Meal { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
}

public class PantryItem
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
}

public class MealRangeGroup
{
    public int Id { get; set; }
    public Guid RangeGroupId { get; set; }
    public int MealId { get; set; }
    public Meal? Meal { get; set; }
}

public class RecipeBook
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<RecipeBookEntry> Entries { get; set; } = new();
}

public class RecipeBookEntry
{
    public int Id { get; set; }
    public int RecipeBookId { get; set; }
    public RecipeBook? RecipeBook { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
}

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StoreType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsDefault { get; set; }
}

public class StoreProduct
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public string ProductUrl { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? PackageSize { get; set; }
    public string? PackageUnit { get; set; }
    public decimal? PricePerUnit { get; set; }
    public DateTime LastUpdated { get; set; }
}

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner
}

public static class CookingUnits
{
    public static readonly string[] All =
    {
        // Volume
        "tsp",       // teaspoon
        "tbsp",      // tablespoon
        "fl oz",     // fluid ounce
        "cup",
        "pt",        // pint
        "qt",        // quart
        "gal",       // gallon
        "ml",        // milliliter
        "L",         // liter
        // Weight
        "oz",        // ounce
        "lb",        // pound
        "g",         // gram
        "kg",        // kilogram
        // Count / Other
        "pinch",
        "dash",
        "clove",
        "slice",
        "piece",
        "whole",
        "can",
        "bunch",
        "sprig",
        "head",
        "stalk",
        "to taste"
    };
}
