using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Global-search query parsing and matching owner. Plain text keeps the existing
    /// localized name/ID AND search, while optional field operators add technical
    /// filtering without triggering Loot or other deferred heavy indexes.
    /// </summary>
    public static partial class ModMain
    {
        private sealed class BrowserAdvancedSearchQuery
        {
            public readonly List<string> PlainTerms = new List<string>();
            public readonly List<string> IdTerms = new List<string>();
            public readonly List<string> NameTerms = new List<string>();
            public readonly List<string> TypeTerms = new List<string>();
            public readonly List<string> RelationTerms = new List<string>();
            public readonly List<int> Categories = new List<int>();
            public readonly List<BrowserTechConstraint> Tech = new List<BrowserTechConstraint>();
            public string Signature = string.Empty;
            public string PlainJoined = string.Empty;
            public string[] PlainTokens = new string[0];
            public bool Valid = true;
            public bool HasTechnicalFilter;
        }

        private sealed class BrowserTechConstraint
        {
            public readonly int Mode; // 0 ==, 1 >=, 2 <=, 3 >, 4 <
            public readonly int Value;

            public BrowserTechConstraint(int mode, int value)
            {
                Mode = mode;
                Value = value;
            }
        }

        private static readonly Dictionary<string, string> BrowserSearchRecordTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BrowserSearchRelationIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> BrowserSearchTechLevels =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static void ResetBrowserAdvancedSearchIndexState()
        {
            BrowserSearchRecordTypes.Clear();
            BrowserSearchRelationIds.Clear();
            BrowserSearchTechLevels.Clear();
        }

        private static void EnsureBrowserSearchIndexWarmup()
        {
            if (!_compatSearchCatalog) return;

            EnsureLocalizationCacheLanguage();
            string language = GetLanguageSignature();
            bool rebuild =
                !string.Equals(language, _browserSearchIndexLanguage, StringComparison.OrdinalIgnoreCase) ||
                BrowserSearchIndexItemIds.Count != KnownItemIds.Count;
            if (!rebuild) return;

            _browserSearchIndexLanguage = language;
            BrowserSearchIndexItemIds.Clear();
            BrowserSearchDisplayNames.Clear();
            BrowserSearchNormalizedNames.Clear();
            BrowserSearchNormalizedIds.Clear();
            BrowserSearchCurrentMatches.Clear();
            BrowserCatalogCategoryByItem.Clear();
            ResetBrowserAdvancedSearchIndexState();
            _browserSearchWarmupIndex = 0;
            _browserSearchIndexRevision++;
            _browserSearchLastResultRevision = -1;
            _browserSearchLastResultLanguage = string.Empty;
            _browserSearchLastResultCount = 0;
            _browserSearchScrollOffset = 0;
            _browserSearchLastNormalizedQuery = string.Empty;

            foreach (string itemId in KnownItemIds)
            {
                if (string.IsNullOrEmpty(itemId)) continue;
                BrowserSearchIndexItemIds.Add(itemId);
                BrowserSearchNormalizedIds[itemId] = NormalizeBrowserSearchText(itemId);
            }

            BrowserSearchIndexItemIds.Sort(StringComparer.OrdinalIgnoreCase);
            _browserSearchWarmupActive = BrowserSearchIndexItemIds.Count > 0;
            UpdateBrowserSearchStatus();
        }

        private static void TickBrowserSearchIndexWarmup()
        {
            if (!_browserSearchWarmupActive) return;

            const int batchSize = 24;
            const double frameBudgetMs = 0.90;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int processed = 0;
            while (_browserSearchWarmupIndex < BrowserSearchIndexItemIds.Count && processed < batchSize &&
                   !PerformanceBudgetExceeded(started, frameBudgetMs))
            {
                string itemId = BrowserSearchIndexItemIds[_browserSearchWarmupIndex];
                string display = NormalizeGameText(LocalizeItem(itemId));
                if (!string.IsNullOrEmpty(display))
                {
                    BrowserSearchDisplayNames[itemId] = display;
                    BrowserSearchNormalizedNames[itemId] = NormalizeBrowserSearchText(display);
                }

                if (!BrowserCatalogCategoryByItem.ContainsKey(itemId))
                    BrowserCatalogCategoryByItem[itemId] = ClassifyCatalogItem(itemId);
                IndexBrowserAdvancedSearchMetadata(itemId);

                _browserSearchWarmupIndex++;
                processed++;
            }
            if (processed > 0) _browserSearchIndexRevision++;

            if (_browserSearchWarmupIndex >= BrowserSearchIndexItemIds.Count)
            {
                _browserSearchWarmupActive = false;
                if (_browserCatalogOpen) RefreshBrowserCatalog();
                Debug.Log("[ItemIntelligence][AdvancedSearch] index ready: items=" +
                    BrowserSearchIndexItemIds.Count.ToString(CultureInfo.InvariantCulture) +
                    ", type=" + BrowserSearchRecordTypes.Count.ToString(CultureInfo.InvariantCulture) +
                    ", tech=" + BrowserSearchTechLevels.Count.ToString(CultureInfo.InvariantCulture) +
                    ", relation=" + BrowserSearchRelationIds.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            UpdateBrowserSearchStatus();
            if (_browserSearchInput != null && _browserSearchInput.isFocused &&
                !string.IsNullOrEmpty(_browserSearchInput.text) &&
                (Time.frameCount - _browserSearchLastRefreshFrame >= 6 || !_browserSearchWarmupActive))
            {
                RefreshBrowserSearchSuggestions(_browserSearchInput.text);
            }
        }

        private static void IndexBrowserAdvancedSearchMetadata(string itemId)
        {
            object record;
            if (!ItemRecordsById.TryGetValue(itemId, out record) || record == null) return;

            Type type = record.GetType();
            string typeName = type.FullName ?? type.Name ?? string.Empty;
            BrowserSearchRecordTypes[itemId] = NormalizeBrowserSearchText(typeName);

            string relationId = ResolveStaticRelationItemId(itemId);
            if (!string.IsNullOrEmpty(relationId))
                BrowserSearchRelationIds[itemId] = NormalizeBrowserSearchText(relationId);

            int techLevel;
            if (TryGetExactItemTechLevel(itemId, out techLevel))
                BrowserSearchTechLevels[itemId] = techLevel;
        }

        private static string NormalizeBrowserSearchText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = NormalizeGameText(value).Trim().ToLowerInvariant();
            if (value.Length == 0) return string.Empty;

            StringBuilder sb = new StringBuilder(value.Length);
            bool pendingSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == 'ё') c = 'е';
                if (char.IsLetterOrDigit(c))
                {
                    if (pendingSpace && sb.Length > 0) sb.Append(' ');
                    sb.Append(c);
                    pendingSpace = false;
                }
                else pendingSpace = true;
            }
            return sb.ToString();
        }

        private static List<string> TokenizeBrowserAdvancedSearch(string raw)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(raw)) return tokens;

            StringBuilder current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Length = 0;
                    }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        private static BrowserAdvancedSearchQuery ParseBrowserAdvancedSearchQuery(string rawQuery)
        {
            BrowserAdvancedSearchQuery result = new BrowserAdvancedSearchQuery();
            List<string> tokens = TokenizeBrowserAdvancedSearch(rawQuery ?? string.Empty);
            StringBuilder signature = new StringBuilder();

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = (tokens[i] ?? string.Empty).Trim();
                if (token.Length == 0) continue;
                if (signature.Length > 0) signature.Append('|');
                signature.Append(token.ToLowerInvariant());

                int split = token.IndexOf(':');
                if (split <= 0 || split >= token.Length - 1)
                {
                    string plain = NormalizeBrowserSearchText(token);
                    if (!string.IsNullOrEmpty(plain)) result.PlainTerms.Add(plain);
                    continue;
                }

                string field = token.Substring(0, split).Trim().ToLowerInvariant();
                string rawValue = token.Substring(split + 1).Trim();
                string value = NormalizeBrowserSearchText(rawValue);
                if (string.IsNullOrEmpty(value))
                {
                    result.Valid = false;
                    continue;
                }

                if (field == "id") { result.IdTerms.Add(value); result.HasTechnicalFilter = true; }
                else if (field == "name") result.NameTerms.Add(value);
                else if (field == "type") { result.TypeTerms.Add(value); result.HasTechnicalFilter = true; }
                else if (field == "rel" || field == "relation") { result.RelationTerms.Add(value); result.HasTechnicalFilter = true; }
                else if (field == "cat" || field == "category")
                {
                    int category = ResolveBrowserSearchCategory(value);
                    if (category < 0) result.Valid = false;
                    else { result.Categories.Add(category); result.HasTechnicalFilter = true; }
                }
                else if (field == "tech")
                {
                    BrowserTechConstraint tech;
                    if (!TryParseBrowserTechConstraint(rawValue, out tech)) result.Valid = false;
                    else { result.Tech.Add(tech); result.HasTechnicalFilter = true; }
                }
                else
                {
                    // Unknown prefixes remain normal searchable text for backward compatibility.
                    string plain = NormalizeBrowserSearchText(token);
                    if (!string.IsNullOrEmpty(plain)) result.PlainTerms.Add(plain);
                }
            }

            result.Signature = signature.ToString();
            if (result.PlainTerms.Count > 0)
            {
                result.PlainTokens = result.PlainTerms.ToArray();
                result.PlainJoined = string.Join(" ", result.PlainTokens);
            }
            return result;
        }

        private static bool TryParseBrowserTechConstraint(string raw, out BrowserTechConstraint constraint)
        {
            constraint = null;
            if (string.IsNullOrEmpty(raw)) return false;
            string value = raw.Trim();
            int mode = 0;
            if (value.StartsWith(">=", StringComparison.Ordinal)) { mode = 1; value = value.Substring(2); }
            else if (value.StartsWith("<=", StringComparison.Ordinal)) { mode = 2; value = value.Substring(2); }
            else if (value.StartsWith(">", StringComparison.Ordinal)) { mode = 3; value = value.Substring(1); }
            else if (value.StartsWith("<", StringComparison.Ordinal)) { mode = 4; value = value.Substring(1); }
            else if (value.StartsWith("=", StringComparison.Ordinal)) value = value.Substring(1);

            int number;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
            constraint = new BrowserTechConstraint(mode, number);
            return true;
        }

        private static int ResolveBrowserSearchCategory(string normalized)
        {
            if (string.IsNullOrEmpty(normalized)) return -1;
            if (normalized == "all" || normalized == "все") return 0;
            if (normalized == "weapon" || normalized == "weapons" || normalized == "оружие") return 1;
            if (normalized == "armor" || normalized == "armour" || normalized == "броня") return 2;
            if (normalized == "ammo" || normalized == "ammunition" || normalized == "патроны" || normalized == "боеприпасы") return 3;
            if (normalized == "implant" || normalized == "implants" || normalized == "augmentation" || normalized == "импланты" || normalized == "аугментации") return 4;
            if (normalized == "consumable" || normalized == "consumables" || normalized == "расходники") return 5;
            if (normalized == "chip" || normalized == "chips" || normalized == "datadisk" || normalized == "чипы" || normalized == "диски") return 6;
            if (normalized == "container" || normalized == "containers" || normalized == "контейнеры" || normalized == "ящики") return 7;
            if (normalized == "other" || normalized == "прочее") return 8;
            return -1;
        }

        private static bool BrowserSearchWordStartsWith(string normalizedText, string token)
        {
            if (string.IsNullOrEmpty(normalizedText) || string.IsNullOrEmpty(token)) return false;
            if (normalizedText.StartsWith(token, StringComparison.Ordinal)) return true;
            return normalizedText.IndexOf(" " + token, StringComparison.Ordinal) >= 0;
        }

        private static int ScoreBrowserSearchMatch(string normalizedName, string normalizedId, string query, string[] queryTokens)
        {
            if (string.IsNullOrEmpty(query)) return -1;
            if (!string.IsNullOrEmpty(normalizedName))
            {
                if (string.Equals(normalizedName, query, StringComparison.Ordinal)) return 0;
                if (normalizedName.StartsWith(query, StringComparison.Ordinal)) return 10;
                if (normalizedName.IndexOf(query, StringComparison.Ordinal) >= 0) return 20;
            }
            if (!string.IsNullOrEmpty(normalizedId))
            {
                if (string.Equals(normalizedId, query, StringComparison.Ordinal)) return 30;
                if (normalizedId.StartsWith(query, StringComparison.Ordinal)) return 40;
                if (normalizedId.IndexOf(query, StringComparison.Ordinal) >= 0) return 50;
            }

            if (queryTokens == null || queryTokens.Length == 0) return -1;
            int score = 70;
            bool allInName = !string.IsNullOrEmpty(normalizedName);
            for (int i = 0; i < queryTokens.Length; i++)
            {
                string token = queryTokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                bool inName = !string.IsNullOrEmpty(normalizedName) && normalizedName.IndexOf(token, StringComparison.Ordinal) >= 0;
                bool inId = !string.IsNullOrEmpty(normalizedId) && normalizedId.IndexOf(token, StringComparison.Ordinal) >= 0;
                if (!inName && !inId) return -1;
                if (!inName) allInName = false;
                if (inName && BrowserSearchWordStartsWith(normalizedName, token)) score += 0;
                else if (inName) score += 2;
                else if (BrowserSearchWordStartsWith(normalizedId, token)) score += 4;
                else score += 6;
            }
            if (allInName) score -= 8;
            return score;
        }

        private static bool BrowserAdvancedSearchMatches(string itemId, string normalizedName, string normalizedId,
            BrowserAdvancedSearchQuery query, out int score)
        {
            score = 80;
            if (query == null || !query.Valid) return false;

            for (int i = 0; i < query.IdTerms.Count; i++)
            {
                string term = query.IdTerms[i];
                if (normalizedId.IndexOf(term, StringComparison.Ordinal) < 0) return false;
                if (string.Equals(normalizedId, term, StringComparison.Ordinal)) score -= 30;
                else if (normalizedId.StartsWith(term, StringComparison.Ordinal)) score -= 20;
                else score -= 10;
            }

            for (int i = 0; i < query.NameTerms.Count; i++)
            {
                string term = query.NameTerms[i];
                if (string.IsNullOrEmpty(normalizedName) || normalizedName.IndexOf(term, StringComparison.Ordinal) < 0) return false;
                if (string.Equals(normalizedName, term, StringComparison.Ordinal)) score -= 25;
                else if (normalizedName.StartsWith(term, StringComparison.Ordinal)) score -= 15;
                else score -= 5;
            }

            string typeName;
            if (!BrowserSearchRecordTypes.TryGetValue(itemId, out typeName)) typeName = string.Empty;
            for (int i = 0; i < query.TypeTerms.Count; i++)
                if (typeName.IndexOf(query.TypeTerms[i], StringComparison.Ordinal) < 0) return false;

            string relationId;
            if (!BrowserSearchRelationIds.TryGetValue(itemId, out relationId)) relationId = string.Empty;
            for (int i = 0; i < query.RelationTerms.Count; i++)
                if (relationId.IndexOf(query.RelationTerms[i], StringComparison.Ordinal) < 0) return false;

            int category;
            if (!BrowserCatalogCategoryByItem.TryGetValue(itemId, out category)) category = -1;
            for (int i = 0; i < query.Categories.Count; i++)
                if (query.Categories[i] != 0 && category != query.Categories[i]) return false;

            if (query.Tech.Count > 0)
            {
                int tech;
                if (!BrowserSearchTechLevels.TryGetValue(itemId, out tech)) return false;
                for (int i = 0; i < query.Tech.Count; i++)
                {
                    BrowserTechConstraint c = query.Tech[i];
                    if (c.Mode == 0 && tech != c.Value) return false;
                    if (c.Mode == 1 && tech < c.Value) return false;
                    if (c.Mode == 2 && tech > c.Value) return false;
                    if (c.Mode == 3 && tech <= c.Value) return false;
                    if (c.Mode == 4 && tech >= c.Value) return false;
                }
            }

            if (query.PlainTokens.Length > 0)
            {
                int plainScore = ScoreBrowserSearchMatch(
                    normalizedName, normalizedId, query.PlainJoined, query.PlainTokens);
                if (plainScore < 0) return false;
                score += plainScore;
            }
            return true;
        }

        private static void RefreshBrowserSearchSuggestions(string rawQuery)
        {
            BrowserAdvancedSearchQuery parsed = ParseBrowserAdvancedSearchQuery(rawQuery);
            if (parsed.PlainTerms.Count == 0 && parsed.IdTerms.Count == 0 && parsed.NameTerms.Count == 0 &&
                parsed.TypeTerms.Count == 0 && parsed.RelationTerms.Count == 0 && parsed.Categories.Count == 0 && parsed.Tech.Count == 0)
            {
                _browserSearchLastResultCount = 0;
                BrowserSearchCurrentMatches.Clear();
                _browserSearchScrollOffset = 0;
                _browserSearchLastNormalizedQuery = string.Empty;
                _browserSearchLastResultRevision = _browserSearchIndexRevision;
                _browserSearchLastResultLanguage = _browserSearchIndexLanguage;
                HideBrowserSearchDropdown();
                UpdateBrowserSearchStatus();
                return;
            }

            EnsureBrowserSearchDropdownUi();
            if (_browserSearchDropdown == null) return;
            EnsureBrowserSearchIndexWarmup();

            bool queryChanged = !string.Equals(parsed.Signature, _browserSearchLastNormalizedQuery, StringComparison.Ordinal);
            if (queryChanged)
            {
                _browserSearchScrollOffset = 0;
                _browserSearchLastNormalizedQuery = parsed.Signature;
            }

            bool cachedResult = !queryChanged &&
                _browserSearchLastResultRevision == _browserSearchIndexRevision &&
                string.Equals(_browserSearchLastResultLanguage, _browserSearchIndexLanguage, StringComparison.OrdinalIgnoreCase);
            if (cachedResult)
            {
                // A zero-result query is a complete result too. Do not rescan the entire
                // item index merely because RenderBrowserSearchCurrentPage hid the dropdown.
                if (BrowserSearchCurrentMatches.Count > 0) RenderBrowserSearchCurrentPage();
                else
                {
                    HideBrowserSearchDropdown();
                    UpdateBrowserSearchStatus();
                }
                return;
            }

            BrowserSearchCurrentMatches.Clear();
            if (!parsed.Valid)
            {
                _browserSearchLastResultCount = 0;
                _browserSearchLastResultRevision = _browserSearchIndexRevision;
                _browserSearchLastResultLanguage = _browserSearchIndexLanguage;
                _browserSearchLastRefreshFrame = Time.frameCount;
                RenderBrowserSearchCurrentPage();
                return;
            }

            for (int i = 0; i < BrowserSearchIndexItemIds.Count; i++)
            {
                string itemId = BrowserSearchIndexItemIds[i];
                string displayName;
                if (!BrowserSearchDisplayNames.TryGetValue(itemId, out displayName)) displayName = string.Empty;
                string normalizedName;
                if (!BrowserSearchNormalizedNames.TryGetValue(itemId, out normalizedName)) normalizedName = string.Empty;
                string normalizedId;
                if (!BrowserSearchNormalizedIds.TryGetValue(itemId, out normalizedId))
                {
                    normalizedId = NormalizeBrowserSearchText(itemId);
                    BrowserSearchNormalizedIds[itemId] = normalizedId;
                }

                int score;
                if (!BrowserAdvancedSearchMatches(itemId, normalizedName, normalizedId, parsed, out score)) continue;

                // Plain player-facing queries keep the old rule that hides untranslated
                // internal records. Explicit technical operators intentionally expose them.
                if (!parsed.HasTechnicalFilter && parsed.IdTerms.Count == 0 &&
                    (string.IsNullOrEmpty(displayName) || string.Equals(displayName, itemId, StringComparison.OrdinalIgnoreCase)))
                {
                    bool idMatch = true;
                    for (int t = 0; t < parsed.PlainTerms.Count; t++)
                    {
                        string term = parsed.PlainTerms[t];
                        if (normalizedId.IndexOf(term, StringComparison.Ordinal) < 0) { idMatch = false; break; }
                    }
                    if (!idMatch) continue;
                }

                BrowserSearchCurrentMatches.Add(new BrowserSearchMatch(itemId, displayName, score));
            }

            BrowserSearchCurrentMatches.Sort(delegate(BrowserSearchMatch a, BrowserSearchMatch b)
            {
                int scoreCompare = a.Score.CompareTo(b.Score);
                if (scoreCompare != 0) return scoreCompare;
                int nameCompare = string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
                if (nameCompare != 0) return nameCompare;
                return string.Compare(a.ItemId, b.ItemId, StringComparison.OrdinalIgnoreCase);
            });

            _browserSearchLastResultCount = BrowserSearchCurrentMatches.Count;
            _browserSearchLastResultRevision = _browserSearchIndexRevision;
            _browserSearchLastResultLanguage = _browserSearchIndexLanguage;
            _browserSearchLastRefreshFrame = Time.frameCount;
            RenderBrowserSearchCurrentPage();
        }

        private static void ScrollBrowserSearchRows(int delta)
        {
            if (delta == 0 || BrowserSearchCurrentMatches.Count == 0) return;
            int maxOffset = Math.Max(0, BrowserSearchCurrentMatches.Count - BrowserSearchVisibleRows);
            int next = Mathf.Clamp(_browserSearchScrollOffset + delta, 0, maxOffset);
            if (next == _browserSearchScrollOffset) return;
            _browserSearchScrollOffset = next;
            RenderBrowserSearchCurrentPage();
        }

        private static void SubmitBrowserSearch(string rawQuery)
        {
            if (string.IsNullOrEmpty(rawQuery)) return;
            RefreshBrowserSearchSuggestions(rawQuery);
            if (!string.IsNullOrEmpty(BrowserSearchRowItemIds[0])) SelectBrowserSearchItem(BrowserSearchRowItemIds[0]);
        }

        private static void SelectBrowserSearchItem(string itemId)
        {
            if (!_inspectorOpen || string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) return;
            if (_browserSearchInput != null)
            {
                _browserSearchSuppressEvents = true;
                _browserSearchInput.text = string.Empty;
                _browserSearchSuppressEvents = false;
                _browserSearchInput.DeactivateInputField();
            }
            HideBrowserSearchDropdown();
            CloseBrowserCatalog();
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            NavigateBrowserToItem(itemId, false, "Browser search");
        }

        private static void HideBrowserSearchDropdown()
        {
            if (_browserSearchDropdown != null) _browserSearchDropdown.SetActive(false);
            for (int i = 0; i < BrowserSearchVisibleRows; i++) BrowserSearchRowItemIds[i] = string.Empty;
        }

        private static void ClearBrowserSearchField()
        {
            if (_browserSearchInput != null)
            {
                _browserSearchSuppressEvents = true;
                _browserSearchInput.text = string.Empty;
                _browserSearchSuppressEvents = false;
            }
            _browserSearchLastResultCount = 0;
            BrowserSearchCurrentMatches.Clear();
            _browserSearchScrollOffset = 0;
            _browserSearchLastNormalizedQuery = string.Empty;
            HideBrowserSearchDropdown();
            UpdateBrowserSearchStatus();
        }
    }
}
