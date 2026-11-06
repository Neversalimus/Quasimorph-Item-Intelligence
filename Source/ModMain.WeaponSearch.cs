using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, string> BrowserWeaponClasses =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BrowserWeaponClassTerms =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BrowserWeaponPerkTerms =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _browserCatalogWeaponClass = string.Empty;

        private static void ResetBrowserWeaponSearchIndex()
        {
            BrowserWeaponClasses.Clear();
            BrowserWeaponClassTerms.Clear();
            BrowserWeaponPerkTerms.Clear();
        }

        private static string LocalizeWeaponClass(string classId)
        {
            if (classId == "ThrowingWeapon") return Ui("catalog.weapon_throwing");
            return NormalizeGameText(LocalizeCandidates(new string[] { "weapon.class." + classId }, classId));
        }

        private static void IndexBrowserWeaponSearchMetadata(string itemId)
        {
            WeaponRecord weapon = GetBaseWeaponRecord(itemId);
            List<string> traits;
            string classId;
            if (weapon != null)
            {
                classId = weapon.WeaponClass.ToString();
                traits = weapon.Traits;
            }
            else
            {
                object record;
                ItemRecordsById.TryGetValue(itemId, out record);
                CompositeItemRecord composite = record as CompositeItemRecord;
                ThrowingWeaponRecord throwing = record as ThrowingWeaponRecord ??
                    (composite == null ? null : composite.GetRecord<ThrowingWeaponRecord>());
                if (throwing == null) return;
                classId = "ThrowingWeapon";
                traits = throwing.Traits;
            }
            BrowserWeaponClasses[itemId] = classId;
            BrowserWeaponClassTerms[itemId] = NormalizeBrowserSearchText(classId + " " + LocalizeWeaponClass(classId));
            List<string> perks = new List<string>();
            if (traits != null)
                foreach (string traitId in traits)
                {
                    if (string.IsNullOrEmpty(traitId)) continue;
                    perks.Add(traitId);
                    perks.Add(LocalizeWeaponModePayloadTrait(traitId));
                }
            BrowserWeaponPerkTerms[itemId] = NormalizeBrowserSearchText(string.Join(" ", perks.ToArray()));
        }

        private static bool BrowserWeaponTermsMatch(string itemId, List<string> terms, bool perks)
        {
            if (terms.Count == 0) return true;
            string text;
            if (!(perks ? BrowserWeaponPerkTerms : BrowserWeaponClassTerms).TryGetValue(itemId, out text)) return false;
            foreach (string term in terms)
                if (text.IndexOf(term, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private static int ScoreBrowserWeaponSearch(string itemId, string name, string id, string query, string[] tokens)
        {
            string classes, perks;
            if (!BrowserWeaponClassTerms.TryGetValue(itemId, out classes)) return -1;
            BrowserWeaponPerkTerms.TryGetValue(itemId, out perks);
            int score = ScoreBrowserSearchMatch(name + " " + id + " " + classes + " " + perks, string.Empty, query, tokens);
            return score < 0 ? -1 : 100 + score;
        }

        private static bool BrowserCatalogItemMatchesWeaponClass(string itemId)
        {
            if (_browserCatalogCategory != 1 || string.IsNullOrEmpty(_browserCatalogWeaponClass)) return true;
            string classId;
            return BrowserWeaponClasses.TryGetValue(itemId, out classId) &&
                string.Equals(classId, _browserCatalogWeaponClass, StringComparison.OrdinalIgnoreCase);
        }
    }
}
