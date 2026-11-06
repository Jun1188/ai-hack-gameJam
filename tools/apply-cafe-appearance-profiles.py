"""Reapply approved appearance fields after source import; never alters the source workbook."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'ai-hack-test/Assets/CafeSim/Data'


def main():
    path = ROOT / 'personas.json'
    table = json.loads(path.read_text(encoding='utf-8'))
    profiles = json.loads((ROOT / 'CustomerAppearanceProfiles.json').read_text(encoding='utf-8'))
    people = {p['id']: p for p in table['items']}
    allowed = {'id', 'gender', 'ageDescription', 'modelBase', 'hair', 'top', 'bottom', 'accessory', 'setting', 'taste', 'twist', 'smalltalkTopics'}
    for profile in profiles['items']:
        if not set(profile) <= allowed:
            raise ValueError('Appearance profiles may only change appearance fields')
        people[profile['id']].update(profile)
    path.write_text(json.dumps(table, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # Gyeongsang is now a young man; retain the family conversation without implying grandchildren.
    for name in ['conversation', 'situations']:
        knowledge_path = ROOT.parent / 'Resources/CafeKnowledge' / (name + '.json')
        knowledge = json.loads(knowledge_path.read_text(encoding='utf-8'))
        changed = False
        for row in knowledge.get('items', []):
            if row.get('personaId') != 'cus_gyeongsang':
                continue
            for key, value in row.items():
                if isinstance(value, str) and '손주' in value:
                    row[key] = value.replace('손주 자랑', '가족 이야기').replace('손주', '가족')
                    changed = True
        if changed:
            knowledge_path.write_text(json.dumps(knowledge, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
