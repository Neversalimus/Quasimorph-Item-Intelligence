using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.37: cold browser UI is feature-owned and constructed only when used.
        // This keeps the first F2 critical path limited to the shell + common row pool.
        // BrowserRowFaction* is a shared four-column pool used by Magnum, Trade, Factions and Loot.
        private static void EnsureBrowserSearchDropdownUi()
        {
            if (_browserSearchDropdown != null || _inspectorRoot == null) return;
            float started = Time.realtimeSinceStartup;
            CreateBrowserSearchDropdown();
            Debug.Log("[ItemIntelligence][LazyUi] searchDropdown=" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

        private static void EnsureBrowserCatalogUi()
        {
            if (_browserCatalogPanel != null || _inspectorRoot == null) return;
            float started = Time.realtimeSinceStartup;
            CreateBrowserCatalogUi();
            ApplyBrowserInterfaceIconVisibility(true);
            UpdateBrowserCatalogControls();
            Debug.Log("[ItemIntelligence][LazyUi] catalog=" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

        private static void EnsureBrowserFactionColumnsUi()
        {
            if (_inspectorRoot == null || BrowserRowFactionReward[0] != null) return;
            float started = Time.realtimeSinceStartup;
            const float rowHeight = 39f;
            for (int i = 0; i < BrowserVisibleRows; i++)
            {
                GameObject row = BrowserRowRoots[i];
                if (row == null) continue;

                GameObject rewardGo = CreateBrowserText("FactionReward", row.transform,
                    new Vector2(304f, 0f), new Vector2(82f, rowHeight - 2f),
                    15f, new Color(0.86f, 0.85f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.Center);
                GameObject unlockGo = CreateBrowserText("FactionUnlock", row.transform,
                    new Vector2(386f, 0f), new Vector2(78f, rowHeight - 2f),
                    15f, new Color(0.86f, 0.85f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.Center);
                GameObject currentGo = CreateBrowserText("FactionCurrent", row.transform,
                    new Vector2(464f, 0f), new Vector2(104f, rowHeight - 2f),
                    15f, new Color(0.86f, 0.85f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.Center);
                GameObject stateGo = CreateBrowserText("FactionState", row.transform,
                    new Vector2(568f, 0f), new Vector2(94f, rowHeight - 2f),
                    13f, new Color(0.86f, 0.85f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.Center);

                BrowserRowFactionReward[i] = rewardGo.GetComponent<TMP_Text>();
                BrowserRowFactionUnlock[i] = unlockGo.GetComponent<TMP_Text>();
                BrowserRowFactionCurrent[i] = currentGo.GetComponent<TMP_Text>();
                BrowserRowFactionState[i] = stateGo.GetComponent<TMP_Text>();
                rewardGo.SetActive(false);
                unlockGo.SetActive(false);
                currentGo.SetActive(false);
                stateGo.SetActive(false);
            }
            Debug.Log("[ItemIntelligence][LazyUi] factionColumns=" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

        private static void EnsureBrowserRecipeContextUi()
        {
            if (_inspectorRoot == null || BrowserRowChipIcons[0] != null) return;
            float started = Time.realtimeSinceStartup;
            EnsureQiiMarkerSprites();
            const float rowHeight = 39f;
            for (int i = 0; i < BrowserVisibleRows; i++)
            {
                GameObject row = BrowserRowRoots[i];
                if (row == null) continue;

                GameObject statusGo = new GameObject("ChipStatus");
                statusGo.transform.SetParent(row.transform, false);
                RectTransform statusRt = statusGo.AddComponent<RectTransform>();
                statusRt.anchorMin = new Vector2(0f, 0.5f);
                statusRt.anchorMax = new Vector2(0f, 0.5f);
                statusRt.pivot = new Vector2(0f, 0.5f);
                statusRt.anchoredPosition = new Vector2(5f, 0f);
                statusRt.sizeDelta = new Vector2(14f, 14f);
                UnityEngine.UI.Image statusImage = statusGo.AddComponent<UnityEngine.UI.Image>();
                statusImage.preserveAspect = true;
                statusImage.raycastTarget = false;
                statusImage.enabled = false;

                GameObject chipGo = new GameObject("RecipeChip");
                chipGo.transform.SetParent(row.transform, false);
                RectTransform chipRt = chipGo.AddComponent<RectTransform>();
                chipRt.anchorMin = new Vector2(0f, 0.5f);
                chipRt.anchorMax = new Vector2(0f, 0.5f);
                chipRt.pivot = new Vector2(0f, 0.5f);
                chipRt.anchoredPosition = new Vector2(23f, 0f);
                chipRt.sizeDelta = new Vector2(22f, 22f);
                UnityEngine.UI.Image chipImage = chipGo.AddComponent<UnityEngine.UI.Image>();
                chipImage.preserveAspect = true;
                chipImage.raycastTarget = false;
                chipImage.enabled = false;
                AttachBrowserItemTooltipTarget(chipImage);
                AttachBrowserItemIconNavigation(chipImage, i, true);

                BrowserRowChipIcons[i] = chipImage;
                BrowserRowChipStatusIcons[i] = statusImage;
            }
            Debug.Log("[ItemIntelligence][LazyUi] recipeContext=" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

        private static bool BrowserLinesNeedRecipeContextUi()
        {
            for (int i = 0; i < BrowserLines.Count; i++)
            {
                BrowserLine line = BrowserLines[i];
                if (line != null && (line.ShowRecipeChipContext || line.RowKind == BrowserRowKind.ChipUnlock))
                    return true;
            }
            return false;
        }

        private static void EnsureBrowserLootProgressUi()
        {
            if (_lootProgressRoot != null || _inspectorRoot == null) return;
            float started = Time.realtimeSinceStartup;
            CreateLootProgressUi();
            Debug.Log("[ItemIntelligence][LazyUi] lootProgress=" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

    }
}
