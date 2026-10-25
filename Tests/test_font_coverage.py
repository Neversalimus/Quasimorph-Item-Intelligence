import unittest
from font_coverage import find_missing, required_characters


class FontCoverageTests(unittest.TestCase):
    def test_missing_punctuation_has_source_key(self):
        self.assertEqual(find_missing({'note': 'A≈B'}, {ord('A'), ord('B')}), {'U+2248': ['note']})

    def test_whitespace_is_layout(self):
        self.assertEqual(required_characters('A\n\t\u00a0 '), {65})

    def test_combining_marks_are_checked(self):
        self.assertEqual(find_missing({'label': 'e\u0301'}, {ord('e')}), {'U+0301': ['label']})

    def test_supplementary_codepoints_are_not_split(self):
        self.assertEqual(find_missing({'label': '😀'}, set()), {'U+1F600': ['label']})

    def test_all_affected_keys_are_reported_once(self):
        self.assertEqual(find_missing({'a': '××', 'b': '×'}, set()), {'U+00D7': ['a', 'b']})

    def test_supported_text_passes(self):
        self.assertEqual(find_missing({'label': '語한'}, {ord('語'), ord('한')}), {})


if __name__ == '__main__':
    unittest.main()
