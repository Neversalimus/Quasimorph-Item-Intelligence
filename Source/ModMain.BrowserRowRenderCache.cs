using System;

namespace ItemIntelligence
{
    /// <summary>
    /// Safe reuse cache for already-rendered text-only Overview rows.
    /// It deliberately excludes rows whose visuals can change asynchronously (item/mode/container
    /// icons, chips, factions and specialized data tables). This keeps the optimization fail-closed:
    /// a row is reused only when every presentation input still matches exactly.
    /// </summary>
    public static partial class ModMain
    {
        private sealed class BrowserRowRenderStamp
        {
            public string Language;
            public int Tab;
            public string Left;
            public string Right;
            public BrowserLineStyle Style;
            public BrowserLeftContentKind LeftContentKind;
            public BrowserAction Action;
            public bool ShowRecipeChipContext;
            public string ChipItemId;
            public BrowserChipStatus ChipStatus;
            public string FactionId;
            public BrowserFactionRelation FactionRelation;
            public string ContainerIconId;
            public int RowKind;
            public string Reward;
            public string Unlock;
            public string Current;
            public string State;
            public BrowserTradeArrivalState TradeArrivalState;

            public bool Matches(BrowserLine line, string language)
            {
                return line != null &&
                    string.Equals(Language, language ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    Tab == BrowserNavigation.Tab &&
                    string.Equals(Left, line.Left, StringComparison.Ordinal) &&
                    string.Equals(Right, line.Right, StringComparison.Ordinal) &&
                    Style == line.Style && LeftContentKind == line.LeftContentKind &&
                    Action.Equals(line.Action) &&
                    ShowRecipeChipContext == line.ShowRecipeChipContext &&
                    string.Equals(ChipItemId, line.ChipItemId, StringComparison.Ordinal) &&
                    ChipStatus == line.ChipStatus &&
                    string.Equals(FactionId, line.FactionId, StringComparison.Ordinal) &&
                    FactionRelation == line.FactionRelation &&
                    string.Equals(ContainerIconId, line.ContainerIconId, StringComparison.Ordinal) &&
                    RowKind == (int)line.RowKind &&
                    string.Equals(Reward, line.ColumnReward, StringComparison.Ordinal) &&
                    string.Equals(Unlock, line.ColumnUnlock, StringComparison.Ordinal) &&
                    string.Equals(Current, line.ColumnCurrent, StringComparison.Ordinal) &&
                    string.Equals(State, line.ColumnState, StringComparison.Ordinal) &&
                    TradeArrivalState == line.TradeArrivalState;
            }
        }

        private static readonly BrowserRowRenderStamp[] BrowserRowRenderStamps =
            new BrowserRowRenderStamp[BrowserVisibleRows];
        private static int _browserRowReuseHitsThisPass;
        private static int _browserRowReuseLogCount;

        private static void BeginBrowserRowRenderReusePass()
        {
            _browserRowReuseHitsThisPass = 0;
        }

        private static bool IsBrowserRowRenderReuseSafe(BrowserLine line)
        {
            if (line == null || BrowserNavigation.Tab != (int)BrowserTabId.Overview) return false;
            if (line.LeftContentKind != BrowserLeftContentKind.Text || line.ShowRecipeChipContext || line.ChipStatus != BrowserChipStatus.None) return false;
            if (!line.Action.IsNone) return false;
            if (!string.IsNullOrEmpty(line.ChipItemId) || !string.IsNullOrEmpty(line.FactionId)) return false;
            if (!string.IsNullOrEmpty(line.ContainerIconId)) return false;
            return line.RowKind == BrowserRowKind.Default ||
                   line.RowKind == BrowserRowKind.FullNote ||
                   line.RowKind == BrowserRowKind.FullSection ||
                   line.RowKind == BrowserRowKind.OverviewCombatHeader ||
                   line.RowKind == BrowserRowKind.BaronLootHeader ||
                   line.RowKind == BrowserRowKind.BaronLootRow;
        }

        private static bool CanReuseBrowserRowRender(int visibleRow, BrowserLine line, string language)
        {
            if (visibleRow < 0 || visibleRow >= BrowserRowRenderStamps.Length) return false;
            if (!IsBrowserRowRenderReuseSafe(line)) return false;
            BrowserRowRenderStamp stamp = BrowserRowRenderStamps[visibleRow];
            if (stamp == null || !stamp.Matches(line, language)) return false;
            if (BrowserRowRoots[visibleRow] == null || BrowserRowLeft[visibleRow] == null || BrowserRowRight[visibleRow] == null)
                return false;
            _browserRowReuseHitsThisPass++;
            return true;
        }

        private static void RestoreCachedBrowserRowBindings(int visibleRow, BrowserLine line)
        {
            if (visibleRow < 0 || visibleRow >= BrowserVisibleRows || line == null) return;
            if (BrowserRowRoots[visibleRow] != null) SetBrowserActiveIfChanged(BrowserRowRoots[visibleRow], true);
            bool actionable = !line.Action.IsNone;
            SetBrowserInteractableIfChanged(BrowserRowButtons[visibleRow], actionable);
            SetBrowserRaycastTargetIfChanged(BrowserRowLeft[visibleRow], false);
        }

        private static void CaptureBrowserRowRenderStamp(int visibleRow, BrowserLine line, string language)
        {
            if (visibleRow < 0 || visibleRow >= BrowserRowRenderStamps.Length || line == null) return;
            BrowserRowRenderStamp stamp = BrowserRowRenderStamps[visibleRow] ?? new BrowserRowRenderStamp();
            stamp.Language = language ?? string.Empty;
            stamp.Tab = BrowserNavigation.Tab;
            stamp.Left = line.Left;
            stamp.Right = line.Right;
            stamp.Style = line.Style;
            stamp.LeftContentKind = line.LeftContentKind;
            stamp.Action = line.Action;
            stamp.ShowRecipeChipContext = line.ShowRecipeChipContext;
            stamp.ChipItemId = line.ChipItemId;
            stamp.ChipStatus = line.ChipStatus;
            stamp.FactionId = line.FactionId;
            stamp.FactionRelation = line.FactionRelation;
            stamp.ContainerIconId = line.ContainerIconId;
            stamp.RowKind = (int)line.RowKind;
            stamp.Reward = line.ColumnReward;
            stamp.Unlock = line.ColumnUnlock;
            stamp.Current = line.ColumnCurrent;
            stamp.State = line.ColumnState;
            stamp.TradeArrivalState = line.TradeArrivalState;
            BrowserRowRenderStamps[visibleRow] = stamp;
        }

        private static void InvalidateBrowserRowRenderCache()
        {
            for (int i = 0; i < BrowserRowRenderStamps.Length; i++) BrowserRowRenderStamps[i] = null;
        }

        private static void EndBrowserRowRenderReusePass()
        {
            if (!ModderMode || _browserRowReuseHitsThisPass <= 0 || _browserRowReuseLogCount >= 12) return;
            _browserRowReuseLogCount++;
            UnityEngine.Debug.Log("[ItemIntelligence][RenderReuse] overviewRows=" +
                _browserRowReuseHitsThisPass.ToString() + "/" + BrowserVisibleRows.ToString() + ".");
        }
    }
}
