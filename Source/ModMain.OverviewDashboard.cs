using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ItemIntelligence
{
    /// <summary>
    /// v1.7.40-test11+: adaptive player-facing landing page for an inspected item.
    /// It projects only already-owned core/feature indexes and never requests Loot,
    /// market or faction warmups. The first F2 therefore keeps the established
    /// deferred-work contract while Overview becomes a useful item dashboard.
    /// </summary>
    public static partial class ModMain
    {
        private enum OverviewRelationKind
        {
            None = 0, UsedInRecipes = 1, RecipeIngredients = 2, ObtainableByDisassembly = 3,
            DisassemblesInto = 4, CompatibleAmmo = 5, CompatibleWeapons = 6
        }

        private sealed class OverviewPreview
        {
            public readonly string Header;
            public readonly List<string> ItemIds;
            public readonly BrowserTabId TargetTab;
            public readonly OverviewRelationKind Kind;

            public OverviewPreview(string header, List<string> itemIds, BrowserTabId targetTab, OverviewRelationKind kind)
            {
                Header = header ?? string.Empty;
                ItemIds = itemIds ?? new List<string>();
                TargetTab = targetTab;
                Kind = kind;
            }
        }

        private static void BuildBrowserOverview(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            int playerContentStart = BrowserLines.Count;

            string relationId = ResolveStaticRelationItemId(itemId);
            if (string.IsNullOrEmpty(relationId)) relationId = itemId;

            List<WeaponModeDescriptor> modes = ShowAmmoRelations
                ? GetWeaponModesForItem(itemId)
                : new List<WeaponModeDescriptor>();

            if (modes.Count > 0)
                AppendOverviewCombat(modes);

            // QuasiPacts get their exact scripted Baron source immediately on the
            // landing page without starting the heavy Loot warmup.
            AppendOverviewBaronSpecial(itemId);

            // Resolve the visual relationship first so the same fact is not printed twice
            // as both a summary action and a preview heading.
            OverviewPreview preview = ResolveOverviewPreview(itemId, relationId);
            AppendOverviewRelationships(itemId, relationId, preview);

            if (UsesInheritedStaticRelations(itemId))
                AddModifiedRelationBrowserNote(itemId);

            if (preview != null && preview.ItemIds.Count > 0)
                AppendOverviewPreview(preview);

            AppendOverviewChipUnlocks(itemId);
            if (BrowserLines.Count == playerContentStart)
                BrowserLines.Add(BrowserLine.FullNote(Ui("ui.overview_no_core_links")));
            AppendBrowserModderOverview(itemId);
        }

        private static void AppendOverviewCombat(List<WeaponModeDescriptor> modes)
        {
            // Two fixed value columns read much faster than a slash-combined right label.
            // This allocates no per-row objects; it only enables the existing pooled texts.
            EnsureBrowserFactionColumnsUi();
            int combatStart = BrowserLines.Count;
            BrowserLines.Add(BrowserLine.OverviewCombatHeader(
                Ui("ui.overview_combat"), Ui("ui.overview_damage_ap_short"), Ui("ui.overview_crit_short")));

            for (int i = 0; i < modes.Count; i++)
            {
                WeaponModeDescriptor mode = modes[i];
                if (mode == null || mode.Stats == null || string.IsNullOrEmpty(mode.Key)) continue;
                string label = ResolveWeaponModeDisplayLabel(mode);
                if (string.IsNullOrEmpty(label)) continue;

                int normalMin, normalMax, critMin, critMax;
                bool haveNormal = TryCalculateWeaponModeDamagePerAp(mode.Key, mode.Stats, out normalMin, out normalMax);
                bool haveCrit = TryCalculateWeaponModeCriticalDamagePerAp(mode.Key, mode.Stats, out critMin, out critMax);
                if (!haveNormal && !haveCrit) continue;
                BrowserLines.Add(BrowserLine.OverviewCombatRow(
                    label, mode.Key,
                    haveNormal ? FormatWeaponModeDamagePerAp(normalMin, normalMax) : "—",
                    haveCrit ? FormatWeaponModeDamagePerAp(critMin, critMax) : "—"));
            }

            if (BrowserLines.Count == combatStart + 1)
                BrowserLines.RemoveAt(combatStart);
        }

        private static void AppendOverviewRelationships(string itemId, string relationId, OverviewPreview preview)
        {
            OverviewMagnumState magnumState = GetOverviewMagnumState(itemId);
            int magnum = magnumState != null ? magnumState.CurrentRequired : 0;
            bool hasMagnumRelations = magnumState != null && magnumState.HasRelations;
            int used = ShowRecipes ? GetUniqueRecipeOutputCount(itemId) : 0;
            int crafted = ShowRecipes ? GetStaticRelationListCount(CraftedFromRecipes, itemId) : 0;
            int disassemblyOutputs = _compatDisassembly && _disassemblyWarmupComplete ? GetDisassemblyOutputCount(itemId) : 0;
            int disassemblySources = _compatDisassembly && _disassemblyWarmupComplete ? GetDisassemblySourceCount(itemId) : 0;

            WeaponInfo weapon = null;
            List<string> compatibleWeapons = null;
            int ammoCount = 0;
            bool isWeapon = ShowAmmoRelations &&
                WeaponsByItem.TryGetValue(relationId, out weapon) && weapon != null;
            bool isAmmo = ShowAmmoRelations &&
                CompatibleWeaponsByAmmo.TryGetValue(relationId, out compatibleWeapons) &&
                compatibleWeapons != null && compatibleWeapons.Count > 0;
            if (isWeapon) ammoCount = weapon.CompatibleAmmo.Count;
            else if (isAmmo) ammoCount = compatibleWeapons.Count;

            OverviewRelationKind previewKind = preview == null ? OverviewRelationKind.None : preview.Kind;
            bool any = hasMagnumRelations ||
                (used > 0 && previewKind != OverviewRelationKind.UsedInRecipes) ||
                (crafted > 0 && previewKind != OverviewRelationKind.RecipeIngredients) ||
                (disassemblyOutputs > 0 && previewKind != OverviewRelationKind.DisassemblesInto) ||
                (disassemblySources > 0 && previewKind != OverviewRelationKind.ObtainableByDisassembly) ||
                (ammoCount > 0 && previewKind != OverviewRelationKind.CompatibleAmmo && previewKind != OverviewRelationKind.CompatibleWeapons);
            if (!any) return;

            BrowserLines.Add(BrowserLine.Section(Ui("ui.overview_relationships")));

            if (hasMagnumRelations)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.overview_magnum_research"),
                    FormatOverviewMagnumStatus(magnumState),
                    BrowserAction.SwitchTab(BrowserTabId.Magnum)));
            }

            if (used > 0 && previewKind != OverviewRelationKind.UsedInRecipes)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.overview_used_in_recipes"),
                    used.ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(BrowserTabId.Recipes)));
            }

            if (crafted > 0 && previewKind != OverviewRelationKind.RecipeIngredients)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.overview_crafted_by_recipes"),
                    crafted.ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(BrowserTabId.Recipes)));
            }

            if (disassemblySources > 0 && previewKind != OverviewRelationKind.ObtainableByDisassembly)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.overview_obtainable_by_disassembly"),
                    disassemblySources.ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(BrowserTabId.Recipes)));
            }

            if (disassemblyOutputs > 0 && previewKind != OverviewRelationKind.DisassemblesInto)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.overview_disassembles_into"),
                    disassemblyOutputs.ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(BrowserTabId.Recipes)));
            }

            if (ammoCount > 0 && previewKind != OverviewRelationKind.CompatibleAmmo && previewKind != OverviewRelationKind.CompatibleWeapons)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui(isWeapon ? "ui.overview_compatible_ammo" : "ui.overview_compatible_weapons"),
                    ammoCount.ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(BrowserTabId.Ammo)));
            }
        }


        private static OverviewPreview ResolveOverviewPreview(string itemId, string relationId)
        {
            if (ShowAmmoRelations)
            {
                WeaponInfo weapon;
                if (WeaponsByItem.TryGetValue(relationId, out weapon) && weapon != null && weapon.CompatibleAmmo.Count > 0)
                    return NewOverviewPreview(Ui("ui.overview_compatible_ammo"), weapon.CompatibleAmmo, BrowserTabId.Ammo, OverviewRelationKind.CompatibleAmmo);

                List<string> weapons;
                if (CompatibleWeaponsByAmmo.TryGetValue(relationId, out weapons) && weapons != null && weapons.Count > 0)
                    return NewOverviewPreview(Ui("ui.overview_compatible_weapons"), weapons, BrowserTabId.Ammo, OverviewRelationKind.CompatibleWeapons);
            }

            List<OverviewPreview> candidates = new List<OverviewPreview>();
            if (ShowRecipes)
            {
                AddOverviewPreviewCandidate(candidates,
                    Ui("ui.overview_used_in_recipes"), CollectRecipeOutputIds(relationId), BrowserTabId.Recipes, OverviewRelationKind.UsedInRecipes);
                AddOverviewPreviewCandidate(candidates,
                    Ui("ui.overview_recipe_ingredients"), CollectRecipeIngredientIds(relationId, itemId), BrowserTabId.Recipes, OverviewRelationKind.RecipeIngredients);
            }

            if (_compatDisassembly && _disassemblyWarmupComplete)
            {
                AddOverviewPreviewCandidate(candidates,
                    Ui("ui.overview_obtainable_by_disassembly"), CollectDisassemblySourceIds(itemId), BrowserTabId.Recipes, OverviewRelationKind.ObtainableByDisassembly);
                AddOverviewPreviewCandidate(candidates,
                    Ui("ui.overview_disassembles_into"), CollectDisassemblyOutputIds(itemId), BrowserTabId.Recipes, OverviewRelationKind.DisassemblesInto);
            }

            OverviewPreview best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                OverviewPreview candidate = candidates[i];
                if (candidate == null || candidate.ItemIds.Count == 0) continue;
                if (best == null || candidate.ItemIds.Count > best.ItemIds.Count)
                    best = candidate;
            }
            return best;
        }

        private static OverviewPreview NewOverviewPreview(
            string header, IEnumerable<string> ids, BrowserTabId targetTab, OverviewRelationKind kind)
        {
            return new OverviewPreview(header, NormalizeOverviewPreviewIds(ids), targetTab, kind);
        }

        private static void AddOverviewPreviewCandidate(
            List<OverviewPreview> candidates, string header, List<string> ids, BrowserTabId targetTab, OverviewRelationKind kind)
        {
            if (candidates == null || ids == null || ids.Count == 0) return;
            candidates.Add(new OverviewPreview(header, ids, targetTab, kind));
        }

        private static void AppendOverviewPreview(OverviewPreview preview)
        {
            int total = preview.ItemIds.Count;
            BrowserLines.Add(BrowserLine.FullSection(
                preview.Header.ToUpperInvariant() + "  •  " + total.ToString(CultureInfo.InvariantCulture)));

            HashSet<string> shownNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            int shown = 0;
            for (int i = 0; i < preview.ItemIds.Count && shown < 3; i++)
            {
                string candidateId = preview.ItemIds[i];
                string candidateName = NormalizeGameText(LocalizeItem(candidateId));
                if (!shownNames.Add(candidateName)) continue;
                BrowserLines.Add(BrowserLine.ItemAction(candidateId, string.Empty));
                shown++;
            }

            if (total > shown)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    Ui("ui.more_relationships"),
                    "+" + (total - shown).ToString(CultureInfo.InvariantCulture),
                    BrowserAction.SwitchTab(preview.TargetTab)));
            }
        }

        private static List<string> NormalizeOverviewPreviewIds(IEnumerable<string> ids)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    if (string.IsNullOrEmpty(id) || !KnownItemIds.Contains(id) || !seen.Add(id)) continue;
                    result.Add(id);
                }
            }
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.Count; i++)
                names[result[i]] = LocalizeItem(result[i]);
            result.Sort(delegate(string a, string b)
            {
                string an, bn;
                names.TryGetValue(a, out an);
                names.TryGetValue(b, out bn);
                int byName = string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
                return byName != 0 ? byName : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static List<string> CollectRecipeOutputIds(string relationId)
        {
            List<RecipeUse> uses;
            if (!UsedInRecipes.TryGetValue(relationId, out uses) || uses == null)
                return new List<string>();
            List<string> ids = new List<string>();
            for (int i = 0; i < uses.Count; i++)
                if (uses[i] != null) ids.Add(uses[i].OutputItemId);
            return NormalizeOverviewPreviewIds(ids);
        }

        private static List<string> CollectRecipeIngredientIds(string relationId, string inspectedItemId)
        {
            List<RecipeDef> recipes;
            if (!CraftedFromRecipes.TryGetValue(relationId, out recipes) || recipes == null)
                return new List<string>();
            List<string> ids = new List<string>();
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe == null || recipe.Ingredients == null) continue;
                foreach (string ingredientId in recipe.Ingredients.Keys)
                    if (!string.Equals(ingredientId, inspectedItemId, StringComparison.OrdinalIgnoreCase))
                        ids.Add(ingredientId);
            }
            return NormalizeOverviewPreviewIds(ids);
        }

        private static List<string> CollectDisassemblySourceIds(string itemId)
        {
            List<DisassemblySource> sources;
            if (!DisassemblySourcesByOutputItem.TryGetValue(itemId, out sources) || sources == null)
                return new List<string>();
            List<string> ids = new List<string>();
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] != null) ids.Add(sources[i].ItemId);
            return NormalizeOverviewPreviewIds(ids);
        }

        private static List<string> CollectDisassemblyOutputIds(string itemId)
        {
            List<DisassemblyOutput> outputs;
            if (!DisassemblyOutputsByItem.TryGetValue(itemId, out outputs) || outputs == null)
                return new List<string>();
            List<string> ids = new List<string>();
            for (int i = 0; i < outputs.Count; i++)
                if (outputs[i] != null) ids.Add(outputs[i].ItemId);
            return NormalizeOverviewPreviewIds(ids);
        }

        private static void AppendOverviewChipUnlocks(string itemId)
        {
            List<string> chipUnlockItems = GetDatadiskUnlockedItemsSorted(itemId);
            if (chipUnlockItems.Count == 0) return;

            int learnedCount = 0;
            int lockedCount = 0;
            int unknownCount = 0;
            List<int> unlockStatuses = new List<int>(chipUnlockItems.Count);
            for (int i = 0; i < chipUnlockItems.Count; i++)
            {
                bool? learnedState = IsProductionItemUnlocked(chipUnlockItems[i]);
                int status = !learnedState.HasValue ? 2 : (learnedState.Value ? 1 : -1);
                unlockStatuses.Add(status);
                if (status == 1) learnedCount++;
                else if (status == -1) lockedCount++;
                else unknownCount++;
            }

            BrowserLines.Add(BrowserLine.Header(
                Ui("ui.chip_unlocks") + "  •  " + chipUnlockItems.Count.ToString(CultureInfo.InvariantCulture),
                FormatChipUnlockStatusSummary(learnedCount, lockedCount, unknownCount)));
            if (_chipUnlockChanceContractVerified)
                BrowserLines.Add(BrowserLine.ChipNote(Ui("ui.chip_unlock_chance_note")));

            for (int i = 0; i < chipUnlockItems.Count; i++)
            {
                string unlockedItemId = chipUnlockItems[i];
                int hits, total;
                float chance;
                string right = string.Empty;
                if (TryGetDatadiskUnlockChance(itemId, unlockedItemId, out hits, out total, out chance))
                    right = FormatChipUnlockChance(chance);
                BrowserLines.Add(BrowserLine.ChipUnlockAction(unlockedItemId, right, unlockStatuses[i]));
            }
        }

        private static string FormatChipUnlockStatusSummary(int learnedCount, int lockedCount, int unknownCount)
        {
            StringBuilder summary = new StringBuilder();
            AppendChipUnlockStatusPart(summary, learnedCount, Ui("ui.chip_summary_unlocked"));
            AppendChipUnlockStatusPart(summary, lockedCount, Ui("ui.chip_summary_locked"));
            AppendChipUnlockStatusPart(summary, unknownCount, Ui("ui.chip_summary_unknown"));
            return summary.ToString();
        }

        private static void AppendChipUnlockStatusPart(StringBuilder summary, int count, string label)
        {
            if (count <= 0) return;
            if (summary.Length > 0) summary.Append(" • ");
            summary.Append(count.ToString(CultureInfo.InvariantCulture));
            summary.Append(' ');
            summary.Append(label);
        }

        private static int GetOverviewRecipeRelationCount(string itemId)
        {
            string relationId = ResolveStaticRelationItemId(itemId);
            HashSet<string> recipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            List<RecipeUse> uses;
            if (UsedInRecipes.TryGetValue(relationId, out uses) && uses != null)
                for (int i = 0; i < uses.Count; i++)
                    if (uses[i] != null && !string.IsNullOrEmpty(uses[i].RecipeId)) recipes.Add(uses[i].RecipeId);

            List<RecipeDef> crafted;
            if (CraftedFromRecipes.TryGetValue(relationId, out crafted) && crafted != null)
                for (int i = 0; i < crafted.Count; i++)
                    if (crafted[i] != null && !string.IsNullOrEmpty(crafted[i].RecipeId)) recipes.Add(crafted[i].RecipeId);

            return recipes.Count;
        }

        private static void UpdateBrowserStats(string itemId)
        {
            if (_browserStatsText == null) return;
            _browserStatsText.color = new UnityEngine.Color(0.43f, 0.69f, 0.59f, 1f);

            List<string> parts = new List<string>(4);
            int tech;
            if (TryGetExactItemTechLevel(itemId, out tech))
                parts.Add(Ui("stats.tech") + " " + tech.ToString(CultureInfo.InvariantCulture));

            List<WeaponModeDescriptor> modes = ShowAmmoRelations
                ? GetWeaponModesForItem(itemId)
                : new List<WeaponModeDescriptor>();
            if (modes.Count > 0)
                parts.Add(Ui("stats.modes") + " " + modes.Count.ToString(CultureInfo.InvariantCulture));

            if (ShowAmmoRelations && parts.Count < 4)
            {
                string relationId = ResolveStaticRelationItemId(itemId);
                WeaponInfo statsWeapon;
                List<string> statsCompatibleWeapons;
                if (WeaponsByItem.TryGetValue(relationId, out statsWeapon) && statsWeapon != null && statsWeapon.CompatibleAmmo.Count > 0)
                {
                    parts.Add(Ui("stats.ammo") + " " + statsWeapon.CompatibleAmmo.Count.ToString(CultureInfo.InvariantCulture));
                }
                else if (CompatibleWeaponsByAmmo.TryGetValue(relationId, out statsCompatibleWeapons) &&
                         statsCompatibleWeapons != null && statsCompatibleWeapons.Count > 0)
                {
                    parts.Add(Ui("stats.weapons") + " " + statsCompatibleWeapons.Count.ToString(CultureInfo.InvariantCulture));
                }
            }

            OverviewMagnumState magnumState = GetOverviewMagnumState(itemId);
            int magnum = magnumState != null ? magnumState.CurrentRequired : 0;
            if (magnum > 0 && parts.Count < 4)
                parts.Add(Ui("stats.magnum") + " " + magnum.ToString(CultureInfo.InvariantCulture));

            int recipes = ShowRecipes ? GetOverviewRecipeRelationCount(itemId) : 0;
            if (recipes > 0 && parts.Count < 4)
                parts.Add(Ui("stats.recipes") + " " + recipes.ToString(CultureInfo.InvariantCulture));

            SetBrowserTextIfChanged(_browserStatsText, NormalizeModUiText(string.Join("   •   ", parts)));
        }
    }
}
