"""Build persona/utterance -> follow-up reply mappings from the existing café DB.

No source spreadsheet edits, model calls, downloads or player dialogue persistence.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'ai-hack-test/Assets/CafeSim'
load = lambda p: json.loads((ROOT / p).read_text(encoding='utf-8'))['items']
people = load('Data/personas.json')
people = [p for p in people if not p.get('parentPersonaId')]
previous = ROOT / 'Resources/CafeKnowledge/conversation.json'
reply_vocabulary = json.loads(previous.read_text(encoding='utf-8')).get('replyVocabulary', []) if previous.exists() else []
reply_vocabulary = [r for r in reply_vocabulary if r['personaId'] in {p['id'] for p in people}]
terms = {t['id']: t['text'] for t in load('Resources/CafeKnowledge/terms.json')}
profiles = {p['personaId']: p['lexiconId'] for p in load('Resources/CafeKnowledge/profiles.json')}
bindings = load('Resources/CafeKnowledge/bindings.json')
# Authored follow-ups to each persona's existing opening chatter, in that persona's voice.
answers = {
    'cus_elder': '옛 다방에서는 커피 한 잔 놓고 이런저런 이야기를 오래 나눴지. 그 느긋한 시간이 문득 그리워지는구먼.',
    'cus_gyeongsang': '장 보고 오니 장바구니가 제법 무겁네예. 잠깐 내려놓고 쉬면서 이야기 좀 하려구예.',
    'cus_jeolla': '친구들이랑 둘러앉아 먹을 얘기 하는 게 즐겁당께요. 맛난 거 생각하면 기다리는 동안도 심심하지 않제라.',
    'cus_chungcheong': '농사짓다 보면 날씨 소식부터 보게 돼유. 기다리는 김에 비 소식이 있나 살펴보던 참이여유.',
    'cus_gangwon': '산에 두고 온 강아지가 생각나드래요. 돌아가면 반갑게 맞아줄 모습이 눈에 선하구먼요.',
    'cus_jeju': '제주에 있는 손주 생각을 하고 이서요. 바다 건너에 있으니 얼굴 보고 이야기할 때가 더 반갑주.',
    'cus_sea': '고향 가족에게 오늘 하루 어땠는지 말하고 싶어요. 한국말로도 조금씩 더 자연스럽게 이야기하고 싶고요.',
    'cus_west': '한국 여행하면서 배운 말을 직접 써 보는 게 재미있어요. 틀린 표현이 있으면 천천히 알려 주세요.',
    'cus_japan': '여행 중에 어디를 더 걸어볼지 생각하고 있어요. 천천히 둘러보면서 한국 음식도 맛보고 싶어요.',
    'cus_china': '학교 과제도 있고 한국 생활에서 배울 것도 많아요. 잠깐 쉬면서 이야기하면 기분 전환이 돼요.',
    'cus_child': '유치원에서 친구랑 캐릭터 그림을 그렸어요! 생각하니까 또 그리고 싶어요.',
    'cus_student': '시험 끝나고 듣고 싶은 노래들을 생각하고 있었어요. 공부하다가 좋아하는 노래 들으면 좀 힘이 나잖아요.',
    'cus_pangyo': '다음 회의 시간을 확인하던 중이에요. 잠깐이라도 숨 돌리고 들어가면 좋겠네요.',
    'cus_barista': '자격증 실습에서 배운 원두 향을 떠올리고 있었어요. 같은 커피도 향을 차근차근 살펴보는 게 재미있습니다.',
    'cus_trainer': '오늘 운동 기록이랑 쉬는 시간을 보고 있었어요! 운동만큼 쉬는 시간도 챙겨야 다음에 힘을 낼 수 있거든요.',
    'cus_blind': '산책할 때 들었던 음악과 익숙한 길을 떠올리고 있었어요. 그때 들리던 소리가 기억에 남네요.',
}
rows = []
for p in people:
    bs = [b for b in bindings if b['personaId'] == profiles[p['id']] and b['slot'] == 'chatter']
    for b in bs:
        # Every existing autonomous chatter line is mapped as a retrieval candidate.
        rows.append(dict(id=p['id']+'_'+b['termId'], personaId=p['id'], anchorTermId=b['termId'],
                         anchor=terms[b['termId']], queries=[], words=p['smalltalkTopics'].replace(',', ' '),
                         voice=p['voice'], response=terms[b['termId']], direct=False,
                         requires=b['requires'], minWait=b['minWait'], maxWait=b['maxWait']))
        if b['minWait'] == 0:
            rows.append(dict(id=p['id']+'_'+b['termId']+'_followup', personaId=p['id'], anchorTermId=b['termId'],
                             anchor=terms[b['termId']], queries=['무슨 생각 하세요', '무슨 이야기예요', '좀 더 이야기해 주세요',
                             '더 자세히 들려주세요', '어땠어요', '왜요', '그렇군요', '재밌겠네요'],
                             words=p['smalltalkTopics'].replace(',', ' '), voice=p['voice'],
                             response=answers[p['id']], direct=True, requires=b['requires'], minWait=0, maxWait=60))
    rows.append(dict(id=p['id']+'_hello', personaId=p['id'], anchorTermId='', anchor='',
                     queries=['안녕하세요', '안녕', '반가워요'], words='인사 안녕 반갑', voice=p['voice'],
                     response='안녕하세요. 잠깐 이야기 나누니 반갑네요.', direct=True,
                     requires=[], minWait=0, maxWait=100000))
target = ROOT / 'Resources/CafeKnowledge/conversation.json'
groups = [
    ['옛날', '예전', '과거', '추억', '옛이야기'], ['다방', '찻집'],
    ['이야기', '얘기', '말씀'], ['즐겁', '재밌', '재미'], ['고향', '본가'],
    ['노래', '음악', '플레이리스트'], ['멍멍이', '반려견', '강아지'],
    ['손자', '손녀', '손주'], ['공부', '학습'], ['일정', '스케줄'],
    ['안녕하세요', '안녕', '반가워요'], ['들려주세요', '말해주세요', '알려주세요'],
]
endings = [
    '허허, 그 이야기는 내가 잘 모르겠구먼. 이야기는 여기까지 하고 잠깐 쉬겠네.',
    '그거는 잘 모르겠네예. 이야기는 이쯤 하고 잠깐 쉬고 있을게예.',
    '그 얘기는 잘 모르겠당께요. 이야기는 이만하고 좀 쉬어야 쓰겄어요.',
    '그건 잘 모르겠어유. 이야기는 여기까지 하고 좀 쉴게유.',
    '그 이야기는 잘 모르겠드래요. 이야기는 이만하고 쉬겠어요.',
    '그 이야기는 잘 모르쿠다. 이만 이야기하고 쉬고 이시쿠다.',
    '그 이야기는 잘 모르겠어요. 이야기는 여기까지 하고 조금 쉴게요.',
    '그 이야기는 아직 잘 모르겠어요. 오늘 이야기는 여기서 마칠게요.',
    '그 이야기는 잘 모르겠네요. 이야기는 이만하고 잠깐 쉬겠습니다.',
    '그건 잘 모르겠어요. 이야기는 여기까지 하고 잠깐 쉬려고요.',
    '그건 잘 모르겠어요. 이제 이야기 그만하고 좀 쉴래요.',
    '그건 잘 모르겠는데요. 이야기는 여기까지 하고 잠깐 쉴게요.',
    '그 주제는 잘 모르겠네요. 대화는 이쯤 마무리하고 쉬겠습니다.',
    '그 이야기는 잘 모르겠습니다. 이야기는 여기까지 하고 잠시 쉬겠습니다.',
    '그건 잘 모르겠어요! 이야기는 이만하고 잠깐 쉬어야겠네요.',
    '그 이야기는 잘 모르겠어요. 이야기는 여기까지 하고 잠깐 쉬겠습니다.',
]
target.write_text(json.dumps(dict(items=rows, wordGroups=[dict(words=g) for g in groups],
    endings=[dict(personaId=p['id'],speech=s) for p,s in zip(people,endings)], replyVocabulary=reply_vocabulary), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(f'Built {len(rows)} reply mappings for {len(people)} personas: {target}')
requests = load('Resources/CafeKnowledge/requests.json')
new_menu = []
for p in people:
    ending = next(terms[b['termId']].strip() for b in bindings
                  if b['personaId'] == profiles[p['id']] and b['slot'] == 'ending')
    for request in requests:
        new_menu.append(dict(personaId=p['id'], requestId=request['id'], name=request['name'],
                             speech=request['name']+' '+ending, voice=p['voice']))
(target.parent / 'NewMenuRequests.json').write_text(json.dumps(dict(tableName='새로운 메뉴',items=new_menu),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Built {len(new_menu)} persona-specific short answers in 새로운 메뉴')

# Reapply independent branches after rebuilding the original source tables.
import runpy
runpy.run_path(str(Path(__file__).with_name('apply-cafe-persona-branches.py')), run_name='__main__')
