using System;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private enum BrowserCatalogScope
        {
            All = 0,
            Favorites = 1,
            Recent = 2,
            Count = 3
        }

        private enum BrowserCatalogDataFilter
        {
            Any = 0,
            Recipes = 1,
            Sources = 2,
            Consumers = 3,
            Magnum = 4,
            Factions = 5,
            Ammo = 6,
            Disassembly = 7,
            Count = 8
        }

        private enum BrowserCatalogSortMode
        {
            Name = 0,
            Tech = 1,
            ItemId = 2,
            Count = 3
        }

        private enum BrowserTabId
        {
            Overview = 0,
            Magnum = 1,
            Recipes = 2,
            Trade = 3,
            Ammo = 4,
            Factions = 5,
            Loot = 6,
            Count = 7
        }

        private sealed class BrowserItemNavigationState
        {
            public readonly string ItemId;
            public readonly int Tab;
            public readonly int Page;

            public BrowserItemNavigationState(string itemId, int tab, int page)
            {
                ItemId = itemId ?? string.Empty;
                Tab = tab;
                Page = page;
            }
        }

        private sealed class BrowserSearchMatch
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly int Score;

            public BrowserSearchMatch(string itemId, string displayName, int score)
            {
                ItemId = itemId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Score = score;
            }
        }

        private enum BrowserRowKind
        {
            Default = 0,
            FactionRewardHeader = 1,
            FactionReward = 2,
            LootHeader = 3,
            LootRow = 4,
            FullNote = 5,
            LootHeaderSixColumns = 6,
            LootRowSixColumns = 7,
            MagnumResearch = 8,
            TradeHeader = 9,
            TradeStation = 10,
            ChipUnlock = 11,
            ChipNote = 12,
            FullSection = 13
        }

        private sealed class BrowserLine
        {
            public readonly string Left;
            public readonly string Right;
            public readonly int Style;
            public readonly int LeftMode;
            public readonly string ActionSpaceObjectId;
            public readonly bool ShowRecipeChipContext;
            public readonly string ChipItemId;
            public readonly int ChipStatus;
            public readonly string FactionId;
            public readonly int FactionRelation;
            public readonly BrowserRowKind RowKind;
            public readonly string ColumnReward;
            public readonly string ColumnUnlock;
            public readonly string ColumnCurrent;
            public readonly string ColumnState;
            public readonly string ContainerIconId;
            public readonly int MetaState;

            private BrowserLine(string left, string right, int style, int leftMode, string actionSpaceObjectId,
                bool showRecipeChipContext, string chipItemId, int chipStatus,
                string factionId = "", int factionRelation = 0,
                BrowserRowKind rowKind = BrowserRowKind.Default, string columnReward = "", string columnUnlock = "",
                string columnCurrent = "", string columnState = "", string containerIconId = "", int metaState = 0)
            {
                Left = left ?? string.Empty;
                Right = right ?? string.Empty;
                Style = style;
                LeftMode = leftMode;
                ActionSpaceObjectId = actionSpaceObjectId ?? string.Empty;
                ShowRecipeChipContext = showRecipeChipContext;
                ChipItemId = chipItemId ?? string.Empty;
                ChipStatus = chipStatus;
                FactionId = factionId ?? string.Empty;
                FactionRelation = factionRelation;
                RowKind = rowKind;
                ColumnReward = columnReward ?? string.Empty;
                ColumnUnlock = columnUnlock ?? string.Empty;
                ColumnCurrent = columnCurrent ?? string.Empty;
                ColumnState = columnState ?? string.Empty;
                ContainerIconId = containerIconId ?? string.Empty;
                MetaState = metaState;
            }

            public static BrowserLine Normal(string left, string right)
            {
                return new BrowserLine(left, right, 0, 0, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine Section(string left)
            {
                return new BrowserLine(left, string.Empty, 1, 0, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine Note(string left)
            {
                return new BrowserLine(left, string.Empty, 2, 0, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine ChipNote(string left)
            {
                // BrowserRowKind.ChipNote: a full-width compact informational row used by the
                // chip unlock lottery explanation. Keeping it on one pooled row avoids
                // truncation without increasing the browser page count.
                return new BrowserLine(left, string.Empty, 2, 0, string.Empty, false, string.Empty, 0,
                    string.Empty, 0, BrowserRowKind.ChipNote);
            }

            public static BrowserLine Accent(string left, string right)
            {
                return new BrowserLine(left, right, 3, 0, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine Item(string itemId, string right)
            {
                return new BrowserLine(itemId, right, 0, 1, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine ItemAction(string itemId, string right)
            {
                return new BrowserLine(itemId, right, 0, 1, BrowserItemActionPrefix + itemId, false, string.Empty, 0);
            }

            public static BrowserLine ChipUnlockAction(string itemId, string right, int unlockStatus)
            {
                // BrowserRowKind.ChipUnlock: clickable item row with the existing QII learned/locked
                // marker, but without a duplicate chip icon.
                return new BrowserLine(itemId, right, 0, 1, BrowserItemActionPrefix + itemId,
                    false, string.Empty, unlockStatus, string.Empty, 0, BrowserRowKind.ChipUnlock);
            }

            public static BrowserLine WeaponMode(string label, string modeKey, string right)
            {
                return new BrowserLine(label, right, 0, 3, string.Empty, false, string.Empty, 0,
                    string.Empty, 0, BrowserRowKind.Default, string.Empty, string.Empty, string.Empty, string.Empty, modeKey);
            }

            public static BrowserLine InternalAction(string left, string actionId)
            {
                return new BrowserLine(left, string.Empty, 3, 0, actionId, false, string.Empty, 0);
            }

            public static BrowserLine InternalAction(string left, string right, string actionId)
            {
                return new BrowserLine(left, right, 3, 0, actionId, false, string.Empty, 0);
            }

            public static BrowserLine CopyValue(string left, string right, string value)
            {
                return new BrowserLine(left, right, 0, 0, BrowserCopyTextActionPrefix + (value ?? string.Empty),
                    false, string.Empty, 0);
            }

            public static BrowserLine RecipeItem(string itemId, string right, string chipItemId, int chipStatus)
            {
                return new BrowserLine(itemId, right, 0, 1, string.Empty, true, chipItemId, chipStatus);
            }

            public static BrowserLine RecipeHeader(string left, string right, string chipItemId, int chipStatus)
            {
                return new BrowserLine(left, right, 3, 0, string.Empty, true, chipItemId, chipStatus);
            }

            public static BrowserLine Perk(string perkId, string right)
            {
                return new BrowserLine(perkId, right, 0, 2, string.Empty, false, string.Empty, 0);
            }

            public static BrowserLine Station(
                string left, string right, string spaceObjectId,
                string factionId, int factionRelation)
            {
                return new BrowserLine(
                    left, right, 0, 0, spaceObjectId,
                    false, string.Empty, 0, factionId, factionRelation);
            }

            public static BrowserLine MagnumResearchRow(string route, string quantity, string state)
            {
                return new BrowserLine(
                    route, string.Empty, 0, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.MagnumResearch, quantity, state, string.Empty, string.Empty);
            }

            public static BrowserLine TradeHeader(
                string station, string price, string stock, string mission, string travel)
            {
                return new BrowserLine(
                    station, string.Empty, 2, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.TradeHeader, price, stock, mission, travel);
            }

            public static BrowserLine TradeStation(
                string station, string price, string stock, string mission, string travel, string spaceObjectId,
                string factionId, int factionRelation, int missionArrivalState)
            {
                return new BrowserLine(
                    station, string.Empty, 0, 0, spaceObjectId,
                    false, string.Empty, 0, factionId, factionRelation,
                    BrowserRowKind.TradeStation, price, stock, mission, travel, string.Empty, missionArrivalState);
            }

            public static BrowserLine LootMissionRow(string source, string type, string tech)
            {
                // Mission-pool rows are already filtered to eligible sources. A dedicated
                // STATUS=eligible column only repeated that fact and exposed an internal term.
                return new BrowserLine(
                    source, string.Empty, 0, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.LootRow, type, tech, string.Empty, string.Empty);
            }

            public static BrowserLine LootHeader(
                string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, 2, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.LootHeader, column1, column2, column3, column4);
            }

            public static BrowserLine LootRow(
                string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, 0, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.LootRow, column1, column2, column3, column4);
            }

            public static BrowserLine LootContainerRow(
                string containerId, string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, 0, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.LootRow, column1, column2, column3, column4, containerId);
            }

            public static BrowserLine LootHeader6(
                string left, string column1, string column2, string column3,
                string column4, string column5)
            {
                return new BrowserLine(
                    left, column5, 2, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.LootHeaderSixColumns, column1, column2, column3, column4);
            }

            public static BrowserLine LootRow6(
                string left, string column1, string column2, string column3,
                string column4, string column5, string factionId)
            {
                return new BrowserLine(
                    left, column5, 0, 0, string.Empty,
                    false, string.Empty, 0, factionId, 0,
                    BrowserRowKind.LootRowSixColumns, column1, column2, column3, column4);
            }

            public static BrowserLine FullNote(string left)
            {
                return new BrowserLine(
                    left, string.Empty, 2, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.FullNote);
            }

            public static BrowserLine FullSection(string left)
            {
                return new BrowserLine(
                    left, string.Empty, 1, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.FullSection);
            }

            public static BrowserLine FactionRewardHeader(
                string faction, string reward, string unlock, string current, string state)
            {
                return new BrowserLine(
                    faction, string.Empty, 2, 0, string.Empty,
                    false, string.Empty, 0, string.Empty, 0,
                    BrowserRowKind.FactionRewardHeader, reward, unlock, current, state);
            }

            public static BrowserLine FactionReward(
                string faction, string reward, string unlock, string current, string state,
                string factionId, bool available)
            {
                return new BrowserLine(
                    faction, string.Empty, available ? 3 : 0, 0, string.Empty,
                    false, string.Empty, 0, factionId, 0,
                    BrowserRowKind.FactionReward, reward, unlock, current, state);
            }
        }

    }
}
