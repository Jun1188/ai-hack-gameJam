"""Generate compact, bounded persona choices with the already packaged local model."""
import json, pathlib, socket, subprocess, time, urllib.request
root=pathlib.Path(__file__).resolve().parents[1]
payload=root/'ai-hack-test/LocalInference'
resource=root/'ai-hack-test/Assets/CafeSim/Resources'
people=json.loads((resource.parent/'Data/personas.json').read_text(encoding='utf-8'))['items']
vocab=json.loads((resource/'CafeVocabulary.json').read_text(encoding='utf-8'))['items']
if all(len(v['orderOpen'])==1 for v in vocab):
    plan=dict(choices=[0]*len(vocab),source='Authored imported vocabulary; one opener per profile, no model selection needed',preparedSeconds=0)
    (resource/'CafePlans.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Single-variant compatibility table prepared; no engine or model loaded.')
    raise SystemExit(0)
prompt='각 손님의 상황과 말투에 어울리는 어휘 변형 번호 0 또는 1을 순서대로 고른다. JSON만 반환: {"choices":[0,1,0,1,0,1,0,1,0]}\n'
for p,v in zip(people,vocab):
    assert p['id']==v['id']
    prompt+=f"{p['id']} {p['setting']} {p['voice']} 0={v['orderOpen'][0]} 1={v['orderOpen'][1]}\n"
with socket.socket() as s:
    s.bind(('127.0.0.1',0)); port=s.getsockname()[1]
url=f'http://127.0.0.1:{port}'
log=(root/'.utmp/cafe-plan-server.log').open('w',encoding='utf-8')
proc=subprocess.Popen([str(payload/'engine/llama-server.exe'),'-m',str(payload/'model.gguf'),'-c','4096','-ngl','0','-t','8','--parallel','1','--jinja','--reasoning-budget','0','--no-warmup','--host','127.0.0.1','--port',str(port)],cwd=payload/'engine',stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
try:
    for _ in range(120):
        if proc.poll() is not None: raise RuntimeError('Local engine exited')
        try:
            with urllib.request.urlopen(url+'/health',timeout=2) as r:
                if json.load(r)['status']=='ok': break
        except Exception: time.sleep(1)
    start=time.monotonic()
    body=dict(messages=[dict(role='system',content='페르소나 어휘 변형 번호를 고르는 편집자다. JSON choices 배열만 출력한다.'),dict(role='user',content=prompt)],max_tokens=96,temperature=.6,stream=False,response_format=dict(type='json_object'),chat_template_kwargs=dict(enable_thinking=False))
    req=urllib.request.Request(url+'/v1/chat/completions',data=json.dumps(body).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=180) as r: result=json.load(r)['choices'][0]
    assert result['finish_reason']=='stop'
    plan=json.loads(result['message']['content'])
    assert len(plan['choices'])==9 and all(type(x) is int and x in [0,1] for x in plan['choices'])
    plan['source']='Qwen3-VL-8B-Instruct-Q4_K_M; local CPU; bounded vocabulary selection'
    plan['preparedSeconds']=round(time.monotonic()-start,3)
    (resource/'CafePlans.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(plan,ensure_ascii=True))
finally:
    proc.terminate()
    try: proc.wait(timeout=10)
    except subprocess.TimeoutExpired: proc.kill();proc.wait()
    log.close()
