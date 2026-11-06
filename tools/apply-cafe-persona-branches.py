"""Generate independent persona branches without editing any parent record or reply.

Run after source import/knowledge regeneration. Safe to repeat; no Unity or model execution.
"""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'ai-hack-test/Assets/CafeSim'


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


def write(path, data):
    (ROOT / path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    definition = ROOT / 'Data/PersonaBranches.json'
    if not definition.exists():
        return
    branches = read('Data/PersonaBranches.json')['items']
    ids = {b['persona']['id'] for b in branches}
    people = read('Data/personas.json')
    people['items'] = [p for p in people['items'] if p['id'] not in ids]
    parents = {p['id']: p for p in people['items']}
    for b in branches:
        p = copy.deepcopy(parents[b['persona']['parentPersonaId']])
        p.update(b['persona'])
        b['resolved'] = p
        people['items'].append(p)
    write('Data/personas.json', people)

    kb = 'Resources/CafeKnowledge/'
    tables = {name: read(kb + name + '.json') for name in
              ['profiles', 'terms', 'bindings', 'orderWeights', 'situations', 'conversation', 'NewMenuRequests']}
    for name, table in tables.items():
        if name == 'terms':
            table['items'] = [r for r in table['items'] if not r['id'].startswith('branch_')]
        else:
            table['items'] = [r for r in table['items'] if r.get('personaId') not in ids]
    conv = tables['conversation']
    for section in ['endings', 'replyVocabulary']:
        conv[section] = [r for r in conv.get(section, []) if r['personaId'] not in ids]
    collection = read('Resources/CafeCollection/collection.json')
    collection['customers'] = [r for r in collection['customers'] if r['id'] not in ids]
    collection['phrases'] = [r for r in collection['phrases'] if r['customer'] not in ids]

    for b in branches:
        p = b['resolved']
        pid, parent = p['id'], p['parentPersonaId']
        tables['profiles']['items'].append(dict(personaId=pid, lexiconId=pid))
        for row in list(tables['orderWeights']['items']):
            if row['personaId'] == parent:
                tables['orderWeights']['items'].append(dict(row, personaId=pid))
        # Same timing/flag contract as the parent; entirely separate authored text and term IDs.
        for i, row in enumerate([r for r in tables['bindings']['items'] if r['personaId'] == parent]):
            slot = row['slot']
            if slot == 'chatter':
                band = 0 if row['minWait'] == 0 else 1 if row['minWait'] == 60 else 2
                text = b['chatter'][band + (3 if 'active' in row['requires'] else 0)]
            else:
                text = b['slots'][slot]
            tid = f'branch_{pid}_{i}'
            tables['terms']['items'].append(dict(id=tid, text=text))
            tables['bindings']['items'].append(dict(row, personaId=pid, termId=tid))
            if slot == 'chatter':
                common = dict(personaId=pid, anchorTermId=tid, anchor=text,
                              words=p['smalltalkTopics'].replace(',', ' '), voice=p['voice'],
                              requires=row['requires'], minWait=row['minWait'], maxWait=row['maxWait'])
                conv['items'].append(dict(common, id=tid, queries=[], response=text, direct=False))
                if row['minWait'] == 0:
                    conv['items'].append(dict(common, id=tid + '_followup',
                        queries=['왜요', '좀 더 이야기해 주세요', '더 자세히 들려주세요', '무슨 생각 하세요'],
                        response=b['followup'], direct=True))
        for i, context in enumerate(['잠깐 쉬러 들른 날', '다음 일정을 준비하는 날']):
            spoken = ('노래 연습을 마치고 잠깐 쉬러 왔어요.' if pid == 'cus_miku' else '회원 수업을 마치고 잠깐 쉬러 왔습니다.') if i == 0 else (
                '다음 작은 무대에서 부를 노래를 고르는 중이에요.' if pid == 'cus_miku' else '다음 수업 전에 회복하는 시간을 챙기고 있습니다.')
            tables['situations']['items'].append(dict(id=f'sit_{pid}_{i}', personaId=pid,
                context=context, spokenContext=spoken, effect='기존 주문 흐름 유지',
                weightRule='변화 없음', hotDelta=0, patienceScale=1))
        greeting = '안녕하세요. 오늘은 어떤 향으로 쉬어 갈지 기대돼요.' if pid == 'cus_miku' else '반갑습니다. 오늘도 잠깐 쉬어 가겠습니다.'
        conv['items'].append(dict(id=pid + '_hello', personaId=pid, anchorTermId='', anchor='',
            queries=['안녕하세요', '안녕', '반가워요'], words='인사 안녕 반갑', voice=p['voice'],
            response=greeting, direct=True, requires=[], minWait=0, maxWait=100000))
        conv['endings'].append(dict(personaId=pid, speech='이야기 즐거웠어요. 다음에 또 만나요.' if pid == 'cus_miku' else '이야기 잘 나눴습니다. 다음에 또 뵙죠.'))
        conv['replyVocabulary'].append(dict(personaId=pid, acknowledgements=['아, 그렇군요.', '말씀 잘 들었어요.'],
            clarifications=['어떤 뜻인지 조금 더 이야기해 주실래요?'], greetings=[greeting], topicQuestion='{topic}에 대해 어떤 점이 궁금하세요?'))
        for row in list(tables['NewMenuRequests']['items']):
            if row['personaId'] == parent:
                tables['NewMenuRequests']['items'].append(dict(row, personaId=pid,
                    voice=p['voice'], speech=row['name'] + b['slots']['ending']))
        collection['customers'].append(dict(id=pid, name=p['name'], group='분기', tag='기타',
            style=p['communicationPoint'] + ' · 선택 분기', greeting=greeting, optional=True))
        phrases = [
            ('후렴처럼 은은하게 달게', '후렴처럼 은은한 단맛이면 좋겠어요.', ['sweet'], '은은하게 달게'),
            ('첫 음처럼 또렷하게', '첫 음처럼 쓴맛이 또렷하면 좋겠어요.', ['bitter'], '쓴맛이 또렷하게'),
            ('화음처럼 부드럽게', '우유가 화음처럼 부드럽게 받쳐 주면 좋겠어요.', ['milk'], '우유를 넣어 부드럽게'),
            ('앙코르처럼 따뜻하게', '앙코르처럼 따뜻하게 남는 잔이면 좋겠어요.', ['hot'], '따뜻하게'),
            ('산뜻한 박자로 차갑게', '산뜻한 박자처럼 차갑게 부탁드려요.', ['ice'], '차갑게')
        ] if pid == 'cus_miku' else [
            ('가볍게 단맛', '오늘은 단맛을 가볍게 챙겨 보죠.', ['sweet'], '가벼운 단맛'),
            ('또렷한 쓴맛', '쓴맛이 또렷한 쪽으로 부탁드립니다.', ['bitter'], '또렷하게 쓴 맛'),
            ('회복하듯 부드럽게', '우유가 들어가 부드러운 쪽이면 좋겠습니다.', ['milk'], '우유를 넣어 부드럽게'),
            ('쉬는 시간처럼 따뜻하게', '쉬는 시간처럼 따뜻한 잔으로 부탁드립니다.', ['hot'], '따뜻하게'),
            ('운동 뒤처럼 시원하게', '운동 뒤처럼 시원한 잔이면 좋겠습니다.', ['ice'], '차갑게')
        ]
        collection['phrases'].extend(dict(customer=pid, text=t, line=line, attrs=attrs, meaning=meaning)
                                     for t, line, attrs, meaning in phrases)
    for name, table in tables.items():
        write(kb + name + '.json', table)
    write('Resources/CafeCollection/collection.json', collection)
    vocabulary = read('Resources/CafeVocabulary.json')
    vocabulary['items'] = [r for r in vocabulary['items'] if r['id'] not in ids]
    chatter = read('Data/ChatterLexicon.json')
    chatter['items'] = [r for r in chatter['items'] if r['personaId'] not in ids]
    for b in branches:
        pid = b['persona']['id']
        vocabulary['items'].append(dict(id=pid, **{
            field: [b['slots'][slot]] for field, slot in
            [('orderOpen', 'opener'), ('orderClose', 'ending'), ('matched', 'match'),
             ('correction', 'wrong'), ('ratingGood', 'good'), ('ratingBad', 'bad')]}))
        chatter['items'].append(dict(personaId=pid, inactive=b['chatter'][:3], active=b['chatter'][3:]))
    write('Resources/CafeVocabulary.json', vocabulary)
    write('Data/ChatterLexicon.json', chatter)
    print(f'Applied {len(branches)} independent branches; {len(parents)} original personas preserved')


if __name__ == '__main__':
    main()
    import runpy
    runpy.run_path(str(Path(__file__).with_name('apply-cafe-appearance-profiles.py')), run_name='__main__')
