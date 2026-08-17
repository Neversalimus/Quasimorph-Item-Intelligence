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
            int fallback = IsRussian() ? russianFallbackChars : englishFallbackChars;
            List<string> lines = WrapBrowserFullWidthText(Ui(localizationKey), fallback);
            for (int i = 0; i < lines.Count; i++)
                BrowserLines.Add(BrowserLine.FullNote(lines[i]));
        }

        private static List<string> WrapBrowserFullWidthText(string value, int fallbackMaxChars)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            fallbackMaxChars = Math.Max(36, fallbackMaxChars);

            TMP_Text measure = BrowserRowLeft != null && BrowserRowLeft.Length > 0
                ? BrowserRowLeft[0] : null;
            float oldFontSize = 0f;
            if (measure != null)
            {
                oldFontSize = measure.fontSize;
                measure.fontSize = 12.5f;
            }

            try
            {
                string[] words = value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder line = new StringBuilder();
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    string candidate = line.Length == 0 ? word : line.ToString() + " " + word;
                    bool tooWide = measure != null
                        ? measure.GetPreferredValues(candidate).x > 678f
                        : candidate.Length > fallbackMaxChars;
                    if (line.Length > 0 && tooWide)
                    {
                        result.Add(line.ToString());
                        line.Length = 0;
                    }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                }
                if (line.Length > 0) result.Add(line.ToString());
            }
            finally
            {
                if (measure != null) measure.fontSize = oldFontSize;
            }
            return result;
        }
    }
}
