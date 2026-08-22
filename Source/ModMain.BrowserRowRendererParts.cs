using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Feature-owned renderer parts extracted from RenderBrowserRowsOnly. The context is a
    /// value type, so decomposition does not add one heap allocation per pooled row.
    /// </summary>
    public static partial class ModMain
    {
        private struct BrowserRowRenderContext
        {
            internal int Slot;
            internal BrowserLine Line;
            internal GameObject Root;
            internal TMP_Text Left;
            internal TMP_Text Right;
            internal TMP_Text FactionReward;
            internal TMP_Text FactionUnlock;
            internal TMP_Text FactionCurrent;
            internal TMP_Text FactionState;
            internal Image Background;
            internal Image ItemIcon;
            internal Image ChipIcon;
            internal Image ChipStatusIcon;
            internal Button RowButton;
            internal Outline RowOutline;
            internal RectTransform ItemRt;
            internal RectTransform ChipRt;
            internal RectTransform StatusRt;
            internal RectTransform LeftRt;
            internal RectTransform RightRt;
            internal bool Actionable;
            internal bool ShowIcon;
            internal bool ShowRecipeContext;
            internal bool ShowChipUnlockStatus;
            internal string LeftText;
        }

        private static void InitializeBrowserRowRenderContext(
            ref BrowserRowRenderContext context, int slot, BrowserLine line, GameObject root, TMP_Text left, TMP_Text right)
        {
            context.Slot = slot;
            context.Line = line;
            context.Root = root;
            context.Left = left;
            context.Right = right;
            context.FactionReward = BrowserRowFactionReward[slot];
            context.FactionUnlock = BrowserRowFactionUnlock[slot];
            context.FactionCurrent = BrowserRowFactionCurrent[slot];
            context.FactionState = BrowserRowFactionState[slot];
            context.Background = BrowserRowBackground[slot];
            context.ItemIcon = BrowserRowIcons[slot];
            context.ChipIcon = BrowserRowChipIcons[slot];
            context.ChipStatusIcon = BrowserRowChipStatusIcons[slot];
        }

        private static void PrepareBrowserRowForRender(ref BrowserRowRenderContext ctx)
        {
            ResetBrowserLinkPresentation(ctx.Slot);
            InitializeBrowserRowInteraction(ref ctx);
            ResolveBrowserRowVisuals(ref ctx);
            RenderBrowserRowChipContext(ref ctx);
            PrepareBrowserRowBaseLayout(ref ctx);
        }

        private static void InitializeBrowserRowInteraction(ref BrowserRowRenderContext ctx)
        {
            int i = ctx.Slot; BrowserLine line = ctx.Line; GameObject root = ctx.Root; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right;
            TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock; TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState;

            SetBrowserWeaponModeTooltipTarget(root, string.Empty, string.Empty, false);
            SetBrowserRaycastTargetIfChanged(left, line != null && line.LeftContentKind == BrowserLeftContentKind.Item && IsKnownItemId(line.Left));
            SetBrowserActiveIfChanged(root, true);
            
            if (factionReward != null) SetBrowserActiveIfChanged(factionReward.gameObject, false);
            if (factionUnlock != null) SetBrowserActiveIfChanged(factionUnlock.gameObject, false);
            if (factionCurrent != null) SetBrowserActiveIfChanged(factionCurrent.gameObject, false);
            if (factionState != null) SetBrowserActiveIfChanged(factionState.gameObject, false);
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, true);
            
            Button rowButton = BrowserRowButtons[i];
            bool actionable = !line.Action.IsNone;
            if (rowButton != null)
            {
                SetBrowserInteractableIfChanged(rowButton, actionable);
                ColorBlock rowColors = rowButton.colors;
                rowColors.normalColor = Color.white;
                rowColors.highlightedColor = line.RowKind == BrowserRowKind.LootSectionHeader
                    ? new Color(0.58f, 1.00f, 0.70f, 1f)
                    : new Color(0.72f, 1.00f, 0.78f, 1f);
                rowColors.pressedColor = new Color(1.00f, 0.88f, 0.52f, 1f);
                rowColors.selectedColor = rowColors.highlightedColor;
                rowColors.disabledColor = Color.white;
                rowButton.colors = rowColors;
            }
            
            Outline rowOutline = BrowserRowOutlines[i];
            if (rowOutline != null)
            {
                SetBrowserOutlineEnabledIfChanged(rowOutline, actionable);
                SetBrowserOutlineColorIfChanged(rowOutline, line.RowKind == BrowserRowKind.LootSectionHeader
                    ? new Color(0.50f, 0.92f, 0.66f, 0.98f)
                    : new Color(0.42f, 0.80f, 0.59f, 0.90f));
                SetBrowserOutlineDistanceIfChanged(rowOutline, line.RowKind == BrowserRowKind.LootSectionHeader
                    ? new Vector2(2f, -2f) : new Vector2(1f, -1f));
            }
            
            string leftText = line.Left ?? string.Empty;
            if (line.LeftContentKind == BrowserLeftContentKind.Item) leftText = LocalizeItem(leftText);
            else if (line.LeftContentKind == BrowserLeftContentKind.MagnumPerk) leftText = LocalizeMagnumPerk(leftText);

            ctx.RowButton = rowButton;
            ctx.Actionable = actionable;
            ctx.RowOutline = rowOutline;
            ctx.LeftText = leftText;
        }

        private static void ResolveBrowserRowVisuals(ref BrowserRowRenderContext ctx)
        {
            int i = ctx.Slot; BrowserLine line = ctx.Line; GameObject root = ctx.Root; string leftText = ctx.LeftText;

            bool showIcon = false;
            Image itemIconImage = BrowserRowIcons[i];
            Image chipIconImage = BrowserRowChipIcons[i];
            Image chipStatusImage = BrowserRowChipStatusIcons[i];
            
            if (itemIconImage != null)
            {
                SetBrowserItemTooltipTarget(itemIconImage, string.Empty, false);
                SetBrowserImageSpriteIfChanged(itemIconImage, null);
                SetBrowserImageEnabledIfChanged(itemIconImage, false);
                SetBrowserGraphicColorIfChanged(itemIconImage, Color.white);
            }
            if (chipIconImage != null)
            {
                SetBrowserItemTooltipTarget(chipIconImage, string.Empty, false);
                SetBrowserImageSpriteIfChanged(chipIconImage, null);
                SetBrowserImageEnabledIfChanged(chipIconImage, false);
                SetBrowserGraphicColorIfChanged(chipIconImage, Color.white);
            }
            if (chipStatusImage != null)
            {
                SetBrowserImageSpriteIfChanged(chipStatusImage, null);
                SetBrowserImageEnabledIfChanged(chipStatusImage, false);
                SetBrowserGraphicColorIfChanged(chipStatusImage, Color.white);
            }
            
            if (line.LeftContentKind == BrowserLeftContentKind.Item && itemIconImage != null)
            {
                Sprite icon = TryResolveItemSmallIcon(line.Left);
                if (icon != null)
                {
                    SetBrowserImageSpriteIfChanged(itemIconImage, icon);
                    SetBrowserImageEnabledIfChanged(itemIconImage, true);
                    SetBrowserItemTooltipTarget(itemIconImage, line.Left, true, true);
                    showIcon = true;
                }
            }
            else if (line.LeftContentKind == BrowserLeftContentKind.WeaponMode && itemIconImage != null)
            {
                Sprite modeIcon = TryResolveWeaponModeSmallIcon(line.ContainerIconId);
                if (modeIcon != null)
                {
                    SetBrowserImageSpriteIfChanged(itemIconImage, modeIcon);
                    SetBrowserImageEnabledIfChanged(itemIconImage, true);
                    SetBrowserGraphicColorIfChanged(itemIconImage, Color.white);
                    showIcon = true;
                }
            }
            else if (!string.IsNullOrEmpty(line.FactionId) && itemIconImage != null)
            {
                Sprite factionIcon = TryResolveFactionSmallIcon(
                    line.FactionId, ResolveFactionById(line.FactionId));
                if (factionIcon != null)
                {
                    SetBrowserImageSpriteIfChanged(itemIconImage, factionIcon);
                    SetBrowserImageEnabledIfChanged(itemIconImage, true);
                    SetBrowserGraphicColorIfChanged(itemIconImage, Color.white);
                    showIcon = true;
                }
            }
            
            else if (!string.IsNullOrEmpty(line.ContainerIconId) && itemIconImage != null)
            {
                Sprite containerIcon = TryResolveLootContainerSmallIcon(line.ContainerIconId);
                if (containerIcon != null)
                {
                    SetBrowserImageSpriteIfChanged(itemIconImage, containerIcon);
                    SetBrowserImageEnabledIfChanged(itemIconImage, true);
                    SetBrowserGraphicColorIfChanged(itemIconImage, Color.white);
                    showIcon = true;
                }
            }
            
            if (line.LeftContentKind == BrowserLeftContentKind.WeaponMode)
                SetBrowserWeaponModeTooltipTarget(root, line.ContainerIconId, leftText, true);

            ctx.ShowIcon = showIcon;
        }

        private static void RenderBrowserRowChipContext(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; Image chipIconImage = ctx.ChipIcon; Image chipStatusImage = ctx.ChipStatusIcon;

            bool showRecipeContext = line.ShowRecipeChipContext;
            bool showChipUnlockStatus = line.RowKind == BrowserRowKind.ChipUnlock;
            if (showRecipeContext && chipIconImage != null)
            {
                if (!string.IsNullOrEmpty(line.ChipItemId))
                {
                    Sprite chipSprite = TryResolveItemSmallIcon(line.ChipItemId);
                    if (chipSprite != null)
                    {
                        SetBrowserImageSpriteIfChanged(chipIconImage, chipSprite);
                        SetBrowserGraphicColorIfChanged(chipIconImage, Color.white);
                    }
                    else
                    {
                        SetBrowserImageSpriteIfChanged(chipIconImage, _qiiNoDatadiskSprite);
                        SetBrowserGraphicColorIfChanged(chipIconImage, new Color(0.48f, 0.62f, 0.56f, 1f));
                    }
                }
                else
                {
                    SetBrowserImageSpriteIfChanged(chipIconImage, _qiiNoDatadiskSprite);
                    SetBrowserGraphicColorIfChanged(chipIconImage, new Color(0.48f, 0.62f, 0.56f, 1f));
                }
                SetBrowserImageEnabledIfChanged(chipIconImage, chipIconImage.sprite != null);
                SetBrowserItemTooltipTarget(
                    chipIconImage,
                    line.ChipItemId,
                    chipIconImage.enabled && !string.IsNullOrEmpty(line.ChipItemId),
                    true);
            }
            
            if (((showRecipeContext && !string.IsNullOrEmpty(line.ChipItemId)) || showChipUnlockStatus) && chipStatusImage != null)
            {
                if (line.ChipStatus == BrowserChipStatus.Unlocked || line.ChipStatus == BrowserChipStatus.Unknown)
                {
                    SetBrowserImageSpriteIfChanged(chipStatusImage, _qiiUnlockedMarkerSprite);
                    SetBrowserGraphicColorIfChanged(chipStatusImage, line.ChipStatus == BrowserChipStatus.Unlocked
                        ? new Color(0.46f, 0.92f, 0.54f, 1f)
                        : new Color(0.92f, 0.82f, 0.38f, 1f));
                    SetBrowserImageEnabledIfChanged(chipStatusImage, chipStatusImage.sprite != null);
                }
                else if (line.ChipStatus == BrowserChipStatus.Locked)
                {
                    SetBrowserImageSpriteIfChanged(chipStatusImage, _qiiLockedMarkerSprite);
                    SetBrowserGraphicColorIfChanged(chipStatusImage, new Color(0.94f, 0.34f, 0.30f, 1f));
                    SetBrowserImageEnabledIfChanged(chipStatusImage, chipStatusImage.sprite != null);
                }
            }

            ctx.ShowRecipeContext = showRecipeContext;
            ctx.ShowChipUnlockStatus = showChipUnlockStatus;
        }

        private static void PrepareBrowserRowBaseLayout(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right;
            Image itemIconImage = ctx.ItemIcon; Image chipIconImage = ctx.ChipIcon; Image chipStatusImage = ctx.ChipStatusIcon;
            bool showIcon = ctx.ShowIcon; bool showRecipeContext = ctx.ShowRecipeContext; bool showChipUnlockStatus = ctx.ShowChipUnlockStatus; bool actionable = ctx.Actionable;

            RectTransform itemRt = itemIconImage == null ? null : itemIconImage.rectTransform;
            RectTransform chipRt = chipIconImage == null ? null : chipIconImage.rectTransform;
            RectTransform statusRt = chipStatusImage == null ? null : chipStatusImage.rectTransform;
            RectTransform leftRt = left.rectTransform;
            RectTransform rightRt = right.rectTransform;
            
            // The same fixed row objects are reused at every scroll position and tab. Reset
            // typography before applying a specialized table layout so Loot/Faction
            // rows cannot leak their font sizes or styles into normal browser rows.
            ResetBrowserRowTextFit(left, right);
            SetBrowserFontSizeIfChanged(left, 18f);
            SetBrowserFontSizeIfChanged(right, 16f);
            SetBrowserFontStyleIfChanged(left, FontStyles.Normal);
            SetBrowserFontStyleIfChanged(right, FontStyles.Normal);
            SetBrowserAlignmentIfChanged(right, TextAlignmentOptions.MidlineRight);
            
            if (showRecipeContext)
            {
                SetBrowserRectPositionIfChanged(statusRt, 5f, 0f);
                SetBrowserRectPositionIfChanged(chipRt, 23f, 0f);
                SetBrowserRectPositionIfChanged(itemRt, 51f, 0f);
            }
            else if (showChipUnlockStatus)
            {
                SetBrowserRectPositionIfChanged(statusRt, 6f, 0f);
                SetBrowserRectPositionIfChanged(itemRt, 28f, 0f);
            }
            else if (itemRt != null)
            {
                SetBrowserRectPositionIfChanged(itemRt, 8f, 0f);
            }
            
            bool needsRightColumn = actionable || !string.IsNullOrEmpty(line.Right);
            ApplyBrowserStandardRowLayout(
                leftRt, rightRt, showIcon, showRecipeContext, showChipUnlockStatus, needsRightColumn);
            if (showRecipeContext && right != null)
            {
                SetBrowserAutoSizingIfChanged(right, true);
                SetBrowserFontSizeMinIfChanged(right, 10.5f);
                SetBrowserFontSizeMaxIfChanged(right, 16f);
                SetBrowserOverflowIfChanged(right, TextOverflowModes.Ellipsis);
            }

            ctx.ItemRt = itemRt;
            ctx.ChipRt = chipRt;
            ctx.StatusRt = statusRt;
            ctx.LeftRt = leftRt;
            ctx.RightRt = rightRt;
        }

        private static void RenderBrowserRowContent(ref BrowserRowRenderContext ctx)
        {
            BrowserRowKind kind = ctx.Line.RowKind;
            if (kind == BrowserRowKind.OverviewCombatHeader || kind == BrowserRowKind.OverviewCombatRow)
            {
                RenderBrowserOverviewCombatRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.FactionRewardHeader || kind == BrowserRowKind.FactionReward)
            {
                RenderBrowserFactionRewardRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.LootHeader || kind == BrowserRowKind.LootRow)
            {
                RenderBrowserLootRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.LootHeaderSixColumns || kind == BrowserRowKind.LootRowSixColumns)
            {
                RenderBrowserLootSixColumnRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.LootSpecialHeader || kind == BrowserRowKind.LootSpecialRow)
            {
                RenderBrowserLootSpecialRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.MagnumResearch)
            {
                RenderBrowserMagnumResearchRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.TradeHeader || kind == BrowserRowKind.TradeStation || kind == BrowserRowKind.TradeStationCard)
            {
                RenderBrowserTradeRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.BaronLootHeader || kind == BrowserRowKind.BaronLootRow)
            {
                RenderBrowserBaronLootRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.LootSectionHeader)
            {
                RenderBrowserLootSectionHeader(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.FullNote || kind == BrowserRowKind.FullSection)
            {
                RenderBrowserFullWidthTextRow(ref ctx);
                return;
            }
            if (kind == BrowserRowKind.ChipNote)
            {
                RenderBrowserChipNoteRow(ref ctx);
                return;
            }
            RenderBrowserDefaultRow(ref ctx);
        }

        private static void RenderBrowserOverviewCombatRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line;
            TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock;
            Image bg = ctx.Background; RectTransform leftRt = ctx.LeftRt; bool showIcon = ctx.ShowIcon; string leftText = ctx.LeftText;
            ApplyBrowserCombatColumns(
                leftRt, showIcon, right, factionReward, factionUnlock,
                line.ColumnReward, line.ColumnUnlock,
                line.RowKind == BrowserRowKind.OverviewCombatHeader);
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            
            if (line.RowKind == BrowserRowKind.OverviewCombatHeader)
            {
                Color headerColor = new Color(0.74f, 0.86f, 0.62f, 1f);
                SetBrowserFontSizeIfChanged(left, 15f);
                SetBrowserFontStyleIfChanged(left, FontStyles.Bold);
                SetBrowserGraphicColorIfChanged(left, headerColor);
                SetLootColumnHeaderStyle(factionReward, new Color(0.50f, 0.70f, 0.61f, 1f));
                SetLootColumnHeaderStyle(factionUnlock, new Color(0.50f, 0.70f, 0.61f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.032f, 0.092f, 0.073f, 0.95f));
            }
            else
            {
                SetBrowserFontSizeIfChanged(left, 16f);
                if (factionReward != null) SetBrowserGraphicColorIfChanged(factionReward, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (factionUnlock != null) SetBrowserGraphicColorIfChanged(factionUnlock, new Color(0.92f, 0.86f, 0.52f, 1f));
            }
        }

        private static void RenderBrowserFactionRewardRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward;
            TMP_Text factionUnlock = ctx.FactionUnlock; TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState;
            Image bg = ctx.Background; RectTransform leftRt = ctx.LeftRt; bool showIcon = ctx.ShowIcon; bool actionable = ctx.Actionable; string leftText = ctx.LeftText;
            if (factionReward != null) { SetBrowserFontSizeIfChanged(factionReward, 15f); SetBrowserFontStyleIfChanged(factionReward, FontStyles.Normal); }
            if (factionUnlock != null) { SetBrowserFontSizeIfChanged(factionUnlock, 15f); SetBrowserFontStyleIfChanged(factionUnlock, FontStyles.Normal); }
            if (factionCurrent != null) { SetBrowserFontSizeIfChanged(factionCurrent, 15f); SetBrowserFontStyleIfChanged(factionCurrent, FontStyles.Normal); }
            if (factionState != null) { SetBrowserFontSizeIfChanged(factionState, 13f); SetBrowserFontStyleIfChanged(factionState, FontStyles.Normal); }
            // Stable faction table geometry: faction/name at left, then four
            // fixed columns whose headers sit directly above their values.
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            
            if (leftRt != null)
            {
                bool factionHasIcon = line.RowKind == BrowserRowKind.FactionReward && showIcon;
                SetBrowserRectPositionIfChanged(leftRt, factionHasIcon ? 36f : 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, factionHasIcon ? 268f : 294f, leftRt.sizeDelta.y);
            }
            
            if (factionReward != null)
            {
                RectTransform rt = factionReward.rectTransform;
                SetBrowserRectPositionIfChanged(rt, 304f, 0f);
                SetBrowserRectSizeIfChanged(rt, 82f, rt.sizeDelta.y);
                SetBrowserActiveIfChanged(factionReward.gameObject, true);
                SetBrowserTextIfChanged(factionReward, NormalizeModUiText(line.ColumnReward));
            }
            if (factionUnlock != null)
            {
                RectTransform rt = factionUnlock.rectTransform;
                SetBrowserRectPositionIfChanged(rt, 386f, 0f);
                SetBrowserRectSizeIfChanged(rt, 78f, rt.sizeDelta.y);
                SetBrowserActiveIfChanged(factionUnlock.gameObject, true);
                SetBrowserTextIfChanged(factionUnlock, NormalizeModUiText(line.ColumnUnlock));
            }
            if (factionCurrent != null)
            {
                RectTransform rt = factionCurrent.rectTransform;
                SetBrowserRectPositionIfChanged(rt, 464f, 0f);
                SetBrowserRectSizeIfChanged(rt, 104f, rt.sizeDelta.y);
                SetBrowserActiveIfChanged(factionCurrent.gameObject, true);
                SetBrowserTextIfChanged(factionCurrent, NormalizeModUiText(line.ColumnCurrent));
            }
            if (factionState != null)
            {
                RectTransform rt = factionState.rectTransform;
                SetBrowserRectPositionIfChanged(rt, 568f, 0f);
                SetBrowserRectSizeIfChanged(rt, 94f, rt.sizeDelta.y);
                SetBrowserActiveIfChanged(factionState.gameObject, true);
                SetBrowserTextIfChanged(factionState, NormalizeModUiText(line.ColumnState));
            }
            
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            
            if (line.RowKind == BrowserRowKind.FactionRewardHeader)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetBrowserGraphicColorIfChanged(left, new Color(0.35f, 0.58f, 0.52f, 1f));
                Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(factionReward, headerColor);
                SetBrowserGraphicColorIfChanged(factionUnlock, headerColor);
                SetBrowserGraphicColorIfChanged(factionCurrent, headerColor);
                SetBrowserGraphicColorIfChanged(factionState, headerColor);
                SetBrowserFontStyleIfChanged(factionReward, FontStyles.Italic);
                SetBrowserFontStyleIfChanged(factionUnlock, FontStyles.Italic);
                SetBrowserFontStyleIfChanged(factionCurrent, FontStyles.Italic);
                SetBrowserFontStyleIfChanged(factionState, FontStyles.Italic);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                Color valueColor = new Color(0.92f, 0.86f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(factionReward, valueColor);
                SetBrowserGraphicColorIfChanged(factionUnlock, valueColor);
                SetBrowserGraphicColorIfChanged(factionCurrent, valueColor);
                SetBrowserGraphicColorIfChanged(factionState, line.Style == BrowserLineStyle.Accent
                    ? new Color(0.64f, 0.85f, 0.67f, 1f)
                    : valueColor);
            }
        }

        private static void RenderBrowserLootSpecialRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line;
            TMP_Text left = ctx.Left; TMP_Text right = ctx.Right;
            TMP_Text kind = ctx.FactionReward; TMP_Text condition = ctx.FactionUnlock;
            TMP_Text result = ctx.FactionCurrent; TMP_Text unused = ctx.FactionState;
            RectTransform leftRt = ctx.LeftRt; RectTransform statusRt = ctx.StatusRt;
            Image bg = ctx.Background; Image statusIcon = ctx.ChipStatusIcon;
            bool header = line.RowKind == BrowserRowKind.LootSpecialHeader;
            bool rewardPool = string.Equals(
                line.ColumnState, "reward_pool", StringComparison.OrdinalIgnoreCase);
            bool eligibilityStatus = IsBrowserEligibilityMarker(line.ColumnCurrent);

            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            if (unused != null) SetBrowserActiveIfChanged(unused.gameObject, false);

            // LootSpecial rows reuse the same pooled TMP objects as notes, regular Loot and
            // accordion headers. Always overwrite the left cell here; test17 accidentally
            // left the previous pooled-row text in place, which produced player-visible
            // fragments such as "Шанс." / section titles in the source column after scrolling.
            SetBrowserTextIfChanged(left, NormalizeModUiText(ctx.LeftText));

            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, rewardPool ? 184f : 206f, leftRt.sizeDelta.y);
            }
            if (rewardPool)
            {
                ConfigureLootColumn(kind, 198f, 118f, line.ColumnReward, header ? 10.25f : 10.75f);
                ConfigureLootColumn(condition, 320f, 294f, line.ColumnUnlock, header ? 10.25f : 10.75f);
                ConfigureLootColumn(result, 618f, 70f,
                    eligibilityStatus ? string.Empty : line.ColumnCurrent, header ? 10.25f : 10.75f);
            }
            else
            {
                ConfigureLootColumn(kind, 220f, 126f, line.ColumnReward, header ? 10.25f : 10.75f);
                ConfigureLootColumn(condition, 350f, 198f, line.ColumnUnlock, header ? 10.25f : 10.75f);
                ConfigureLootColumn(result, 554f, 134f, line.ColumnCurrent, header ? 10.25f : 10.75f);
            }

            if (!header && rewardPool && eligibilityStatus && statusIcon != null)
            {
                if (statusRt != null)
                {
                    SetBrowserRectPositionIfChanged(statusRt, 645f, 0f);
                    SetBrowserRectSizeIfChanged(statusRt, 16f, 16f);
                }
                bool eligible = string.Equals(
                    line.ColumnCurrent, "eligible", StringComparison.OrdinalIgnoreCase);
                SetBrowserImageSpriteIfChanged(statusIcon,
                    eligible ? _qiiUnlockedMarkerSprite : _qiiLockedMarkerSprite);
                SetBrowserGraphicColorIfChanged(statusIcon, eligible
                    ? new Color(0.46f, 0.92f, 0.54f, 1f)
                    : new Color(0.94f, 0.34f, 0.30f, 1f));
                SetBrowserImageEnabledIfChanged(statusIcon, statusIcon.sprite != null);
            }

            if (left != null)
            {
                SetBrowserFontSizeIfChanged(left, header ? 11.5f : 13f);
                SetBrowserAutoSizingIfChanged(left, false);
                SetBrowserOverflowIfChanged(left, TextOverflowModes.Ellipsis);
            }
            TMP_Text[] cols = new TMP_Text[] { kind, condition, result };
            for (int i = 0; i < cols.Length; i++)
            {
                TMP_Text c = cols[i];
                if (c == null) continue;
                SetBrowserAutoSizingIfChanged(c, false);
                SetBrowserOverflowIfChanged(c, TextOverflowModes.Ellipsis);
            }

            if (header)
            {
                Color headerColor = new Color(0.44f, 0.67f, 0.57f, 1f);
                SetBrowserGraphicColorIfChanged(left, headerColor);
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetLootColumnHeaderStyle(kind, headerColor);
                SetLootColumnHeaderStyle(condition, headerColor);
                SetLootColumnHeaderStyle(result, headerColor);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                SetBrowserGraphicColorIfChanged(kind, new Color(0.52f, 0.74f, 0.63f, 1f));
                SetBrowserGraphicColorIfChanged(condition, new Color(0.92f, 0.86f, 0.52f, 1f));
                SetBrowserGraphicColorIfChanged(result, new Color(0.76f, 0.88f, 0.68f, 1f));
            }
        }

        private static void RenderBrowserLootRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock;
            TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState; Image bg = ctx.Background; Image chipStatusImage = ctx.ChipStatusIcon;
            RectTransform leftRt = ctx.LeftRt; RectTransform itemRt = ctx.ItemRt; RectTransform statusRt = ctx.StatusRt; bool showIcon = ctx.ShowIcon; string leftText = ctx.LeftText;
            // Loot uses the already pooled faction-table text objects instead of
            // creating new UI objects. The dedicated geometry makes source,
            // context, chance, rolls/Tech and status readable at a glance.
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            
            bool hasFourthColumn = !string.IsNullOrEmpty(line.ColumnState);
            bool scavengerTimingRow = line.Style == BrowserLineStyle.ScavengerUnknown ||
                line.Style == BrowserLineStyle.ScavengerReachable ||
                line.Style == BrowserLineStyle.ScavengerExpiresBeforeArrival;
            bool showContainerIcon =
                line.RowKind == BrowserRowKind.LootRow &&
                !string.IsNullOrEmpty(line.ContainerIconId) &&
                showIcon && itemRt != null;
            if (leftRt != null)
            {
                // Container rows now follow the same icon-before-name grammar as
                // item/faction rows throughout QII.
                SetBrowserRectPositionIfChanged(leftRt, showContainerIcon ? 36f : 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt,
                    showContainerIcon ? 260f : (hasFourthColumn ? 286f : 304f),
                    leftRt.sizeDelta.y);
            }
            if (showContainerIcon && itemRt != null)
            {
                SetBrowserRectPositionIfChanged(itemRt, 8f, 0f);
                SetBrowserRectSizeIfChanged(itemRt, 22f, 22f);
            }
            
            if (hasFourthColumn)
            {
                if (scavengerTimingRow)
                {
                    if (leftRt != null) SetBrowserRectSizeIfChanged(leftRt, 264f, leftRt.sizeDelta.y);
                    ConfigureLootColumn(factionReward, 274f, 140f, line.ColumnReward, 12.5f);
                    ConfigureLootColumn(factionUnlock, 414f, 74f, line.ColumnUnlock, 12f);
                    ConfigureLootColumn(factionCurrent, 488f, 88f, line.ColumnCurrent, 12.5f);
                    ConfigureLootColumn(factionState, 576f, 112f, line.ColumnState, 12.5f);
                }
                else
                {
                    ConfigureLootColumn(factionReward, 296f, 150f, line.ColumnReward, 12.5f);
                    ConfigureLootColumn(factionUnlock, 446f, 86f, line.ColumnUnlock, 12.5f);
                    ConfigureLootColumn(factionCurrent, 532f, 88f, line.ColumnCurrent, 12.5f);
                }
            
                bool eligibilityStatus = IsBrowserEligibilityMarker(line.ColumnState);
                if (eligibilityStatus && line.RowKind == BrowserRowKind.LootRow && chipStatusImage != null)
                {
                    ConfigureLootColumn(factionState, 620f, 70f, string.Empty, 12f);
                    if (statusRt != null)
                    {
                        SetBrowserRectPositionIfChanged(statusRt, 646f, 0f);
                        SetBrowserRectSizeIfChanged(statusRt, 16f, 16f);
                    }
                    bool eligible = string.Equals(line.ColumnState, "eligible", StringComparison.OrdinalIgnoreCase);
                    SetBrowserImageSpriteIfChanged(chipStatusImage, eligible ? _qiiUnlockedMarkerSprite : _qiiLockedMarkerSprite);
                    SetBrowserGraphicColorIfChanged(chipStatusImage, eligible
                        ? new Color(0.46f, 0.92f, 0.54f, 1f)
                        : new Color(0.94f, 0.34f, 0.30f, 1f));
                    SetBrowserImageEnabledIfChanged(chipStatusImage, chipStatusImage.sprite != null);
                }
                else if (!scavengerTimingRow)
                {
                    ConfigureLootColumn(factionState, 620f, 70f, line.ColumnState, 12f);
                }
            }
            else
            {
                ConfigureLootColumn(factionReward, 314f, 184f, line.ColumnReward, 12.5f);
                ConfigureLootColumn(factionUnlock, 498f, 92f, line.ColumnUnlock, 12.5f);
            
                bool eligibilityStatus = IsBrowserEligibilityMarker(line.ColumnCurrent);
                if (eligibilityStatus && line.RowKind == BrowserRowKind.LootRow && chipStatusImage != null)
                {
                    ConfigureLootColumn(factionCurrent, 590f, 96f, string.Empty, 12.5f);
                    if (statusRt != null)
                    {
                        SetBrowserRectPositionIfChanged(statusRt, 630f, 0f);
                        SetBrowserRectSizeIfChanged(statusRt, 16f, 16f);
                    }
                    bool eligible = string.Equals(line.ColumnCurrent, "eligible", StringComparison.OrdinalIgnoreCase);
                    SetBrowserImageSpriteIfChanged(chipStatusImage, eligible ? _qiiUnlockedMarkerSprite : _qiiLockedMarkerSprite);
                    SetBrowserGraphicColorIfChanged(chipStatusImage, eligible
                        ? new Color(0.46f, 0.92f, 0.54f, 1f)
                        : new Color(0.94f, 0.34f, 0.30f, 1f));
                    SetBrowserImageEnabledIfChanged(chipStatusImage, chipStatusImage.sprite != null);
                }
                else
                {
                    ConfigureLootColumn(factionCurrent, 590f, 96f, line.ColumnCurrent, 12.5f);
                }
                ConfigureLootColumn(factionState, 662f, 0f, string.Empty, 12f);
            }
            
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            SetBrowserFontSizeIfChanged(left, line.RowKind == BrowserRowKind.LootHeader ? 12.5f : 14.5f);
            
            if (line.RowKind == BrowserRowKind.LootHeader)
            {
                Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(left, headerColor);
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetLootColumnHeaderStyle(factionReward, headerColor);
                SetLootColumnHeaderStyle(factionUnlock, headerColor);
                SetLootColumnHeaderStyle(factionCurrent, headerColor);
                SetLootColumnHeaderStyle(factionState, headerColor);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                if (factionReward != null)
                    SetBrowserGraphicColorIfChanged(factionReward, new Color(0.52f, 0.74f, 0.63f, 1f));
                if (factionUnlock != null)
                    SetBrowserGraphicColorIfChanged(factionUnlock, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (factionCurrent != null)
                    SetBrowserGraphicColorIfChanged(factionCurrent, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (factionState != null)
                    SetBrowserGraphicColorIfChanged(factionState, line.Style == BrowserLineStyle.ScavengerExpiresBeforeArrival
                        ? new Color(0.95f, 0.62f, 0.34f, 1f)
                        : line.Style == BrowserLineStyle.ScavengerReachable
                            ? new Color(0.62f, 0.82f, 0.66f, 1f)
                            : line.Style == BrowserLineStyle.ScavengerUnknown
                                ? new Color(0.50f, 0.58f, 0.54f, 1f)
                                : new Color(0.62f, 0.82f, 0.66f, 1f));
            }
        }

        private static void RenderBrowserLootSixColumnRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock;
            TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState; Image bg = ctx.Background; RectTransform leftRt = ctx.LeftRt; RectTransform itemRt = ctx.ItemRt; string leftText = ctx.LeftText;
            // Six-column enemy Loot table. Reuse the pooled Right text as the sixth column so no extra runtime UI objects are allocated.
            if (right != null)
            {
                SetBrowserActiveIfChanged(right.gameObject, true);
                SetBrowserAlignmentIfChanged(right, TextAlignmentOptions.Center);
            }
            if (itemRt != null)
            {
                SetBrowserRectPositionIfChanged(itemRt, 8f, 0f);
                SetBrowserRectSizeIfChanged(itemRt, 20f, 20f);
            }
            if (leftRt != null)
            {
                // Dedicated faction-icon cell: 8..30 px. Enemy text always starts
                // after that cell, so faction emblems can never overlap the name.
                SetBrowserRectPositionIfChanged(leftRt, 38f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, 172f, leftRt.sizeDelta.y);
            }
            
            // Give probability ranges substantially more room. Values such as
            // 0.548%-0.912% must remain readable in both EN and RU layouts.
            ConfigureLootColumn(factionReward, 210f, 112f, line.ColumnReward, 12f);
            ConfigureLootColumn(factionUnlock, 322f, 112f, line.ColumnUnlock, 11.5f);
            ConfigureLootColumn(factionCurrent, 434f, 54f, line.ColumnCurrent, 12f);
            ConfigureLootColumn(factionState, 488f, 96f, line.ColumnState, 11.5f);
            ConfigureLootColumn(right, 584f, 104f, line.Right, 11.5f);
            
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            SetBrowserFontSizeIfChanged(left, line.RowKind == BrowserRowKind.LootHeaderSixColumns ? 12f : 14f);
            
            if (line.RowKind == BrowserRowKind.LootHeaderSixColumns)
            {
                Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(left, headerColor);
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetLootColumnHeaderStyle(factionReward, headerColor);
                SetLootColumnHeaderStyle(factionUnlock, headerColor);
                SetLootColumnHeaderStyle(factionCurrent, headerColor);
                SetLootColumnHeaderStyle(factionState, headerColor);
                SetLootColumnHeaderStyle(right, headerColor);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                if (factionReward != null)
                    SetBrowserGraphicColorIfChanged(factionReward, new Color(0.52f, 0.74f, 0.63f, 1f));
                if (factionUnlock != null)
                    SetBrowserGraphicColorIfChanged(factionUnlock, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (factionCurrent != null)
                    SetBrowserGraphicColorIfChanged(factionCurrent, new Color(0.76f, 0.88f, 0.68f, 1f));
                if (factionState != null)
                    SetBrowserGraphicColorIfChanged(factionState, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (right != null)
                    SetBrowserGraphicColorIfChanged(right, new Color(0.62f, 0.82f, 0.66f, 1f));
            }
        }

        private static void RenderBrowserMagnumResearchRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock;
            TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState; RectTransform leftRt = ctx.LeftRt; string leftText = ctx.LeftText;
            // Magnum research route | quantity | full state. This avoids the old
            // right-edge truncation ("compl") while keeping long routes readable.
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, 478f, leftRt.sizeDelta.y);
            }
            ConfigureLootColumn(factionReward, 488f, 54f, line.ColumnReward, 14f);
            ConfigureLootColumn(factionUnlock, 542f, 146f, line.ColumnUnlock, 13.5f);
            ConfigureLootColumn(factionCurrent, 688f, 0f, string.Empty, 12f);
            ConfigureLootColumn(factionState, 688f, 0f, string.Empty, 12f);
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            SetBrowserFontSizeIfChanged(left, 16f);
            if (factionReward != null) SetBrowserGraphicColorIfChanged(factionReward, new Color(0.92f, 0.86f, 0.52f, 1f));
            if (factionUnlock != null) SetBrowserGraphicColorIfChanged(factionUnlock, new Color(0.76f, 0.88f, 0.68f, 1f));
        }


        private static void RenderBrowserBaronLootRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; TMP_Text factionReward = ctx.FactionReward; TMP_Text factionUnlock = ctx.FactionUnlock;
            TMP_Text factionCurrent = ctx.FactionCurrent; TMP_Text factionState = ctx.FactionState; Image bg = ctx.Background; RectTransform leftRt = ctx.LeftRt; string leftText = ctx.LeftText;
            // Compact Baron table shared by Overview and Loot.
            // Geometry intentionally fills the complete 688 px content width.
            ApplyBrowserBaronColumns(
                leftRt, right, factionReward, factionUnlock,
                line.ColumnReward, line.ColumnUnlock,
                line.RowKind == BrowserRowKind.BaronLootHeader);
            ConfigureLootColumn(factionCurrent, 688f, 0f, string.Empty, 12f);
            ConfigureLootColumn(factionState, 688f, 0f, string.Empty, 12f);
            
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            SetBrowserFontSizeIfChanged(left, line.RowKind == BrowserRowKind.BaronLootHeader ? 12.5f : 16f);
            
            if (line.RowKind == BrowserRowKind.BaronLootHeader)
            {
                Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(left, headerColor);
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetLootColumnHeaderStyle(factionReward, headerColor);
                SetLootColumnHeaderStyle(factionUnlock, headerColor);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                SetBrowserGraphicColorIfChanged(left, new Color(0.46f, 0.72f, 0.61f, 1f));
                if (factionReward != null) SetBrowserGraphicColorIfChanged(factionReward, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (factionUnlock != null) SetBrowserGraphicColorIfChanged(factionUnlock, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.018f, 0.050f, 0.043f, 0.45f));
            }
        }

        private static void RenderBrowserLootSectionHeader(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; string leftText = ctx.LeftText;
            // Accordion headers are actionable, but unlike navigation rows they
            // use their own disclosure glyph instead of the retired text-only chrome.
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
            SetBrowserTextIfChanged(right, NormalizeModUiText(line.Right ?? string.Empty));
            SetBrowserAutoSizingIfChanged(left, true);
            SetBrowserFontSizeMinIfChanged(left, 13.5f);
            SetBrowserFontSizeMaxIfChanged(left, 16f);
            SetBrowserFontSizeIfChanged(left, 16f);
            SetBrowserAutoSizingIfChanged(right, true);
            SetBrowserFontSizeMinIfChanged(right, 12.5f);
            SetBrowserFontSizeMaxIfChanged(right, 14.5f);
            SetBrowserFontSizeIfChanged(right, 14.5f);
        }

        private static void RenderBrowserFullWidthTextRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; RectTransform leftRt = ctx.LeftRt; string leftText = ctx.LeftText;
            // Full-width informational rows avoid wasting the unused right column.
            // Full-width informational rows avoid wasting the unused right
            // column. FullSection is used by long localized section titles;
            // FullNote lines are pre-wrapped to preserve the fixed row pool.
            if (line.RowKind == BrowserRowKind.FullSection)
                ApplyBrowserFullWidthRow(left, right, leftRt, 17f, 12.5f, true);
            else
                ApplyBrowserFullWidthRow(left, right, leftRt, 11.5f, 10.5f, true);
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
        }

        private static void RenderBrowserChipNoteRow(ref BrowserRowRenderContext ctx)
        {
            TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; RectTransform leftRt = ctx.LeftRt; string leftText = ctx.LeftText;
            // Chip unlock chance explanation uses the full content width.
            ApplyBrowserFullWidthRow(left, right, leftRt, 13f, 12f, true);
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
        }

        private static void RenderBrowserDefaultRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; string leftText = ctx.LeftText;
            if (TryRenderBrowserItemLink(ref ctx)) return;
            SetBrowserTextIfChanged(right, NormalizeModUiText(line.Right ?? string.Empty));
            SetBrowserTextIfChanged(left, NormalizeModUiText(leftText));
        }

        private static void ApplyBrowserRowFinalStyle(ref BrowserRowRenderContext ctx)
        {
            int i = ctx.Slot; BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right; Image bg = ctx.Background;
            bool actionable = ctx.Actionable; Outline rowOutline = ctx.RowOutline;
            if (line.Style == BrowserLineStyle.Section)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Bold);
                SetBrowserGraphicColorIfChanged(left, new Color(0.74f, 0.86f, 0.62f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.48f, 0.72f, 0.62f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.032f, 0.092f, 0.073f, 0.95f));
            }
            else if (line.Style == BrowserLineStyle.Note)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetBrowserGraphicColorIfChanged(left, new Color(0.35f, 0.58f, 0.52f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.35f, 0.58f, 0.52f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else if (line.Style == BrowserLineStyle.Accent)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Bold);
                SetBrowserGraphicColorIfChanged(left, new Color(0.64f, 0.85f, 0.67f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.046f, 0.106f, 0.073f, 0.80f));
            }
            else
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Normal);
                SetBrowserGraphicColorIfChanged(left, new Color(0.46f, 0.72f, 0.61f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.86f, 0.85f, 0.61f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, i % 2 == 0
                    ? new Color(0.018f, 0.050f, 0.043f, 0.45f)
                    : new Color(0.010f, 0.034f, 0.030f, 0.18f));
            }
            
            if (actionable)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Bold);
                SetBrowserGraphicColorIfChanged(left, new Color(0.68f, 0.90f, 0.68f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.95f, 0.88f, 0.52f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.030f, 0.090f, 0.064f, 0.88f));
            }
            if (line.RowKind == BrowserRowKind.LootSectionHeader)
            {
                SetBrowserFontStyleIfChanged(left, FontStyles.Bold);
                SetBrowserGraphicColorIfChanged(left, new Color(0.76f, 0.94f, 0.72f, 1f));
                SetBrowserGraphicColorIfChanged(right, new Color(0.96f, 0.88f, 0.54f, 1f));
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.040f, 0.125f, 0.082f, 0.96f));
            }
            
            if (actionable && !string.IsNullOrEmpty(line.FactionId))
            {
                if (line.FactionRelation == BrowserFactionRelation.Friendly)
                {
                    SetBrowserGraphicColorIfChanged(left, new Color(0.43f, 0.92f, 0.55f, 1f));
                    if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.025f, 0.095f, 0.048f, 0.90f));
                    if (rowOutline != null)
                        SetBrowserOutlineColorIfChanged(rowOutline, new Color(0.30f, 0.82f, 0.46f, 0.92f));
                }
                else if (line.FactionRelation == BrowserFactionRelation.Hostile)
                {
                    SetBrowserGraphicColorIfChanged(left, new Color(0.96f, 0.42f, 0.38f, 1f));
                    if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.105f, 0.028f, 0.030f, 0.90f));
                    if (rowOutline != null)
                        SetBrowserOutlineColorIfChanged(rowOutline, new Color(0.90f, 0.30f, 0.28f, 0.94f));
                }
                else if (line.FactionRelation == BrowserFactionRelation.Neutral)
                {
                    SetBrowserGraphicColorIfChanged(left, new Color(0.72f, 0.78f, 0.69f, 1f));
                    if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.046f, 0.060f, 0.052f, 0.88f));
                    if (rowOutline != null)
                        SetBrowserOutlineColorIfChanged(rowOutline, new Color(0.47f, 0.58f, 0.52f, 0.84f));
                }
                else
                {
                    // Unknown relation is not neutral. Use a subdued amber treatment
                    // instead of claiming a relationship state that was not resolved.
                    SetBrowserGraphicColorIfChanged(left, new Color(0.82f, 0.74f, 0.50f, 1f));
                    if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.075f, 0.061f, 0.036f, 0.88f));
                    if (rowOutline != null)
                        SetBrowserOutlineColorIfChanged(rowOutline, new Color(0.67f, 0.56f, 0.34f, 0.86f));
                }
            }
            ApplyBrowserItemLinkFinalStyle(ref ctx);
        }

        private static void UpdateBrowserRowScrollChrome(int total)
        {
            if (_browserScrollText != null)
            {
                int first = total <= 0 ? 0 : BrowserNavigation.ScrollOffset + 1;
                int last = Math.Min(total, BrowserNavigation.ScrollOffset + BrowserVisibleRows);
                string scrollLabel = NormalizeModUiText(
                    Ui("ui.rows_visible") + " " +
                    first.ToString(CultureInfo.InvariantCulture) + "-" +
                    last.ToString(CultureInfo.InvariantCulture) + " / " +
                    total.ToString(CultureInfo.InvariantCulture));
                if (!string.Equals(_browserScrollText.text, scrollLabel, StringComparison.Ordinal))
                    _browserScrollText.text = scrollLabel;
            }
            SyncBrowserContinuousScrollbar(_browserScrollScrollbar, total, BrowserVisibleRows, BrowserNavigation.ScrollOffset);
        }
    }
}
