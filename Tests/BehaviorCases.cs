// Fixtures replace only game services: names, chip data, warning sink and Unity rounding.
// All algorithms under test are compiled from current Source declarations by the runner.
namespace UnityEngine
{
    internal static class Mathf
    {
        public static int RoundToInt(float value)
        {
            return (int)System.Math.Round(value, System.MidpointRounding.ToEven);
        }
    }
}

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<string>> DatadisksByUnlockedItem =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static int assertions;
        private static int warnings;
        private static string LocalizeItem(string id) { string value; return Names.TryGetValue(id, out value) ? value : id; }
        private static void LogRuntimeBoundaryWarningOnce(string key, string text, Exception error) { warnings++; }
        private static void Check(bool ok, string message)
        {
            assertions++;
            if (!ok) throw new Exception("Behavior regression: " + message);
        }
        private static void Near(double actual, double expected, string message)
        {
            Check(!double.IsNaN(actual) && Math.Abs(actual - expected) < 1e-12, message + ": " + actual + " != " + expected);
        }
        private static RecipeUseGroup Recipe(string id, string kind, int quantity, string recipe)
        {
            RecipeUseGroup row = new RecipeUseGroup(id, kind);
            row.MinQuantity = row.MaxQuantity = quantity;
            row.Variants = 1;
            row.RecipeIds.Add(recipe);
            return row;
        }
        public static int RunBehaviorCases()
        {
            assertions = warnings = 0;
            Names.Clear(); DatadisksByUnlockedItem.Clear();
            string armor = "spider_light_armor_1", helmet = "spider_light_helmet_1";
            foreach (string language in new string[] { "Chitin Shell", "Хитиновый панцирь", "untranslated" })
            {
                Names[armor] = Names[helmet] = language;
                List<RecipeUseGroup> rows = new List<RecipeUseGroup> {
                    Recipe(armor, "craft", 1, "armor_recipe"), Recipe(helmet, "craft", 2, "helmet_recipe") };
                Check(ConsolidateRecipeUseFamilies(rows).Count == 2, "same-name unrelated items must stay separate: " + language);
                DatadisksByUnlockedItem[armor] = new List<string> { "shared_chip" };
                DatadisksByUnlockedItem[helmet] = new List<string> { "shared_chip" };
                Check(ConsolidateRecipeUseFamilies(rows).Count == 2, "a shared chip cannot merge unrelated items");
            }
            Names[armor + "_custom"] = Names[armor];
            DatadisksByUnlockedItem.Clear();
            List<RecipeUseGroup> variants = new List<RecipeUseGroup> {
                Recipe(armor, "craft", 2, "base"), Recipe(armor + "_custom", "craft", 5, "custom") };
            List<RecipeUseGroup> merged = ConsolidateRecipeUseFamilies(variants);
            Check(merged.Count == 1, "canonical custom variants merge");
            Check(merged[0].Variants == 2 && merged[0].MinQuantity == 2 && merged[0].MaxQuantity == 5, "merged count/range preserved");
            Check(merged[0].OutputItemIds.Count == 2 && merged[0].RecipeIds.Count == 2, "all recipe and output identities preserved");
            Check(variants[0].OutputItemIds.Count == 1 && variants[0].RecipeIds.Count == 1, "input rows not mutated");
            DatadisksByUnlockedItem[armor] = new List<string> { "chip_a" };
            DatadisksByUnlockedItem[armor + "_custom"] = new List<string> { "chip_b" };
            Check(ConsolidateRecipeUseFamilies(variants).Count == 2, "independent unlock chips remain distinct");
            DatadisksByUnlockedItem.Clear();
            Check(ConsolidateRecipeUseFamilies(new List<RecipeUseGroup> {
                Recipe(armor, "craft", 1, "a"), Recipe(armor, "station", 1, "b") }).Count == 2, "recipe kinds remain distinct");
            Check(ConsolidateRecipeUseFamilies(null).Count == 0, "null input");
            Check(ConsolidateRecipeUseFamilies(new List<RecipeUseGroup> { null, null }).Count == 0, "null rows");

            // Independent oracle enumerates Bernoulli outcomes and the extra-roll branch.
            foreach (double p in new double[] { 0, .0001, .01, .1, .5, .9999, 1 })
            for (int rolls = 0; rolls <= 10; rolls++)
            foreach (double fraction in new double[] { 0, .1, .3, .5, .9 })
            {
                double expected = (1 - fraction) * EnumeratedChance(p, rolls) + fraction * EnumeratedChance(p, rolls + 1);
                Near(CorpseBonusAtLeastOnceChance(p, rolls + fraction), expected, "fractional bonus probability");
            }
            Near(CorpseBonusAtLeastOnceChance(-1, 4), 0, "negative probability clamped");
            Near(CorpseBonusAtLeastOnceChance(2, .3), .3, "probability above one clamped");
            Near(CorpseBonusAtLeastOnceChance(.5, -1), 0, "negative roll count clamped");
            Near(CorpseBonusAtLeastOnceChance(.5, 2147483648.5), 1, "large roll count cannot overflow int");
            foreach (double invalid in new double[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Check(double.IsNaN(CorpseBonusAtLeastOnceChance(invalid, 1)), "invalid probability hidden");
                Check(double.IsNaN(CorpseBonusAtLeastOnceChance(.5, invalid)), "invalid roll count hidden");
            }

            bool eligible;
            HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Weapon", "AnCom" };
            Near(GetItemDropCategoryWeight(categories, null, false, "AnCom", out eligible), 0, "absent whitelist weight");
            Check(eligible, "absent whitelist admits candidate");
            GetItemDropCategoryWeight(categories, new Dictionary<string, double>(), true, "AnCom", out eligible);
            Check(!eligible, "present empty whitelist rejects candidate");
            Dictionary<string, double> whitelist = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Weapon", 2 }, { "Faction", 5 } };
            Near(GetItemDropCategoryWeight(categories, whitelist, true, "AnCom", out eligible), 5, "maximum matched category/faction bonus");
            Check(eligible, "faction membership admits candidate");
            whitelist = new Dictionary<string, double> { { "Weapon", 0 } };
            Near(GetItemDropCategoryWeight(categories, whitelist, true, null, out eligible), 0, "zero whitelist weight preserved");
            Check(eligible, "zero weight membership is significant");
            whitelist["Weapon"] = -5;
            Near(GetItemDropCategoryWeight(categories, whitelist, true, null, out eligible), 0, "negative bonus floored");
            Check(eligible, "negative weight membership is significant");
            double total;
            Check(TryResolveStrictlyPositiveItemDropTotal(new Dictionary<string, double> { { "a", 2 }, { "b", 3 } }, "test", out total), "positive pool accepted");
            Near(total, 5, "pool total");
            foreach (double invalid in new double[] { 0, -1, double.NaN, double.PositiveInfinity })
            {
                Check(!TryResolveStrictlyPositiveItemDropTotal(new Dictionary<string, double> { { "a", 2 }, { "b", invalid } }, "test", out total), "invalid eligible weight rejects entire pool");
                Near(total, 0, "invalid pool clears output");
            }
            Check(warnings == 4, "invalid pools issue boundary diagnostics");
            Check(!TryResolveStrictlyPositiveItemDropTotal(new Dictionary<string, double> { { "a", double.MaxValue }, { "b", double.MaxValue } }, "test", out total), "total overflow rejected");
            Check(!TryResolveStrictlyPositiveItemDropTotal(null, "test", out total), "missing pool rejected");

            int damage;
            Check(TryRoundAndScaleDamage(11f / 3f, 3, 2, out damage) && damage == 24, "round each fragment before scaling");
            Check(TryRoundAndScaleDamage(2.5f, 1, 1, out damage) && damage == 2, "ties to even");
            Check(TryRoundAndScaleDamage(3.5f, 1, 1, out damage) && damage == 4, "odd midpoint");
            Check(!TryRoundAndScaleDamage(1f, int.MaxValue, 2, out damage) && damage == 0, "aggregate overflow rejected");
            foreach (float invalid in new float[] { float.NaN, float.PositiveInfinity, -1, 2147483648f })
                Check(!TryRoundAndScaleDamage(invalid, 1, 1, out damage) && damage == 0, "invalid damage rejected");
            Check(!TryRoundAndScaleDamage(1, 0, 1, out damage), "zero count rejected");
            Check(!TryRoundAndScaleDamage(1, 1, -1, out damage), "negative count rejected");

            Check(GetLootModifierUnavailableNoteKey(false) == "loot.note.container_modifier_basic", "blocked manual control must not be suggested");
            Check(GetLootModifierUnavailableNoteKey(true) == "loot.note.container_modifier_unavailable", "available manual mode can be suggested");
            Check(GetLootManualUnavailableNoteKey(false) == "loot.note.manual_compatibility", "unsupported manual calculation explained");
            Check(GetLootManualUnavailableNoteKey(true) == "loot.note.manual_unavailable", "transient manual unavailability does not blame an update");
            Check(GetLootContainerUnavailableNoteKey(false, false) == "loot.note.container_save_compatibility", "compatibility loss explained");
            Check(GetLootContainerUnavailableNoteKey(false, true) == "loot.note.container_calculation_unavailable", "transient calculation loss explained");
            Check(GetLootContainerUnavailableNoteKey(true, true) == "loot.note.container_save_unavailable", "missing data explained separately");
            RunCompatibilityCases();
            RunContainerCases();
            RunScavengerCases();
            RunAmputationCases();
            return assertions;
        }
        private static double EnumeratedChance(double p, int rolls)
        {
            double hit = 0;
            for (int mask = 1; mask < (1 << rolls); mask++)
            {
                double weight = 1;
                for (int bit = 0; bit < rolls; bit++) weight *= (mask & (1 << bit)) != 0 ? p : 1 - p;
                hit += weight;
            }
            return hit;
        }
    }
}
