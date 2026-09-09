using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // CJK notes have no word-separating spaces. Split at text-element boundaries,
        // retaining the original characters and avoiding isolated closing punctuation.
        private static List<string> WrapUnspacedBrowserText(string value, Func<string, bool> tooWide)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(value)) return lines;
            List<string> elements = new List<string>();
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext()) elements.Add(enumerator.GetTextElement());
            int start = 0;
            while (start < elements.Count)
            {
                int end = start;
                string line = string.Empty;
                while (end < elements.Count)
                {
                    string candidate = line + elements[end];
                    if (end > start && tooWide(candidate)) break;
                    line = candidate;
                    end++;
                }
                if (end < elements.Count && end - start > 1)
                {
                    const string closing = "，。！？；：、）】》」』,.!?;:%";
                    const string opening = "（【《「『(";
                    if (closing.IndexOf(elements[end], StringComparison.Ordinal) >= 0 ||
                        opening.IndexOf(elements[end - 1], StringComparison.Ordinal) >= 0)
                    {
                        end--;
                        line = line.Substring(0, line.Length - elements[end].Length);
                    }
                }
                lines.Add(line);
                start = end;
            }
            return lines;
        }
    }
}
