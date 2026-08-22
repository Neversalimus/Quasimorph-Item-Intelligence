using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Session-only Loot accordion state. Keys are localized section labels without
        // the count suffix, so the same user's section choice follows related items while
        // the inspector stays open. CloseInspector clears it back to sensible defaults.
        private static readonly Dictionary<string, bool> LootSectionExpanded =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly HashSet<string> LootAccordionAuditLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private const string LootSectionCountSeparator = "  •  ";

        private static bool GetLootSectionExpandedState(string label, int sourceCount)
        {
            string key = label ?? string.Empty;
            bool expanded;
            if (!LootSectionExpanded.TryGetValue(key, out expanded))
            {
                // Keep tiny/special Baron blocks immediately useful, but do not build
                // large hidden tables just to throw them away after rendering.
                expanded = sourceCount < 0 || sourceCount <= 3 ||
                    string.Equals(key, Ui("loot.baron.section"), StringComparison.CurrentCultureIgnoreCase);
                LootSectionExpanded[key] = expanded;
            }
            return expanded;
        }

        private static bool AddLootSectionHeaderAndShouldBuild(string label, int sourceCount)
        {
            BrowserLines.Add(BrowserLine.Section(
                (label ?? string.Empty) + LootSectionCountSeparator +
                Math.Max(0, sourceCount).ToString(CultureInfo.InvariantCulture)));
            return GetLootSectionExpandedState(label, sourceCount);
        }

        private static void ApplyLootCollapsibleSections(int firstSectionRow)
        {
            if (firstSectionRow < 0 || firstSectionRow >= BrowserLines.Count) return;

            List<BrowserLine> compact = new List<BrowserLine>(BrowserLines.Count);
            for (int i = 0; i < firstSectionRow; i++) compact.Add(BrowserLines[i]);

            int auditSections = 0;
            int auditExpanded = 0;
            int auditDeclaredSources = 0;
            HashSet<string> auditKeys = new HashSet<string>(StringComparer.Ordinal);
            int auditDuplicateKeys = 0;

            int index = firstSectionRow;
            while (index < BrowserLines.Count)
            {
                BrowserLine header = BrowserLines[index];
                if (!IsLootSectionHeader(header))
                {
                    compact.Add(header);
                    index++;
                    continue;
                }

                int next = index + 1;
                while (next < BrowserLines.Count && !IsLootSectionHeader(BrowserLines[next])) next++;

                string label;
                int sourceCount;
                SplitLootSectionTitle(header.Left, out label, out sourceCount);
                string key = label;
                bool expanded = GetLootSectionExpandedState(label, sourceCount);

                auditSections++;
                if (expanded) auditExpanded++;
                if (sourceCount >= 0) auditDeclaredSources += sourceCount;
                if (!auditKeys.Add(key)) auditDuplicateKeys++;

                string disclosureLabel = (expanded ? "-  " : "+  ") + label;
                string right = sourceCount >= 0
                    ? sourceCount.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                compact.Add(BrowserLine.CollapsibleSection(
                    disclosureLabel, right, key));

                if (expanded)
                {
                    for (int row = index + 1; row < next; row++) compact.Add(BrowserLines[row]);
                }
                index = next;
            }

            string auditItem = _inspectorItemId ?? string.Empty;
            if (!string.IsNullOrEmpty(auditItem) && LootAccordionAuditLogged.Add(auditItem))
            {
                string audit = "[ItemIntelligence][LootAccordionAudit] item=" + auditItem +
                    ", sections=" + auditSections.ToString(CultureInfo.InvariantCulture) +
                    ", expanded=" + auditExpanded.ToString(CultureInfo.InvariantCulture) +
                    ", declaredSources=" + auditDeclaredSources.ToString(CultureInfo.InvariantCulture) +
                    ", duplicateKeys=" + auditDuplicateKeys.ToString(CultureInfo.InvariantCulture) +
                    ", visibleRows=" + compact.Count.ToString(CultureInfo.InvariantCulture) + ".";
                if (auditDuplicateKeys == 0) Debug.Log(audit); else Debug.LogWarning(audit);
            }

            BrowserLines.Clear();
            BrowserLines.AddRange(compact);
        }

        private static bool IsLootSectionHeader(BrowserLine line)
        {
            return line != null && line.Style == BrowserLineStyle.Section && line.RowKind == BrowserRowKind.Default &&
                line.Action.IsNone &&
                !string.IsNullOrWhiteSpace(line.Left);
        }

        private static void SplitLootSectionTitle(string title, out string label, out int sourceCount)
        {
            label = title ?? string.Empty;
            sourceCount = -1;
            int separator = label.LastIndexOf(LootSectionCountSeparator, StringComparison.Ordinal);
            if (separator < 0) return;

            string rawCount = label.Substring(separator + LootSectionCountSeparator.Length).Trim();
            int parsed;
            if (!int.TryParse(rawCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return;
            label = label.Substring(0, separator).TrimEnd();
            sourceCount = Math.Max(0, parsed);
        }

        private static void HandleLootSectionToggleAction(string key, int clickedRowIndex)
        {
            if (string.IsNullOrEmpty(key)) return;
            bool expanded;
            if (!LootSectionExpanded.TryGetValue(key, out expanded)) expanded = true;
            LootSectionExpanded[key] = !expanded;

            // Keep the clicked section header in view after the page shrinks/grows.
            BrowserNavigation.ScrollOffset = Math.Max(0, clickedRowIndex - 1);
            if (BrowserNavigation.Tab >= 0 && BrowserNavigation.Tab < BrowserNavigation.ScrollOffsets.Length)
                BrowserNavigation.ScrollOffsets[BrowserNavigation.Tab] = BrowserNavigation.ScrollOffset;

            Debug.Log("[ItemIntelligence][LootAccordion] section=" + key +
                ", expanded=" + (!expanded ? "true" : "false") + ".");
            RenderBrowser(_inspectorItemId);
        }

        private static void ResetLootAccordionState()
        {
            LootSectionExpanded.Clear();
            LootAccordionAuditLogged.Clear();
        }
    }
}
