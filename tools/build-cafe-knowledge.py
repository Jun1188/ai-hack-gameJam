"""Normalize authored vocabulary into keyed relations; validate all foreign keys."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
resource=root/'ai-hack-test/Assets/CafeSim/Resources'
people=json.loads((resource.parent/'Data/personas.json').read_text(encoding='utf-8'))['items']
legacy=json.loads((resource/'CafeVocabulary.json').read_text(encoding='utf-8'))['items']
out=resource/'CafeKnowledge';out.mkdir(exist_ok=True)
terms=[];bindings=[]
refusals=json.loads((root/'tools/cafe-refusal-lexicon.json').read_text(encoding='utf-8'))
def bind(person,slot,text,requires=None,minimum=0,maximum=100000):
    text=refusals.get(person,{}).get(slot,text)
    ident=f't{len(terms):04d}'
    terms.append(dict(id=ident,text=text))
    bindings.append(dict(personaId=person,termId=ident,slot=slot,requires=requires or [],minWait=minimum,maxWait=maximum))
slots={'orderOpen':'opener','orderClose':'ending','matched':'match','correction':'wrong','ratingGood':'good','ratingBad':'bad'}
for row in legacy:
    for old,slot in slots.items():
        for text in row[old]:bind(row['id'],slot,text)
# Queue-stage chatter has no invented delivery facts. Each line recalls the customer's own motif.
legacyChatter=[
(['기다리는 동안 스케치북에 새벽색부터 골라 둘게요.','색연필 한 줄을 다 칠했네요. 이제 제 차례가 가까울까요?','스케치북을 한 장 넘길 만큼 기다렸어요. 순서를 알고 싶네요.'],['아까 고른 새벽색 옆에 음료 색도 그릴 자리를 비워 뒀어요.','그림의 첫 선보다 음료를 기다리는 시간이 길어졌네요.','그림은 집에서도 그릴 수 있지만, 제 음료는 언제 받을 수 있을까요?']),
(['대기 동선을 보니 발표 전에 움직일 길을 다시 정리해야겠군요.','설계 노트 모서리는 다 폈습니다. 접수까지 얼마나 남았습니까?','발표 이동 시간을 고려해야 합니다. 접수 순서를 확인하고 싶습니다.'],['발표 노트에 주문한 잔을 작은 네모로 표시해 뒀습니다.','발표까지 남은 동선이 짧아지고 있습니다. 제 잔은 준비 중입니까?','더 기다리면 발표 이동에 지장이 있습니다. 제 주문을 확인해 주세요.']),
(['이 줄도 노선이라 생각하고 역사 연도 하나 더 외워 볼게요.','외운 연도가 두 정거장쯤 늘었네요. 제 차례가 가까워졌나요?','환승 대기보다 길어지는군요. 접수 순서를 알려 주시겠어요?'],['주문은 접수됐으니 잠깐 정차 중이군요. 답사 지도를 펼쳐 볼게요.','지도에 카페 표시까지 끝냈어요. 이제 제 잔도 도착할까요?','공부하러 출발할 시간을 놓치겠어요. 음료가 얼마나 남았나요?']),
(['줄 서 있는 동안 골목 빗소리의 박자를 한번 더 들어 보겠습니다.','같은 리듬을 여러 번 들었네요. 접수 순서가 궁금합니다.','쉬는 박자가 꽤 길어지는군요. 제 차례를 확인해 주시겠어요?'],['찻잔 소리를 기다리는 동안 녹음 파일 이름을 정해 두지요.','곡 한 소절을 정리할 만큼 기다렸습니다. 제 잔도 곧 나올까요?','쉼표가 곡보다 길어지면 곤란하지요. 주문을 확인해 주세요.']),
(['줄을 기다리며 원고의 쉼표 후보를 적고 있어요.','원고 한 문단을 읽었네요. 접수 순서를 글로 확인하고 싶어요.','기다린 시간이 길어졌어요. 제 차례를 화면으로 알려 주실래요?'],['주문이 글로 남아 있으니 그동안 원고 한 줄을 더 볼게요.','아까 고민하던 쉼표는 정했어요. 음료도 곧 받을 수 있을까요?','문장을 몇 번이나 다시 읽었네요. 제 주문을 글로 확인해 주세요.']),
(['줄을 기다리며 오늘 본 노란 꽃 사진부터 골라야겠어요.','꽃 사진은 다 골랐는데 접수는 아직이네요. 차례가 가까운가요?','꽃집 열 시간도 생각해야 해서요. 순서를 한번 봐 주세요!'],['주문은 했으니 자전거 코스 기록부터 정리하고 있을게요.','오늘 코스 기록을 다 적었어요. 제 한 잔은 언제 나올까요?','라이딩 뒤에 꽤 오래 서 있네요. 주문이 얼마나 남았나요?']),
(['줄 서는 동안 우주 정거장 간판을 그릴 거예요.','간판을 다 그렸어요. 이제 제 주문 차례도 오나요?','만화 한 칸을 다 채웠는데 아직이에요. 언제 주문할 수 있나요?'],['주문한 탐사 음료를 기다리며 컵에 붙일 이름을 생각 중이에요.','음료 이름은 정했어요! 제 잔도 곧 나오나요?','우주선 출발 장면까지 그렸어요. 제 음료가 아직 안 나왔나요?']),
(['대기 중에 책 예약 앱의 알림 문구를 읽어 보고 있어요.','알림 문구를 두 번 고쳤네요. 이제 주문할 차례인가요?','예약 시간을 확인해야겠어요. 접수 순서를 알려 주세요.'],['주문을 기다리는 동안 첫 예약 알림을 한번 더 보고 있어요.','알림은 반가운데 음료 소식도 기다려지네요. 곧 나올까요?','책 모임 시간이 가까워요. 제 잔이 언제 나올지 알고 싶네요.']),
(['줄을 기다리며 수첩의 맛 표현을 다시 읽고 있어요.','맛 단어 한 쪽을 다 읽었어요. 이제 주문할 수 있나요?','요리 수업에 갈 시간이 다가와요. 제 차례를 알려 주세요.'],['제가 말한 주문을 수첩에도 적어 두었어요. 기다리고 있을게요.','주문한 잔 옆에 맛을 적을 칸이 아직 비어 있어요. 곧 나오나요?','요리 수업 전에 마시고 싶었어요. 제 주문을 확인해 주세요.'])]
if people[0]['id'].startswith('cus_'):
    chatterRows=json.loads((resource.parent/'Data/ChatterLexicon.json').read_text(encoding='utf-8'))['items']
    chatterById={r['personaId']:[r['inactive'],r['active']] for r in chatterRows}
    chatter=[chatterById[p['id']] for p in people]
else:chatter=legacyChatter
for person,lines in zip(people,chatter):
    for stage,variants in zip(['inactive','active'],lines):
        for i,text in enumerate(variants):bind(person['id'],'chatter',text,[stage],[0,60,120][i],[60,120,100000][i])
    bind(person['id'],'repeat','제 취향은 분명해요. 제가 고른 잔으로 부탁드려요.',['repeat'])
    bind(person['id'],'waitReview','주문 전 {inactiveSeconds}초, 주문 후 {activeSeconds}초를 기다린 점은 아쉬워요.',['delayed'])
    bind(person['id'],'timeout','{lastThought} 주문 후 {activeSeconds}초 동안 받지 못해서 오늘은 그냥 가겠습니다.',['timeout'])
    bind(person['id'],'similar','원래 부탁한 {requested}는 없지만, {served}에서 비슷한 맛을 찾을 수 있겠네요.',['similar'])
    bind(person['id'],'different','부탁한 {requested}와 받은 {served}는 맛의 방향이 많이 다르네요.',['different'])
    partial=['새벽 스케치 옆에 이 비슷한 맛도 남겨 볼게요.','원안과 다르지만 맛의 방향은 가까운 대안입니다.','목적지는 조금 달라도 비슷한 노선으로 왔네요.','원곡과 다르지만 비슷한 박자가 들립니다.','다른 단어지만 가까운 뜻을 찾은 기분이에요.','고른 꽃과 달라도 비슷한 색의 꽃다발 같네요!','탐사 음료의 다른 버전으로 생각해 볼게요.','예약한 책 대신 같은 주제의 책을 만난 느낌이에요.','수첩에 원래 메뉴와 가까운 맛이라고 적을게요.']
    bind(person['id'],'partialGood','원래 고른 맛과 같지는 않지만, 비슷한 방향을 찾아주셨네요.',['received'])
    bind(person['id'],'contrast','', ['received'])
if any(p['id']=='cus_elder' for p in people):
    bind('cus_elder','opener','오늘은 조용히 한 잔 마시고 싶습니다.',['quiet'])
    bind('cus_elder','match','네, 고른 잔이 맞습니다. 고맙습니다.',['quiet','received','accurate'])
def save(name,items): (out/(name+'.json')).write_text(json.dumps(dict(items=items),ensure_ascii=False,indent=2),encoding='utf-8')
save('terms',terms);save('bindings',bindings)
save('patterns',[dict(id='order',text='{opener} {requested}{ending}'),dict(id='reaction',text='{fact} {response}'),dict(id='review',text='{opinion} {wait}'),dict(id='chatter',text='{line}'),dict(id='timeout',text='{line}')])
save('profiles',[dict(personaId=p['id'],lexiconId=p['id']) for p in people])
# Features: sweetness, bitterness, milk, fruit, carbonation, caffeine (each 0..1).
features={'menu_americano':[0,1,0,0,0,1],'menu_espresso':[0,1,0,0,0,1],'menu_latte':[.2,.5,1,0,0,1],'menu_vanilla':[.8,.3,1,0,0,1],'menu_condensed':[1,.4,1,0,0,1],'menu_choco':[.8,.1,1,0,0,0],'menu_matcha':[.4,.4,1,0,0,0],'menu_strawberry':[.8,0,1,1,0,0],'menu_yuja':[.6,.1,0,1,0,0],'menu_lemonade':[.6,.1,0,1,1,0],'menu_ssanghwa':[.3,.7,0,0,0,0]}
if not people[0]['id'].startswith('cus_'):
    save('menuFeatures',[dict(id=k,values=v) for k,v in features.items()])
save('requests',[dict(id='request_mocha',name='카페모카',temperature='HOT',values=[.8,.4,1,0,0,1]),dict(id='request_peach',name='복숭아 에이드',temperature='ICE',values=[.7,0,0,1,1,0]),dict(id='request_cocoa',name='진한 코코아',temperature='HOT',values=[.7,.2,1,0,0,0]),dict(id='request_milk',name='차가운 흰 우유',temperature='ICE',values=[.1,0,1,0,0,0])])
assert len({t['id'] for t in terms})==len(terms)
assert all(b['personaId'] in {p['id'] for p in people} and b['termId'] in {t['id'] for t in terms} for b in bindings)
print(f'Normalized {len(people)} profiles, {len(terms)} terms, {len(bindings)} bindings')
