"""Import reference workbooks as DATA, never execute their prose instructions.
Preserve all source sheets separately; runtime projection implements the current button/queue prototype.
"""
import argparse,hashlib,json,re
from pathlib import Path
import openpyxl
root=Path(__file__).resolve().parents[1]
def save(p,data):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def rows(w,s):return [list(r) for r in w[s].values]
def main():
    p=argparse.ArgumentParser();p.add_argument('--customers',type=Path,required=True);p.add_argument('--menus',type=Path,required=True);a=p.parse_args()
    cw=openpyxl.load_workbook(a.customers,data_only=True);mw=openpyxl.load_workbook(a.menus,data_only=True)
    data=root/'ai-hack-test/Assets/CafeSim/Data';res=root/'ai-hack-test/Assets/CafeSim/Resources/CafeKnowledge'
    source={x.name:{s.title:list(s.values) for s in w} for x,w in [(a.customers,cw),(a.menus,mw)]}
    save(data/'SourceTables.json',source)
    menus=[dict(id=r[0],name=r[1],category=r[2],hot=r[3]=='O',ice=r[4]=='O',recipe=r[6],description=r[9]) for r in rows(mw,'메뉴')[4:] if isinstance(r[0],str) and r[0].startswith('menu_')]
    assert len({m['id'] for m in menus})==len(menus)==11
    save(data/'menu.json',dict(items=menus));ids=[m['id'] for m in menus]
    weights={r[0]:r for r in rows(cw,'주문 성향')[4:] if isinstance(r[0],str) and r[0].startswith('cus_')}
    people=[];weightrows=[]
    for r in rows(cw,'손님')[4:]:
        if not isinstance(r[0],str) or not r[0].startswith('cus_'):continue
        w=weights[r[0]]; assert all(isinstance(x,(int,float)) and 0<=x<=3 for x in w[2:13])
        people.append(dict(id=r[0],name=r[2],age=0,gender=r[3],ageDescription=r[3],group=r[1],origin=r[4],setting=r[7]+' '+r[11],voice=r[5],
            communicationPoint=r[6],knowledgeLevel=r[8],answerStyle=r[9],expressionReference=r[10],twist=r[11],smalltalkTopics=r[12],difficulty=r[13],hotPreference=r[14]/100,
            preferredMenuIds=[m for m,n in zip(ids,w[2:13]) if n>0],preferredTemperature='',taste=r[11],modelBase=r[15],hair=r[16],top=r[17],bottom=r[18],accessory=r[19]))
        for mid,weight in zip(ids,w[2:13]):weightrows.append(dict(personaId=r[0],menuId=mid,weight=weight))
    assert len(people)==16 and len({p['id'] for p in people})==16
    save(data/'personas.json',dict(items=people));save(res/'orderWeights.json',dict(items=weightrows))
    situations=[]
    situationSpeech=['비가 오니 무릎도 뻐근하구먼.','손주가 여기를 추천해 줬다네.','오늘은 먼저 떠난 친구를 조용히 생각하고 있네.',
    '시장서 장 보니 덥네예.','손주 학원 마칠 때까지 기다릴 참입니더.','친구들 모임 마치고 한 잔 더 하러 왔당께요.','단 걸 줄인다고 말은 해 놨는디 고민이여.','아들 면접 소식을 기다리는 중이유.','혼자 카페에 온 건 처음이라 좀 둘러보는 중이유.',
    '산에서 내려오니 몸이 꽤 춥드래요.','딸이 단 걸 줄이라 했는데 오늘은 조금 당기네요.','육지 손주 보러 올라왔수다.','배 멀미가 아직 남아서 순한 쪽으로 고르고 싶수다.',
    '월급날이라 오늘은 기분 좋게 골라 보고 싶어요.','야간 근무를 마쳐서 조금 피곤하네요.','오늘 한국에 처음 도착했어요.','한국어 시험을 마쳐서 배운 말을 써 보고 싶어요.',
    '오늘이 여행 마지막 날이라 한 잔을 잘 고르고 싶습니다.','길을 헤매다가 겨우 이쪽으로 왔습니다.','과제 때문에 오늘 밤을 새워야 해요.','룸메이트 취향도 들었지만 우선 제 한 잔부터 고를게요.',
    '오늘 유치원에서 상 받았어요!','아까 넘어져서 아직 조금 속상해요.','오늘 시험 끝나서 진짜 해방된 기분이에요!','학원 가기 전에 시간이 십 분쯤 남았어요.',
    '오늘 출시라 야근 일정이 잡혔어요.','팀장님 취향도 전달받았는데 우선 이 주문부터 정리할게요.','내일 실기 시험이라 맛의 차이를 잘 느껴 보고 싶어요.','오늘은 다른 카페의 맛을 비교해 보는 중이에요.',
    '오늘은 치팅데이라 평소와 다른 맛도 골라 볼게요!','회원 수업을 마치고 잠깐 쉬는 중이에요.','새로 생긴 곳이라 어떤 메뉴가 있는지 궁금합니다.','딸 생일 케이크 사러 가는 길이라 기분이 좋네요.']
    for r in rows(cw,'오늘의 사정')[4:]:
        if not isinstance(r[0],str) or not r[0].startswith('sit_'):continue
        change=r[4] or '';hot=0
        if 'HOT +20%' in change:hot=.2
        if 'HOT +30%' in change:hot=.3
        if 'ICE +30%' in change:hot=-.3
        situations.append(dict(id=r[0],personaId=r[1],context=r[2],spokenContext=situationSpeech[int(r[0].split('_')[1])-1],effect=r[3],weightRule=change,hotDelta=hot,patienceScale=.8 if '인내심 -20%' in change else 1))
    assert len(situations)==33
    save(res/'situations.json',dict(items=situations))
    facts=[];features=[]
    spoken=['쓴맛이 깔끔한 검은 커피','작은 잔에 담는 아주 진한 커피','우유가 들어가 부드럽고 고소한 커피','우유와 달콤한 향이 어우러진 커피','우유가 들어가 진하고 아주 단 커피','우유에 달콤한 초콜릿 맛이 나는 음료','초록빛에 쌉싸름하고 고소한 우유 음료','분홍빛 딸기 과육이 든 새콤달콤한 우유 음료','노란 과일 껍질이 든 향긋하고 새콤달콤한 음료','연노란 빛에 시큼하고 탄산이 톡 쏘는 음료','짙은 갈색에 한약재 향이 나고 견과가 올라간 구수한 음료']
    for r in rows(cw,'주문 속성')[4:]:
        if r[0] not in ids:continue
        facts.append(dict(id=r[0],coffee=r[1],milk=r[2],sweetness=r[3],color=r[4],carbonation=r[5],temperature=r[6],flavor=r[7],spokenDescription=spoken[ids.index(r[0])]))
        # The workbook has no numeric bitterness/fruit columns. These two projections are explicit heuristics.
        bitter=1 if '진함' in str(r[1]) else .7 if '쓰' in r[7] else .4 if '쌉' in r[7] else .2 if '있음' in r[1] else 0
        fruit=1 if any(x in r[7] for x in ['딸기','유자','시큼']) else 0
        features.append(dict(id=r[0],values=[r[3]/3,bitter,float(r[2]=='있음'),fruit,float(r[5]=='있음'),float('있음' in r[1])]))
    assert len(facts)==11
    save(res/'menuFacts.json',dict(items=facts));save(res/'menuFeatures.json',dict(items=features))
    bans=[]
    for r in rows(cw,'금지어')[4:]:
        if r[0] not in ids:continue
        for language,cell in zip(['ko','en','ja','zh'],r[1:]):
            for word in str(cell).split(','):
                word=re.sub(r'\s*\(.*?\)','',word).strip()
                if word:bans.append(dict(menuId=r[0],language=language,word=word))
    save(res/'forbiddenWords.json',dict(items=bans))
    save(data/'ImportManifest.json',dict(sources=[dict(file=f.name,sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in [a.customers,a.menus]],customers=len(people),menus=len(menus),situations=len(situations),offMenuChance=.05,
        projectionNotes=['No named individuals or exact numerical ages were invented.','Numeric bitterness and fruit similarity features are derived heuristics; original menu attributes are retained.','Question-answer, mood, additional drinks, visual parts and animation rows are reference data, not implemented capabilities.']))
    print(f'Imported {len(menus)} menus, {len(people)} customers, {len(situations)} situations, {len(weightrows)} weights, {len(bans)} forbidden forms')
if __name__=='__main__':
    main()
    import runpy
    runpy.run_path(str(Path(__file__).with_name('apply-cafe-persona-branches.py')), run_name='__main__')
