using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Central geometry/text-fit policy for pooled browser rows.
    /// Specialized renderers declare semantics; this owner decides widths and fit behavior.
    /// </summary>
    public static partial class ModMain
    {
        private const float BrowserContentLeft = 10f;
        private const float BrowserContentWidth = 688f;
        private const float BrowserFullNoteWidth = 698f;
        private const float BrowserRightColumnWidth = 194f;
        private const float BrowserRightColumnX = 494f;

        private static void ResetBrowserRowTextFit(TMP_Text left, TMP_Text right)
        {
            if (left != null)
            {
                SetBrowserAutoSizingIfChanged(left, false);
                SetBrowserFontSizeMinIfChanged(left, 12f);
                SetBrowserFontSizeMaxIfChanged(left, 18f);
                SetBrowserWordWrappingIfChanged(left, false);
                SetBrowserOverflowIfChanged(left, TextOverflowModes.Ellipsis);
            }
            if (right != null)
            {
                SetBrowserAutoSizingIfChanged(right, false);
                SetBrowserFontSizeMinIfChanged(right, 11.5f);
                SetBrowserFontSizeMaxIfChanged(right, 16f);
                SetBrowserWordWrappingIfChanged(right, false);
                SetBrowserOverflowIfChanged(right, TextOverflowModes.Ellipsis);
            }
        }

        private static void ApplyBrowserStandardRowLayout(
            RectTransform leftRt, RectTransform rightRt, bool showIcon,
            bool showRecipeContext, bool showChipUnlockStatus, bool needsRightColumn)
        {
            if (rightRt != null)
            {
                SetBrowserRectPositionIfChanged(rightRt, BrowserRightColumnX, 0f);
                SetBrowserRectSizeIfChanged(rightRt, needsRightColumn ? BrowserRightColumnWidth : 0f, rightRt.sizeDelta.y);
            }

            if (showRecipeContext)
            {
                if (rightRt != null)
                {
                    SetBrowserRectPositionIfChanged(rightRt, 436f, 0f);
                    SetBrowserRectSizeIfChanged(rightRt, needsRightColumn ? 252f : 0f, rightRt.sizeDelta.y);
                }
                if (leftRt != null)
                {
                    SetBrowserRectPositionIfChanged(leftRt, 79f, 0f);
                    SetBrowserRectSizeIfChanged(leftRt, needsRightColumn ? 351f : 609f, leftRt.sizeDelta.y);
                }
                return;
            }
            if (showChipUnlockStatus)
            {
                if (leftRt != null)
                {
                    SetBrowserRectPositionIfChanged(leftRt, 56f, 0f);
                    SetBrowserRectSizeIfChanged(leftRt, needsRightColumn ? 368f : 632f, leftRt.sizeDelta.y);
                }
                return;
            }

            if (leftRt != null)
            {
                float x = showIcon ? 36f : BrowserContentLeft;
                float rightEdge = needsRightColumn ? BrowserRightColumnX - 6f : BrowserContentLeft + BrowserContentWidth;
                SetBrowserRectPositionIfChanged(leftRt, x, 0f);
                SetBrowserRectSizeIfChanged(leftRt, Mathf.Max(80f, rightEdge - x), leftRt.sizeDelta.y);
            }
        }

        private static void ApplyBrowserFullWidthRow(
            TMP_Text left, TMP_Text right, RectTransform leftRt,
            float fontSize, float minFontSize, bool autoSize)
        {
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, BrowserContentLeft, 0f);
                SetBrowserRectSizeIfChanged(leftRt, BrowserFullNoteWidth, leftRt.sizeDelta.y);
            }
            if (left != null)
            {
                SetBrowserWordWrappingIfChanged(left, false);
                SetBrowserOverflowIfChanged(left, TextOverflowModes.Ellipsis);
                SetBrowserAutoSizingIfChanged(left, autoSize);
                SetBrowserFontSizeMinIfChanged(left, minFontSize);
                SetBrowserFontSizeMaxIfChanged(left, fontSize);
                SetBrowserFontSizeIfChanged(left, fontSize);
            }
        }

        private static void ApplyBrowserCombatColumns(
            RectTransform leftRt, bool icon, TMP_Text right,
            TMP_Text normal, TMP_Text crit,
            string normalValue, string critValue, bool header)
        {
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, !header && icon ? 36f : 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, 450f, leftRt.sizeDelta.y);
            }
            ConfigureLootColumn(normal, 460f, 112f, normalValue, header ? 12.5f : 14.5f);
            ConfigureLootColumn(crit, 572f, 116f, critValue, header ? 12.5f : 14.5f);
        }

        private static void ApplyBrowserBaronColumns(
            RectTransform leftRt, TMP_Text right,
            TMP_Text itemChance, TMP_Text pactChance,
            string itemChanceValue, string pactChanceValue, bool header)
        {
            if (right != null) SetBrowserActiveIfChanged(right.gameObject, false);
            if (leftRt != null)
            {
                SetBrowserRectPositionIfChanged(leftRt, 10f, 0f);
                SetBrowserRectSizeIfChanged(leftRt, 420f, leftRt.sizeDelta.y);
            }
            ConfigureLootColumn(itemChance, 430f, 140f, itemChanceValue, header ? 12.5f : 14.5f);
            ConfigureLootColumn(pactChance, 570f, 118f, pactChanceValue, header ? 12.5f : 14.5f);
        }
    }
}
