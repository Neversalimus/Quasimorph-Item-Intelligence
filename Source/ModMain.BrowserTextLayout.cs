using System;
using System.Collections.Generic;
using System.Text;
using TMPro;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Full-width browser notes still use the fixed 39px row pool. Split them into
        // rows before rendering, but use the actual TMP font metrics when the browser
        // exists instead of guessing line width from character count.
        private static void AddWrappedBrowserNote(
            string localizationKey, int russianFallbackChars, int englishFallbackChars)
        {
            int fallback = GetLanguageAwareWrapFallback(russianFallbackChars, englishFallbackChars);
            List<string> lines = WrapBrowserFullWidthText(Ui(localizationKey), fallback);
            for (int i = 0; i < lines.Count; i++)
                BrowserLines.Add(BrowserLine.FullNote(lines[i]));
        }

        // Related notes are emitted consecutively. v1.7.40-test7 removed page padding:
        // the virtualized row scroller can stop at any row, so blank filler rows would
        // only create dead space and make long Loot explanations harder to scan.
        private static void AddWrappedBrowserNoteGroup(
            int russianFallbackChars, int englishFallbackChars, params string[] localizationKeys)
        {
            int fallback = GetLanguageAwareWrapFallback(russianFallbackChars, englishFallbackChars);
            if (localizationKeys == null) return;
            for (int i = 0; i < localizationKeys.Length; i++)
            {
                List<string> lines = WrapBrowserFullWidthText(Ui(localizationKeys[i]), fallback);
                for (int j = 0; j < lines.Count; j++)
                    BrowserLines.Add(BrowserLine.FullNote(lines[j]));
            }
        }

        private static void WrapBrowserNoteRows()
        {
            // Some short/conditional notes are added directly by their feature.
            // Wrap every FullNote once after rebuilding the page, before calculating
            // scroll bounds. Scrolling pooled rows never changes the source list.
            int fallback = GetLanguageAwareWrapFallback(100, 115);
            for (int i = BrowserLines.Count - 1; i >= 0; i--)
            {
                BrowserLine note = BrowserLines[i];
                if (note.RowKind != BrowserRowKind.FullNote || string.IsNullOrWhiteSpace(note.Left)) continue;
                List<string> wrapped = WrapBrowserFullWidthText(NormalizeModUiText(note.Left), fallback);
                if (wrapped.Count == 1 && string.Equals(wrapped[0], note.Left, StringComparison.Ordinal)) continue;
                BrowserLines.RemoveAt(i);
                for (int j = wrapped.Count - 1; j >= 0; j--)
                    BrowserLines.Insert(i, BrowserLine.FullNote(wrapped[j]));
            }
        }

        private static List<string> WrapBrowserFullWidthText(string value, int fallbackMaxChars)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            // Legacy fallback budgets were authored for 10.5-unit notes. Keep the
            // no-font path conservative as the chosen profile increases text size.
            fallbackMaxChars = Math.Max(24, (int)Math.Floor(Math.Max(36, fallbackMaxChars) * 10.5f / BrowserNoteFontSize));

            TMP_Text measure = BrowserRowLeft != null && BrowserRowLeft.Length > 0
                ? BrowserRowLeft[0] : null;
            float oldFontSize = 0f;
            bool oldWordWrapping = false;
            bool oldAutoSizing = false;
            FontStyles oldFontStyle = FontStyles.Normal;
            if (measure != null)
            {
                oldFontSize = measure.fontSize;
                oldWordWrapping = measure.enableWordWrapping;
                oldAutoSizing = measure.enableAutoSizing;
                oldFontStyle = measure.fontStyle;
                // Use the same fixed size and upright style as the rendered note.
                // The measuring slot may currently hold a bold section/table row.
                measure.enableAutoSizing = false;
                measure.fontStyle = FontStyles.Normal;
                measure.fontSize = BrowserNoteFontSize;
                measure.enableWordWrapping = false;
            }

            try
            {
                float fullWidthWrapLimit = BrowserColumnCoordinate(BrowserFullNoteWidth, _browserViewport.Width) - 4f;
                if (ContainsHanScript(value) || ContainsKanaScript(value) || ContainsHangulScript(value))
                    return WrapUnspacedBrowserText(value, delegate(string candidate)
                    { return IsBrowserFullWidthTextTooWide(measure, candidate, fullWidthWrapLimit, fallbackMaxChars); });
                List<string> sentences = SplitBrowserNoteSentences(value);
                StringBuilder line = new StringBuilder();

                for (int s = 0; s < sentences.Count; s++)
                {
                    string sentence = sentences[s];
                    if (string.IsNullOrWhiteSpace(sentence)) continue;

                    string sentenceCandidate = line.Length == 0
                        ? sentence
                        : line.ToString() + " " + sentence;
                    if (!IsBrowserFullWidthTextTooWide(
                            measure, sentenceCandidate, fullWidthWrapLimit, fallbackMaxChars))
                    {
                        if (line.Length > 0) line.Append(' ');
                        line.Append(sentence);
                        continue;
                    }

                    // Prefer a sentence boundary when the current row already contains
                    // text. Only split inside a sentence when that sentence cannot fit on
                    // an otherwise empty full-width row.
                    if (line.Length > 0)
                    {
                        result.Add(line.ToString());
                        line.Length = 0;
                    }

                    string[] words = sentence.Split(
                        new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < words.Length; i++)
                    {
                        string word = words[i];
                        string candidate = line.Length == 0
                            ? word
                            : line.ToString() + " " + word;
                        bool tooWide = IsBrowserFullWidthTextTooWide(
                            measure, candidate, fullWidthWrapLimit, fallbackMaxChars);
                        if (line.Length > 0 && tooWide)
                        {
                            result.Add(line.ToString());
                            line.Length = 0;
                        }
                        if (line.Length > 0) line.Append(' ');
                        if (IsBrowserFullWidthTextTooWide(measure, word, fullWidthWrapLimit, fallbackMaxChars))
                        {
                            List<string> pieces = WrapUnspacedBrowserText(word, delegate(string part)
                            { return IsBrowserFullWidthTextTooWide(measure, part, fullWidthWrapLimit, fallbackMaxChars); });
                            for (int p = 0; p < pieces.Count - 1; p++) result.Add(pieces[p]);
                            if (pieces.Count > 0) line.Append(pieces[pieces.Count - 1]);
                        }
                        else line.Append(word);
                    }
                }

                if (line.Length > 0) result.Add(line.ToString());
            }
            finally
            {
                if (measure != null)
                {
                    measure.fontSize = oldFontSize;
                    measure.enableWordWrapping = oldWordWrapping;
                    measure.fontStyle = oldFontStyle;
                    measure.enableAutoSizing = oldAutoSizing;
                }
            }
            return result;
        }

        private static bool IsBrowserFullWidthTextTooWide(
            TMP_Text measure, string candidate, float fullWidthWrapLimit, int fallbackMaxChars)
        {
            return measure != null
                ? measure.GetPreferredValues(candidate, 4096f, 0f).x > fullWidthWrapLimit
                : candidate.Length > fallbackMaxChars;
        }

        private static List<string> SplitBrowserNoteSentences(string value)
        {
            List<string> result = new List<string>();
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c != '.' && c != '!' && c != '?') ||
                    (i + 1 < value.Length && !char.IsWhiteSpace(value[i + 1])))
                    continue;
                string sentence = value.Substring(start, i - start + 1).Trim();
                if (sentence.Length > 0) result.Add(sentence);
                start = i + 1;
            }
            string tail = value.Substring(start).Trim();
            if (tail.Length > 0) result.Add(tail);
            return result;
        }
    }
}
