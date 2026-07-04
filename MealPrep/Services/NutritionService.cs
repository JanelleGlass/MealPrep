using MealPrep.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.Services;

public class NutritionService
{
    private readonly IDbContextFactory<MealPrepDbContext> _factory;

    public NutritionService(IDbContextFactory<MealPrepDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Nutrition>> GetAllAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Nutritions.OrderBy(n => n.Description).ToListAsync();
    }

    public async Task<List<Nutrition>> SearchAsync(string term)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Nutritions
            .Where(n => n.Description.Contains(term))
            .OrderBy(n => n.Description)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Nutrition?> GetByIdAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Nutritions.FindAsync(id);
    }

    public async Task<Nutrition> CreateAsync(Nutrition n)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Nutritions.Add(n);
        await db.SaveChangesAsync();
        return n;
    }

    public async Task UpdateAsync(Nutrition n)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Nutritions.Update(n);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var item = await db.Nutritions.FindAsync(id);
        if (item != null)
        {
            db.Nutritions.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> ImportUSDAFromCsvAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine == null) return 0;

        var headers = ParseCsvLine(headerLine);
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            colMap[headers[i].Trim()] = i;

        var records = new List<Nutrition>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);
            var n = new Nutrition { IsUSDA = true };

            n.NDB_No = GetCol(cols, colMap, "NDB_No");
            n.Description = GetCol(cols, colMap, "Shrt_Desc");
            if (string.IsNullOrEmpty(n.Description))
                n.Description = GetCol(cols, colMap, "Description");

            n.Water_g = GetDecCol(cols, colMap, "Water_(g)");
            n.Energy_Kcal = GetDecCol(cols, colMap, "Energ_Kcal");
            n.Protein_g = GetDecCol(cols, colMap, "Protein_(g)");
            n.Lipid_Tot_g = GetDecCol(cols, colMap, "Lipid_Tot_(g)");
            n.Ash_g = GetDecCol(cols, colMap, "Ash_(g)");
            n.Carbohydrt_g = GetDecCol(cols, colMap, "Carbohydrt_(g)");
            n.Fiber_TD_g = GetDecCol(cols, colMap, "Fiber_TD_(g)");
            n.Sugar_Tot_g = GetDecCol(cols, colMap, "Sugar_Tot_(g)");
            n.Calcium_mg = GetDecCol(cols, colMap, "Calcium_(mg)");
            n.Iron_mg = GetDecCol(cols, colMap, "Iron_(mg)");
            n.Magnesium_mg = GetDecCol(cols, colMap, "Magnesium_(mg)");
            n.Phosphorus_mg = GetDecCol(cols, colMap, "Phosphorus_(mg)");
            n.Potassium_mg = GetDecCol(cols, colMap, "Potassium_(mg)");
            n.Sodium_mg = GetDecCol(cols, colMap, "Sodium_(mg)");
            n.Zinc_mg = GetDecCol(cols, colMap, "Zinc_(mg)");
            n.Copper_mg = GetDecCol(cols, colMap, "Copper_(mg)");
            n.Manganese_mg = GetDecCol(cols, colMap, "Manganese_(mg)");
            n.Selenium_ug = GetDecCol(cols, colMap, "Selenium_(µg)");
            n.Vit_C_mg = GetDecCol(cols, colMap, "Vit_C_(mg)");
            n.Thiamin_mg = GetDecCol(cols, colMap, "Thiamin_(mg)");
            n.Riboflavin_mg = GetDecCol(cols, colMap, "Riboflavin_(mg)");
            n.Niacin_mg = GetDecCol(cols, colMap, "Niacin_(mg)");
            n.Panto_Acid_mg = GetDecCol(cols, colMap, "Panto_Acid_(mg)");
            n.Vit_B6_mg = GetDecCol(cols, colMap, "Vit_B6_(mg)");
            n.Folate_Tot_ug = GetDecCol(cols, colMap, "Folate_Tot_(µg)");
            n.Folic_Acid_ug = GetDecCol(cols, colMap, "Folic_Acid_(µg)");
            n.Food_Folate_ug = GetDecCol(cols, colMap, "Food_Folate_(µg)");
            n.Folate_DFE_ug = GetDecCol(cols, colMap, "Folate_DFE_(µg)");
            n.Choline_Tot_mg = GetDecCol(cols, colMap, "Choline_Tot_(mg)");
            n.Vit_B12_ug = GetDecCol(cols, colMap, "Vit_B12_(µg)");
            n.Vit_A_IU = GetDecCol(cols, colMap, "Vit_A_IU");
            n.Vit_A_RAE = GetDecCol(cols, colMap, "Vit_A_RAE");
            n.Retinol_ug = GetDecCol(cols, colMap, "Retinol_(µg)");
            n.Vit_E_mg = GetDecCol(cols, colMap, "Vit_E_(mg)");
            n.Vit_D_ug = GetDecCol(cols, colMap, "Vit_D_µg");
            n.Vit_D_IU = GetDecCol(cols, colMap, "Vit_D_IU");
            n.Vit_K_ug = GetDecCol(cols, colMap, "Vit_K_(µg)");
            n.Alpha_Carot_ug = GetDecCol(cols, colMap, "Alpha_Carot_(µg)");
            n.Beta_Carot_ug = GetDecCol(cols, colMap, "Beta_Carot_(µg)");
            n.Beta_Crypt_ug = GetDecCol(cols, colMap, "Beta_Crypt_(µg)");
            n.Lycopene_ug = GetDecCol(cols, colMap, "Lycopene_(µg)");
            n.Lut_Zea_ug = GetDecCol(cols, colMap, "Lut+Zea_(µg)");
            n.FA_Sat_g = GetDecCol(cols, colMap, "FA_Sat_(g)");
            n.FA_Mono_g = GetDecCol(cols, colMap, "FA_Mono_(g)");
            n.FA_Poly_g = GetDecCol(cols, colMap, "FA_Poly_(g)");
            n.Cholestrl_mg = GetDecCol(cols, colMap, "Cholestrl_(mg)");
            n.GmWt_1 = GetDecCol(cols, colMap, "GmWt_1");
            n.GmWt_Desc1 = GetCol(cols, colMap, "GmWt_Desc1");
            n.GmWt_2 = GetDecCol(cols, colMap, "GmWt_2");
            n.GmWt_Desc2 = GetCol(cols, colMap, "GmWt_Desc2");
            n.Refuse_Pct = GetDecCol(cols, colMap, "Refuse_Pct");

            records.Add(n);
        }

        if (records.Count == 0) return 0;

        using var db = await _factory.CreateDbContextAsync();
        db.Nutritions.AddRange(records);
        await db.SaveChangesAsync();
        return records.Count;
    }

    public async Task LinkToIngredientAsync(int ingredientId, int nutritionId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var ingredient = await db.Ingredients.FindAsync(ingredientId);
        if (ingredient != null)
        {
            ingredient.NutritionId = nutritionId;
            await db.SaveChangesAsync();
        }
    }

    public async Task UnlinkFromIngredientAsync(int ingredientId)
    {
        using var db = await _factory.CreateDbContextAsync();
        var ingredient = await db.Ingredients.FindAsync(ingredientId);
        if (ingredient != null)
        {
            ingredient.NutritionId = null;
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<Ingredient>> GetIngredientsWithNutritionAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Ingredients
            .Include(i => i.Nutrition)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    private static string GetCol(string[] cols, Dictionary<string, int> map, string key)
    {
        if (map.TryGetValue(key, out var idx) && idx < cols.Length)
            return cols[idx].Trim().Trim('"');
        return string.Empty;
    }

    private static decimal? GetDecCol(string[] cols, Dictionary<string, int> map, string key)
    {
        var val = GetCol(cols, map, key);
        if (string.IsNullOrWhiteSpace(val)) return null;
        return decimal.TryParse(val, out var d) ? d : null;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                    inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
