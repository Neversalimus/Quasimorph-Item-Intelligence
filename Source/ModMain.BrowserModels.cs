using System;
using System.Collections.Generic;

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
            public readonly int ScrollOffset;

            public BrowserItemNavigationState(string itemId, int tab, int scrollOffset)
            {
                ItemId = itemId ?? string.Empty;
                Tab = tab;
                ScrollOffset = scrollOffset;
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
            FullSection = 13,
            OverviewCombatHeader = 14,
            OverviewCombatRow = 15,
            BaronLootHeader = 16,
            BaronLootRow = 17,
            LootSectionHeader = 18,
            LootSpecialHeader = 19,
            LootSpecialRow = 20,
            TradeStationCard = 21
        }

        private enum BrowserLineStyle
        {
            Normal = 0,
            Section = 1,
            Note = 2,
            Accent = 3,
            ScavengerUnknown = 20,
            ScavengerReachable = 21,
            ScavengerExpiresBeforeArrival = 22
        }

        private enum BrowserLeftContentKind
        {
            Text = 0,
            Item = 1,
            MagnumPerk = 2,
            WeaponMode = 3
        }

        private enum BrowserChipStatus
        {
            Locked = -1,
            None = 0,
            Unlocked = 1,
            Unknown = 2
        }

        private enum BrowserFactionRelation
        {
            Hostile = -1,
            Neutral = 0,
            Friendly = 1,
            Unknown = 2
        }

        private enum BrowserTradeArrivalState
        {
            None = 0,
            ComparisonUnavailable = 1,
            MissionExpiresBeforeArrival = 2,
            MissionActiveOnArrival = 3
        }

        private enum BrowserLootModifierCommand
        {
            None = 0,
            ToggleMode = 1,
            CycleMarauder = 2,
            ToggleOrganization = 3,
            ToggleFieldMedic = 4
        }

        private enum BrowserActionKind
        {
            None = 0,
            OpenStarmap = 1,
            OpenItem = 2,
            CopyText = 3,
            SwitchTab = 4,
            ToggleLootSection = 5,
            LootModifier = 6,
            FactionTechnology = 7,
            SecretDataBack = 8,
            SecretDataFaction = 9
        }

        private struct BrowserAction
        {
            public readonly BrowserActionKind Kind;
            public readonly string Payload;
            public readonly BrowserTabId Tab;
            public readonly BrowserLootModifierCommand LootModifierCommand;

            private BrowserAction(
                BrowserActionKind kind, string payload, BrowserTabId tab,
                BrowserLootModifierCommand lootModifierCommand)
            {
                Kind = kind;
                Payload = payload ?? string.Empty;
                Tab = tab;
                LootModifierCommand = lootModifierCommand;
            }

            public bool IsNone
            {
                get { return Kind == BrowserActionKind.None; }
            }

            public bool Equals(BrowserAction other)
            {
                return Kind == other.Kind &&
                    string.Equals(Payload, other.Payload, StringComparison.Ordinal) &&
                    Tab == other.Tab &&
                    LootModifierCommand == other.LootModifierCommand;
            }

            public static BrowserAction None()
            {
                return new BrowserAction(BrowserActionKind.None, string.Empty, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction OpenStarmap(string spaceObjectId)
            {
                if (string.IsNullOrEmpty(spaceObjectId)) return None();
                return new BrowserAction(BrowserActionKind.OpenStarmap, spaceObjectId, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction OpenItem(string itemId)
            {
                if (string.IsNullOrEmpty(itemId)) return None();
                return new BrowserAction(BrowserActionKind.OpenItem, itemId, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction CopyText(string value)
            {
                return new BrowserAction(BrowserActionKind.CopyText, value, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction SwitchTab(BrowserTabId tab)
            {
                int tabIndex = (int)tab;
                if (tabIndex < 0 || tabIndex >= (int)BrowserTabId.Count) return None();
                return new BrowserAction(BrowserActionKind.SwitchTab, string.Empty, tab, BrowserLootModifierCommand.None);
            }

            public static BrowserAction ToggleLootSection(string key)
            {
                if (string.IsNullOrEmpty(key)) return None();
                return new BrowserAction(BrowserActionKind.ToggleLootSection, key, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction LootModifier(BrowserLootModifierCommand command)
            {
                if (command == BrowserLootModifierCommand.None) return None();
                return new BrowserAction(BrowserActionKind.LootModifier, string.Empty, BrowserTabId.Overview, command);
            }

            public static BrowserAction FactionTechnology(string factionId)
            {
                if (string.IsNullOrEmpty(factionId)) return None();
                return new BrowserAction(BrowserActionKind.FactionTechnology, factionId, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction SecretDataBack()
            {
                return new BrowserAction(BrowserActionKind.SecretDataBack, string.Empty, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }

            public static BrowserAction SecretDataFaction(string factionId)
            {
                if (string.IsNullOrEmpty(factionId)) return None();
                return new BrowserAction(BrowserActionKind.SecretDataFaction, factionId, BrowserTabId.Overview, BrowserLootModifierCommand.None);
            }
        }

        private sealed class BrowserNavigationSessionState
        {
            public int Tab;
            public int ScrollOffset;
            public readonly List<BrowserItemNavigationState> History = new List<BrowserItemNavigationState>();
            public readonly int[] ScrollOffsets = new int[BrowserTabCount];
        }

        private static BrowserChipStatus ToBrowserChipStatus(int value)
        {
            if (value == 1) return BrowserChipStatus.Unlocked;
            if (value == -1) return BrowserChipStatus.Locked;
            if (value == 2) return BrowserChipStatus.Unknown;
            return BrowserChipStatus.None;
        }

        private static BrowserFactionRelation ToBrowserFactionRelation(int value)
        {
            if (value == 1) return BrowserFactionRelation.Friendly;
            if (value == -1) return BrowserFactionRelation.Hostile;
            if (value == 0) return BrowserFactionRelation.Neutral;
            return BrowserFactionRelation.Unknown;
        }

        private static BrowserTradeArrivalState ToBrowserTradeArrivalState(int value)
        {
            if (value == 1) return BrowserTradeArrivalState.ComparisonUnavailable;
            if (value == 2) return BrowserTradeArrivalState.MissionExpiresBeforeArrival;
            if (value == 3) return BrowserTradeArrivalState.MissionActiveOnArrival;
            return BrowserTradeArrivalState.None;
        }

        private static BrowserLineStyle ToScavengerLineStyle(int arrivalState)
        {
            if (arrivalState == 1) return BrowserLineStyle.ScavengerReachable;
            if (arrivalState == 2) return BrowserLineStyle.ScavengerExpiresBeforeArrival;
            return BrowserLineStyle.ScavengerUnknown;
        }

        private sealed class BrowserLine
        {
            public readonly string Left;
            public readonly string Right;
            public readonly BrowserLineStyle Style;
            public readonly BrowserLeftContentKind LeftContentKind;
            public readonly BrowserAction Action;
            public readonly bool ShowRecipeChipContext;
            public readonly string ChipItemId;
            public readonly BrowserChipStatus ChipStatus;
            public readonly string FactionId;
            public readonly BrowserFactionRelation FactionRelation;
            public readonly BrowserRowKind RowKind;
            public readonly string ColumnReward;
            public readonly string ColumnUnlock;
            public readonly string ColumnCurrent;
            public readonly string ColumnState;
            public readonly string ContainerIconId;
            public readonly BrowserTradeArrivalState TradeArrivalState;

            private BrowserLine(
                string left, string right, BrowserLineStyle style, BrowserLeftContentKind leftContentKind,
                BrowserAction action, bool showRecipeChipContext, string chipItemId, BrowserChipStatus chipStatus,
                string factionId = "", BrowserFactionRelation factionRelation = BrowserFactionRelation.Neutral,
                BrowserRowKind rowKind = BrowserRowKind.Default, string columnReward = "", string columnUnlock = "",
                string columnCurrent = "", string columnState = "", string containerIconId = "",
                BrowserTradeArrivalState tradeArrivalState = BrowserTradeArrivalState.None)
            {
                Left = left ?? string.Empty;
                Right = right ?? string.Empty;
                Style = style;
                LeftContentKind = leftContentKind;
                Action = action;
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
                TradeArrivalState = tradeArrivalState;
            }

            public static BrowserLine Normal(string left, string right)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine Section(string left)
            {
                return new BrowserLine(left, string.Empty, BrowserLineStyle.Section, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine CollapsibleSection(string left, string right, string sectionKey)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Section, BrowserLeftContentKind.Text,
                    BrowserAction.ToggleLootSection(sectionKey), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.LootSectionHeader);
            }

            public static BrowserLine Note(string left)
            {
                return new BrowserLine(left, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine ChipNote(string left)
            {
                return new BrowserLine(left, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.ChipNote);
            }

            public static BrowserLine Accent(string left, string right)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Accent, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine Item(string itemId, string right)
            {
                return new BrowserLine(itemId, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Item,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine ItemAction(string itemId, string right)
            {
                return new BrowserLine(itemId, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Item,
                    BrowserAction.OpenItem(itemId), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine Header(string left, string right)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Section, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine ChipUnlockAction(string itemId, string right, int unlockStatus)
            {
                return new BrowserLine(itemId, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Item,
                    BrowserAction.OpenItem(itemId), false, string.Empty, ToBrowserChipStatus(unlockStatus),
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.ChipUnlock);
            }

            public static BrowserLine WeaponMode(string label, string modeKey, string right)
            {
                return new BrowserLine(label, right, BrowserLineStyle.Normal, BrowserLeftContentKind.WeaponMode,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.Default,
                    string.Empty, string.Empty, string.Empty, string.Empty, modeKey);
            }

            public static BrowserLine OverviewCombatHeader(string left, string normal, string crit)
            {
                return new BrowserLine(left, string.Empty, BrowserLineStyle.Section, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.OverviewCombatHeader, normal, crit);
            }

            public static BrowserLine OverviewCombatRow(string label, string modeKey, string normal, string crit)
            {
                return new BrowserLine(label, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.WeaponMode,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.OverviewCombatRow,
                    normal, crit, string.Empty, string.Empty, modeKey);
            }

            public static BrowserLine InternalAction(string left, BrowserAction action)
            {
                return new BrowserLine(left, string.Empty, BrowserLineStyle.Accent, BrowserLeftContentKind.Text,
                    action, false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine InternalAction(string left, string right, BrowserAction action)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Accent, BrowserLeftContentKind.Text,
                    action, false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine CopyValue(string left, string right, string value)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.CopyText(value), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine RecipeItem(string itemId, string right, string chipItemId, int chipStatus)
            {
                return new BrowserLine(itemId, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Item,
                    BrowserAction.None(), true, chipItemId, ToBrowserChipStatus(chipStatus));
            }

            public static BrowserLine RecipeHeader(string left, string right, string chipItemId, int chipStatus)
            {
                return new BrowserLine(left, right, BrowserLineStyle.Accent, BrowserLeftContentKind.Text,
                    BrowserAction.None(), true, chipItemId, ToBrowserChipStatus(chipStatus));
            }

            public static BrowserLine Perk(string perkId, string right)
            {
                return new BrowserLine(perkId, right, BrowserLineStyle.Normal, BrowserLeftContentKind.MagnumPerk,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None);
            }

            public static BrowserLine StationAction(
                string left, string right, BrowserAction action,
                string factionId, int factionRelation)
            {
                return new BrowserLine(
                    left, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    action, false, string.Empty, BrowserChipStatus.None,
                    factionId, ToBrowserFactionRelation(factionRelation));
            }

            public static BrowserLine MagnumResearchRow(string route, string quantity, string state)
            {
                return new BrowserLine(
                    route, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.MagnumResearch, quantity, state, string.Empty, string.Empty);
            }

            public static BrowserLine TradeHeader(
                string station, string price, string stock, string mission, string travel, string right = "")
            {
                return new BrowserLine(
                    station, right, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.TradeHeader, price, stock, mission, travel);
            }

            public static BrowserLine TradeStation(
                string station, string price, string stock, string mission, string travel, string spaceObjectId,
                string factionId, int factionRelation, int missionArrivalState, string right = "")
            {
                return new BrowserLine(
                    station, right, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.OpenStarmap(spaceObjectId),
                    false, string.Empty, BrowserChipStatus.None,
                    factionId, ToBrowserFactionRelation(factionRelation),
                    BrowserRowKind.TradeStation, price, stock, mission, travel, string.Empty,
                    ToBrowserTradeArrivalState(missionArrivalState));
            }

            public static BrowserLine TradeStationCard103(
                string station, string priceLine, string middleLine, string travelMissionLine, string spaceObjectId,
                string factionId, int factionRelation, int missionArrivalState)
            {
                return new BrowserLine(
                    station + "\n" + priceLine, travelMissionLine, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.OpenStarmap(spaceObjectId),
                    false, string.Empty, BrowserChipStatus.None,
                    factionId, ToBrowserFactionRelation(factionRelation),
                    BrowserRowKind.TradeStationCard, middleLine, string.Empty, string.Empty, string.Empty, string.Empty,
                    ToBrowserTradeArrivalState(missionArrivalState));
            }

            public static BrowserLine LootMissionRow(string source, string type, string tech)
            {
                return new BrowserLine(
                    source, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootRow, type, tech, string.Empty, string.Empty);
            }

            public static BrowserLine LootHeader(
                string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootHeader, column1, column2, column3, column4);
            }

            public static BrowserLine LootRow(
                string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootRow, column1, column2, column3, column4);
            }

            public static BrowserLine LootContainerRow(
                string containerId, string left, string column1, string column2, string column3, string column4)
            {
                return new BrowserLine(
                    left, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootRow, column1, column2, column3, column4, containerId);
            }


            public static BrowserLine LootSpecialHeader(
                string source, string kind, string condition, string result,
                string layoutTag = "")
            {
                return new BrowserLine(
                    source, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootSpecialHeader, kind, condition, result, layoutTag);
            }

            public static BrowserLine LootSpecialRow(
                string source, string kind, string condition, string result,
                string layoutTag = "")
            {
                return new BrowserLine(
                    source, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootSpecialRow, kind, condition, result, layoutTag);
            }

            public static BrowserLine LootHeader6(
                string left, string column1, string column2, string column3,
                string column4, string column5)
            {
                return new BrowserLine(
                    left, column5, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootHeaderSixColumns, column1, column2, column3, column4);
            }

            public static BrowserLine LootRow6(
                string left, string column1, string column2, string column3,
                string column4, string column5, string factionId)
            {
                return new BrowserLine(
                    left, column5, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    factionId, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootRowSixColumns, column1, column2, column3, column4);
            }

            public static BrowserLine BaronLootHeader(
                string baron, string itemChance, string pactChance)
            {
                return new BrowserLine(
                    baron, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.BaronLootHeader, itemChance, pactChance);
            }

            public static BrowserLine BaronLootRow(
                string baron, string itemChance, string pactChance)
            {
                return new BrowserLine(
                    baron, string.Empty, BrowserLineStyle.Normal, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.BaronLootRow, itemChance, pactChance);
            }

            public static BrowserLine FullNote(string left)
            {
                return new BrowserLine(
                    left, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.FullNote);
            }

            public static BrowserLine FullSection(string left)
            {
                return new BrowserLine(
                    left, string.Empty, BrowserLineStyle.Section, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral, BrowserRowKind.FullSection);
            }

            public static BrowserLine ScavengerMissionHeader(
                string station, string opponent, string chance, string rolls, string tech)
            {
                return new BrowserLine(
                    station, string.Empty, BrowserLineStyle.ScavengerUnknown, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootHeader, opponent, chance, rolls, tech);
            }

            public static BrowserLine ScavengerMissionRow(
                string station, string opponent, string chance, string travel, string timeLeft,
                string spaceObjectId, int arrivalState)
            {
                return new BrowserLine(
                    station, string.Empty, ToScavengerLineStyle(arrivalState), BrowserLeftContentKind.Text,
                    BrowserAction.OpenStarmap(spaceObjectId),
                    false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.LootRow, opponent, chance, travel, timeLeft);
            }

            public static BrowserLine FactionRewardHeader(
                string faction, string reward, string unlock, string current, string state)
            {
                return new BrowserLine(
                    faction, string.Empty, BrowserLineStyle.Note, BrowserLeftContentKind.Text,
                    BrowserAction.None(), false, string.Empty, BrowserChipStatus.None,
                    string.Empty, BrowserFactionRelation.Neutral,
                    BrowserRowKind.FactionRewardHeader, reward, unlock, current, state);
            }

            public static BrowserLine FactionReward(
                string faction, string reward, string unlock, string current, string state,
                string factionId, bool available)
            {
                return new BrowserLine(
                    faction, string.Empty, available ? BrowserLineStyle.Accent : BrowserLineStyle.Normal,
                    BrowserLeftContentKind.Text,
                    string.IsNullOrEmpty(factionId) ? BrowserAction.None() : BrowserAction.FactionTechnology(factionId),
                    false, string.Empty, BrowserChipStatus.None,
                    factionId, BrowserFactionRelation.Neutral,
                    BrowserRowKind.FactionReward, reward, unlock, current, state);
            }
        }

    }
}
