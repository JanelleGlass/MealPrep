namespace MealPrep.Data;

public class FoodLogEntry
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal? FiberG { get; set; }
    public decimal? IronMg { get; set; }
    public bool IsPlant { get; set; }
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public int? MealId { get; set; }
    public Meal? Meal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BodyMeasurement
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal WaistIn { get; set; }
    public decimal HeightIn { get; set; }
}

public class QuickAddItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FiberG { get; set; }
    public decimal IronMg { get; set; }
    public bool IsPlant { get; set; }
    public int SortOrder { get; set; }
}
