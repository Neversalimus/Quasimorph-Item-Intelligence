"""Read local TMP atlases and validate every shipped translation. No asset export."""
import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import sys


def required_characters(text):
    # Whitespace is layout, and may not have a glyph-table entry. Do not discard
    # punctuation, combining marks or supplementary code points.
    return {ord(char) for char in text if not char.isspace()}


def find_missing(rows, supported):
    missing = {}
    for key, value in rows.items():
        for code in sorted(required_characters(value) - supported):
            missing.setdefault(f"U+{code:04X}", []).append(key)
    return missing


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--catalog', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    import UnityPy
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

    data = args.game_root / 'Quasimorph_Data'
    assets = [data / 'resources.assets', *sorted(data.glob('sharedassets*.assets'))]
    if any(not path.is_file() for path in assets):
        raise RuntimeError('Game asset files unavailable')
    env = UnityPy.load(*(str(path) for path in assets))
    unity_version = next(iter(env.files.values())).unity_version
    env.typetree_generator = TypeTreeGenerator(unity_version)
    env.typetree_generator.load_local_game(str(args.game_root))
    keepers = []
    for obj in env.objects:
        if obj.type.name != 'MonoBehaviour':
            continue
        head = obj.parse_monobehaviour_head()
        if head.m_Script.read().m_ClassName == 'LocalizationFontKeeper':
            keepers.append(obj)
    if len(keepers) != 1:
        raise RuntimeError(f'Expected one LocalizationFontKeeper, found {len(keepers)}')

    def dereference(owner, pointer):
        return UnityPy.classes.PPtr(m_FileID=pointer['m_FileID'],
                                    m_PathID=pointer['m_PathID'], assetsfile=owner.assets_file).deref()

    def characters(font, seen):
        identity = (font.assets_file.name, font.path_id)
        if identity in seen:
            return set()
        seen.add(identity)
        tree = font.read_typetree()
        result = {entry['m_Unicode'] for entry in tree['m_CharacterTable']}
        # Dynamic source fonts are not assumed to cover characters absent from
        # their current atlas. Missing glyphs therefore remain a visible failure.
        for pointer in tree.get('m_FallbackFontAssetTable') or []:
            if pointer['m_PathID']:
                result |= characters(dereference(font, pointer), seen)
        return result

    keeper = keepers[0]
    fonts = {}
    for pointer in keeper.read_typetree()['_fontPresets']:
        preset = dereference(keeper, pointer)
        tree = preset.read_typetree()
        font = dereference(preset, tree['_font'])
        font_tree = font.read_typetree()
        coverage = characters(font, set())
        for language in tree['_availableLangs']:
            if language in fonts:
                raise RuntimeError(f'Ambiguous font preset for game language {language}')
            fonts[language] = (font_tree['m_Name'], coverage)

    catalog = json.loads(args.catalog.read_text(encoding='utf-8-sig'))
    report = {
        'game_assembly_sha256': catalog['game_assembly_sha256'],
        'unity_version': unity_version,
        'dependencies': {name: importlib.metadata.version(name) for name in ('UnityPy', 'TypeTreeGeneratorAPI')},
        'scope': 'Shipped translation glyphs in selected native TMP font and its explicit fallbacks; not visual layout QA.',
        'languages': [],
    }
    for language in catalog['languages']:
        path = args.source_root / 'WORKSHOP_CONTENT' / 'Localization' / language['file']
        rows = {}
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            if not line.strip() or line.lstrip().startswith(('#', '@')):
                continue
            key, value = line.split('\t', 1)
            if key in rows:
                raise RuntimeError(f'Duplicate key in {path.name}: {key}')
            rows[key] = value
        name, supported = fonts[language['game_value']]
        missing = find_missing(rows, supported)
        report['languages'].append(dict(language, font=name, entries=len(rows), missing=missing,
                                       translation_sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        print(f"{language['id']}: {len(rows)} entries; {len(missing)} missing characters; {name}")
    report['passed'] = all(not language['missing'] for language in report['languages'])
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
