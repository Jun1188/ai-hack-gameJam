"""Read the supplied workbook without modifying it; only menu rows are game data."""
import json
from pathlib import Path
import sys
import openpyxl

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1])
sheet = openpyxl.load_workbook(source, data_only=True).worksheets[0]
items = []
for row in sheet.iter_rows(min_row=5, values_only=True):
    if not isinstance(row[0], str) or not row[0].startswith('menu_'):
        continue
    items.append(dict(id=row[0], name=row[1], category=row[2], hot=row[3] == 'O',
                      ice=row[4] == 'O', recipe=row[6], description=row[9]))
assert len(items) == 11 and len({m['id'] for m in items}) == 11
target = root / 'ai-hack-test/Assets/CafeSim/Data/menu.json'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps({'items': items}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Imported {len(items)} menus; HOT={sum(m["hot"] for m in items)}, ICE={sum(m["ice"] for m in items)}')
