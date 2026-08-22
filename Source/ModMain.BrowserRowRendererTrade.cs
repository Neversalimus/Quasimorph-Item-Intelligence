using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void RenderBrowserTradeRow(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line; TMP_Text left = ctx.Left; TMP_Text right = ctx.Right;
            TMP_Text reward = ctx.FactionReward; TMP_Text unlock = ctx.FactionUnlock;
            TMP_Text current = ctx.FactionCurrent; TMP_Text state = ctx.FactionState;
            Image bg = ctx.Background; RectTransform leftRt = ctx.LeftRt; bool showIcon = ctx.ShowIcon;

            if (line.RowKind == BrowserRowKind.TradeStationCard)
            {
                RenderBrowserTradeCard103(line, left, right, reward, unlock, current, state, leftRt, showIcon);
                return;
            }

            bool sixColumns = !string.IsNullOrEmpty(line.Right);
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, showIcon ? 36f : 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, sixColumns ? (showIcon ? 250f : 276f) : (showIcon ? 324f : 350f), leftRt.sizeDelta.y);
            }
            if (sixColumns)
            {
                ConfigureLootColumn(reward, 286f, 55f, line.ColumnReward, 12.5f);
                ConfigureLootColumn(unlock, 341f, 82f, line.ColumnUnlock, 12.5f);
                ConfigureLootColumn(current, 423f, 47f, line.ColumnCurrent, 12f);
                ConfigureLootColumn(state, 470f, 83f, line.ColumnState, 12f);
                ConfigureLootColumn(right, 553f, 135f, line.Right, 12f);
                if (right != null) SetBrowserAlignmentIfChanged(right, TextAlignmentOptions.MidlineRight);
            }
            else
            {
                if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
                ConfigureLootColumn(reward, 360f, 65f, line.ColumnReward, 13.5f);
                ConfigureLootColumn(unlock, 425f, 58f, line.ColumnUnlock, 13.5f);
                ConfigureLootColumn(current, 483f, 80f, line.ColumnCurrent, 12.5f);
                ConfigureLootColumn(state, 563f, 125f, line.ColumnState, 13f);
            }
            SetBrowserTextIfChanged(left, NormalizeModUiText(ctx.LeftText));
            SetBrowserFontSizeIfChanged(left, line.RowKind == BrowserRowKind.TradeHeader ? 12.5f : (sixColumns ? 15f : 16f));

            if (line.RowKind == BrowserRowKind.TradeHeader)
            {
                Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                SetBrowserGraphicColorIfChanged(left, headerColor); SetBrowserFontStyleIfChanged(left, FontStyles.Italic);
                SetLootColumnHeaderStyle(reward, headerColor); SetLootColumnHeaderStyle(unlock, headerColor);
                SetLootColumnHeaderStyle(current, headerColor); SetLootColumnHeaderStyle(state, headerColor);
                if (sixColumns) SetLootColumnHeaderStyle(right, headerColor);
                if (bg != null) SetBrowserGraphicColorIfChanged(bg, new Color(0.010f, 0.030f, 0.027f, 0.30f));
            }
            else
            {
                if (reward != null) SetBrowserGraphicColorIfChanged(reward, new Color(0.92f, 0.86f, 0.52f, 1f));
                if (unlock != null) SetBrowserGraphicColorIfChanged(unlock, new Color(0.76f, 0.88f, 0.68f, 1f));
                TMP_Text missionColumn = sixColumns ? state : current;
                TMP_Text stockOrTravelColumn = sixColumns ? current : state;
                if (missionColumn != null) SetBrowserGraphicColorIfChanged(missionColumn, GetTradeMissionColor(line.TradeArrivalState));
                if (stockOrTravelColumn != null) SetBrowserGraphicColorIfChanged(stockOrTravelColumn, new Color(0.70f, 0.78f, 0.73f, 1f));
                if (sixColumns && right != null) SetBrowserGraphicColorIfChanged(right, new Color(0.70f, 0.78f, 0.73f, 1f));
            }
        }

        private static void RenderBrowserTradeCard103(
            BrowserLine line, TMP_Text left, TMP_Text right, TMP_Text middle,
            TMP_Text unlock, TMP_Text current, TMP_Text state, RectTransform leftRt, bool showIcon)
        {
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, showIcon ? 36f : 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, showIcon ? 390f : 416f, leftRt.sizeDelta.y);
            }
            if (unlock != null) SetBrowserActiveIfChanged(unlock.gameObject, false);
            if (current != null) SetBrowserActiveIfChanged(current.gameObject, false);
            if (state != null) SetBrowserActiveIfChanged(state.gameObject, false);

            ConfigureLootColumn(middle, 426f, 112f, line.ColumnReward, 11.75f);
            ConfigureLootColumn(right, 538f, 150f, line.Right, 11.5f);
            if (right != null) SetBrowserAlignmentIfChanged(right, TextAlignmentOptions.MidlineRight);

            SetBrowserTextIfChanged(left, NormalizeModUiText(ctxSafe(line.Left)));
            SetBrowserFontSizeIfChanged(left, 12.5f);
            SetBrowserAutoSizingIfChanged(left, true);
            SetBrowserFontSizeMinIfChanged(left, 10.75f);
            SetBrowserFontSizeMaxIfChanged(left, 12.5f);
            SetBrowserWordWrappingIfChanged(left, false);
            SetBrowserOverflowIfChanged(left, TextOverflowModes.Ellipsis);

            if (middle != null) SetBrowserGraphicColorIfChanged(middle, new Color(0.88f, 0.82f, 0.50f, 1f));
            if (right != null) SetBrowserGraphicColorIfChanged(right, GetTradeMissionColor(line.TradeArrivalState));
        }

        private static string ctxSafe(string value) { return value ?? string.Empty; }

        private static Color GetTradeMissionColor(BrowserTradeArrivalState state)
        {
            return state == BrowserTradeArrivalState.MissionActiveOnArrival ? new Color(0.95f, 0.62f, 0.34f, 1f)
                : state == BrowserTradeArrivalState.MissionExpiresBeforeArrival ? new Color(0.56f, 0.72f, 0.58f, 1f)
                : state == BrowserTradeArrivalState.ComparisonUnavailable ? new Color(0.92f, 0.76f, 0.42f, 1f)
                : new Color(0.70f, 0.78f, 0.73f, 1f);
        }
    }
}
