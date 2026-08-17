using System;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.37-test3: WeaponModeDescriptor is built during the Ammo warmup and must
        // never own a player-facing localized label. The active game/browser language
        // can change later without rebuilding weapon indexes, so labels are projected
        // from raw mode identity at render time, just like other language-safe indexes.
        private static string ResolveWeaponModeDisplayLabel(WeaponModeDescriptor mode)
        {
            if (mode == null) return string.Empty;

            EnsureLocalizationCacheLanguage();
            string rawId = mode.RawId ?? string.Empty;

            // Prefer vanilla localization in the CURRENT language. Do not reuse the
            // descriptor's warmup-time text unless it matches the active RU/EN script.
            if (!string.IsNullOrEmpty(rawId))
            {
                string localized = LocalizeCandidates(new string[]
                {
                    "firemode." + rawId + ".name",
                    "fire_mode." + rawId + ".name",
                    "weapon.firemode." + rawId + ".name",
                    "attackmode." + rawId + ".name",
                    rawId
                }, rawId);
                if (!string.IsNullOrEmpty(localized) &&
                    !string.Equals(localized, rawId, StringComparison.OrdinalIgnoreCase) &&
                    IsWeaponModeLabelCompatibleWithCurrentLanguage(localized))
                    return NormalizeGameText(localized);
            }

            if (!string.IsNullOrEmpty(mode.Label) &&
                IsWeaponModeLabelCompatibleWithCurrentLanguage(mode.Label))
                return NormalizeGameText(mode.Label);

            string fallback = BuildWeaponModeLanguageSafeFallback(rawId, mode.Stats);
            if (!string.IsNullOrEmpty(fallback)) return fallback;

            if (!string.IsNullOrEmpty(rawId) && IsWeaponModeLabelCompatibleWithCurrentLanguage(rawId))
                return HumanizeModeIdentifier(rawId);

            // Last-resort fallback must also be authored through Ui(...): a malformed or
            // modded mode node can expose a localized Name as its "id", and echoing that
            // value would reintroduce the exact RU/EN leakage fixed by test3.
            if (mode.Stats != null && mode.Stats.AmmoPerShot <= 0 && mode.Stats.WeaponCastsCount <= 0)
                return Ui("ui.mode_melee");
            return Ui("ui.mode_single");
        }

        private static string BuildWeaponModeLanguageSafeFallback(string rawId, WeaponModeStaticStats stats)
        {
            string normalized = (rawId ?? string.Empty).Trim();
            string lower = normalized.ToLowerInvariant();

            if (lower.Contains("stock") || lower.Contains("butt") || lower.Contains("bash"))
                return Ui("ui.mode_buttstroke");

            bool meleeFamily = lower.Contains("melee") || lower.Contains("claw") || lower.Contains("slash") ||
                               lower.Contains("stab") || lower.Contains("chop") || lower.Contains("smash") ||
                               lower.Contains("punch") || lower.Contains("strike") || lower.Contains("breaker");
            if (meleeFamily)
            {
                string family = normalized;
                int underscore = family.IndexOf('_');
                if (underscore > 0) family = family.Substring(0, underscore);
                string familyLower = family.ToLowerInvariant();
                if (familyLower == "stab") return Ui("ui.mode_stab");
                if (familyLower == "slash") return Ui("ui.mode_slash");
                if (familyLower == "chop") return Ui("ui.mode_chop");
                if (familyLower == "smash") return Ui("ui.mode_smash");
                if (familyLower == "punch") return Ui("ui.mode_punch");
                return Ui("ui.mode_melee");
            }

            if (lower.Contains("auto")) return Ui("ui.mode_auto");
            if (lower.Contains("burst") || lower.Contains("volley")) return Ui("ui.mode_burst");
            if (lower.Contains("launcher")) return Ui("ui.mode_launcher");
            if (lower.Contains("throw")) return Ui("ui.mode_throw");

            int count = stats == null ? 0 : stats.WeaponCastsCount;
            if (count > 0 && count < 100)
                return count == 1
                    ? Ui("ui.mode_single_shot")
                    : Ui("ui.mode_shots") + " " + count.ToString(CultureInfo.InvariantCulture);

            return string.Empty;
        }

        private static bool IsWeaponModeLabelCompatibleWithCurrentLanguage(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            bool hasCyrillic = false;
            bool hasLatin = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) hasCyrillic = true;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) hasLatin = true;
            }

            if (IsEnglishLanguage()) return !hasCyrillic;
            if (IsRussian()) return hasCyrillic || !hasLatin;
            return true;
        }
    }
}
