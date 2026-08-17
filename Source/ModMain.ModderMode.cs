using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    /// <summary>
    /// Optional technical presentation for mod authors. Read-only and deliberately
    /// compact: it exposes stable IDs/types already present in the browser indexes and
    /// never scans live scenes or mutates game data.
    /// </summary>
    public static partial class ModMain
    {
        private static void AppendBrowserModderOverview(string itemId)
        {
            if (!ModderMode || string.IsNullOrEmpty(itemId)) return;

            BrowserLines.Add(BrowserLine.Section(Ui("ui.modder_section")));

            object record;
            if (ItemRecordsById.TryGetValue(itemId, out record) && record != null)
            {
                Type type = record.GetType();
                string typeName = type.FullName ?? type.Name ?? string.Empty;
                if (!string.IsNullOrEmpty(typeName))
                    BrowserLines.Add(BrowserLine.CopyValue(Ui("ui.modder_record_type"), typeName, typeName));

                object itemClass = GetMember(record, "ItemClass");
                string itemClassText = itemClass == null ? string.Empty : Convert.ToString(itemClass, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(itemClassText))
                    BrowserLines.Add(BrowserLine.CopyValue(Ui("ui.modder_item_class"), itemClassText, itemClassText));

                int techLevel;
                if (TryGetExactItemTechLevel(itemId, out techLevel))
                    BrowserLines.Add(BrowserLine.CopyValue(Ui("ui.modder_tech_level"),
                        techLevel.ToString(CultureInfo.InvariantCulture), techLevel.ToString(CultureInfo.InvariantCulture)));
            }

            string relationId = ResolveStaticRelationItemId(itemId);
            if (!string.IsNullOrEmpty(relationId) && !string.Equals(relationId, itemId, StringComparison.OrdinalIgnoreCase))
                BrowserLines.Add(BrowserLine.CopyValue(Ui("ui.modder_relation_id"), relationId, relationId));

            List<WeaponModeDescriptor> modes = GetWeaponModesForItem(itemId);
            if (modes != null && modes.Count > 0)
            {
                List<string> rawIds = new List<string>();
                for (int i = 0; i < modes.Count; i++)
                {
                    WeaponModeDescriptor mode = modes[i];
                    if (mode == null || string.IsNullOrEmpty(mode.RawId) || rawIds.Contains(mode.RawId)) continue;
                    rawIds.Add(mode.RawId);
                }
                for (int i = 0; i < rawIds.Count; i++)
                {
                    string rawId = rawIds[i];
                    BrowserLines.Add(BrowserLine.CopyValue(
                        i == 0 ? Ui("ui.modder_firemode_ids") : string.Empty, rawId, rawId));
                }
            }

            BrowserLines.Add(BrowserLine.Note(Ui("ui.modder_search_syntax")));
        }
    }
}
