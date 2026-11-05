"""Test an EXISTING local model with the portable CPU server, then copy on success.
No model download, no LM Studio/Hub changes. Python is only a development tool.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import socket
import subprocess
import time
import urllib.request
import urllib.error
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PAYLOAD = ROOT / 'ai-hack-test/LocalInference'
WORK = ROOT / '.utmp/cafe-local'
TAG = 'b12125'
ARCHIVE_SHA = '968091ccb8c230e0cf7e0eed386310703bebae4a4fd4375f0c57db53958307d8'
URL = f'https://github.com/ggml-org/llama.cpp/releases/download/{TAG}/llama-{TAG}-bin-win-cpu-x64.zip'

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(8 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def get(url):
    with urllib.request.urlopen(url, timeout=120) as r:
        return r.read()

def chat(base, messages, structured=False):
    body = dict(messages=messages, temperature=0.6, max_tokens=384,
                chat_template_kwargs={'enable_thinking': False}, stream=False)
    if structured:
        body['response_format'] = {'type': 'json_object'}
    req = urllib.request.Request(base + '/v1/chat/completions',
          data=json.dumps(body, ensure_ascii=False).encode(), headers={'Content-Type': 'application/json'})
    start = time.monotonic()
    with urllib.request.urlopen(req, timeout=300) as r:
        response = json.load(r)
    choice = response['choices'][0]
    text = choice['message']['content'].strip()
    assert choice['finish_reason'] == 'stop', 'Truncated generation'
    assert 1 <= len(text) <= 1500 and re.search('[가-힣]', text), 'Not normal Korean text'
    assert '<think>' not in text and '\ufffd' not in text, 'Invalid visible output'
    return text, round(time.monotonic() - start, 2)

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--model', type=Path, required=True)
    p.add_argument('--test-only', action='store_true')
    p.add_argument('--copy-tested', action='store_true')
    p.add_argument('--model-source', default='https://huggingface.co/Qwen/Qwen3.5-9B')
    p.add_argument('--crt-dir', type=Path)
    args = p.parse_args()
    source = args.model.resolve(strict=True)
    with source.open('rb') as header:
        assert header.read(4) == b'GGUF'
    WORK.mkdir(parents=True, exist_ok=True)
    archive = WORK / 'cpu.zip'
    if not archive.exists() or digest(archive) != ARCHIVE_SHA:
        print('Downloading pinned CPU engine (not model)', flush=True)
        archive.write_bytes(get(URL))
    assert digest(archive) == ARCHIVE_SHA
    engine = WORK / 'engine'
    engine.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        z.extractall(engine)
    executable = next(engine.rglob('llama-server.exe'))
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0))
        port = s.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    log = (WORK / 'server.log').open('w', encoding='utf-8')
    command = [str(executable), '-m', str(source), '--host', '127.0.0.1', '--port', str(port),
               '-c', '4096', '-ngl', '0', '-t', '8', '--parallel', '1', '--jinja',
               '--reasoning-budget', '0', '--no-warmup']
    if args.copy_tested:
        evidence = json.loads((WORK / 'candidate.json').read_text(encoding='utf-8'))
        assert evidence['sourceHash'] == digest(source) and evidence['sourceName'] == source.name
        copy_payload(source, executable, evidence['samples'], args.model_source, args.crt_dir)
        return
    print('Testing existing model in CPU mode before copying', flush=True)
    proc = subprocess.Popen(command, cwd=executable.parent, stdout=log, stderr=log,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    samples = []
    try:
        deadline = time.monotonic() + 240
        while time.monotonic() < deadline:
            if proc.poll() is not None:
                raise RuntimeError('Server exited: ' + (WORK / 'server.log').read_text(encoding='utf-8')[-2000:])
            try:
                if json.loads(get(base + '/health')).get('status') == 'ok':
                    break
            except Exception:
                time.sleep(1)
        else:
            raise TimeoutError('Model load timeout')
        text, elapsed = chat(base, [{'role':'user','content':'한국어로 카페 손님처럼 따뜻한 차 한 잔을 주문하세요. 두 문장 이내로 답하세요.'}])
        samples.append(dict(kind='plain-korean', text=text, seconds=elapsed))
        menus = json.loads((ROOT / 'ai-hack-test/Assets/CafeSim/Data/menu.json').read_text(encoding='utf-8'))
        people = json.loads((ROOT / 'ai-hack-test/Assets/CafeSim/Data/personas.json').read_text(encoding='utf-8'))['items']
        system = ('당신은 바리스타가 아니라 음료를 주문하고 제공받는 카페 손님이다. 모든 대사는 제공된 페르소나의 상황, 취향, 말투를 따른다. '
                  '연령이나 성별만으로 고정관념을 만들지 않는다. 한국어 한두 문장으로 짧게 말한다. '
                  '메뉴 목록의 메뉴 정확히 하나, 한 잔과 가능한 HOT/ICE 하나만 주문한다. 메뉴 레시피를 사실로 사용한다. '
                  '에스프레소가 있는 음료에는 카페인이 있다. 당신은 음료를 만드는 직원이 아니며 손님의 말만 한다. JSON만 출력한다.')
        for person in [people[0], people[3], people[6]]:
            candidates = {'items':[m for m in menus['items'] if m['id'] in person['preferredMenuIds']]}
            order_prompt = json.dumps(dict(task='order', persona=person, menu=candidates, reminder=person['taste'],
                format={'speech':'손님의 주문 대사', 'menuId':'메뉴 ID', 'temperature':'HOT 또는 ICE'}),ensure_ascii=False)
            order, elapsed = chat(base,[{'role':'system','content':system},{'role':'user','content':order_prompt}],True)
            order = json.loads(order)
            item = next(m for m in menus['items'] if m['id'] == order['menuId'])
            assert item['id'] in person['preferredMenuIds'], 'Order contradicted persona preferences'
            assert order['temperature'] in ['HOT','ICE'] and item[order['temperature'].lower()]
            assert isinstance(order['speech'], str) and 1 <= len(order['speech']) <= 300
            # Test both matching service and a deliberate wrong delivery.
            serve = item if len(samples) != 3 else next(m for m in menus['items'] if m['id'] != item['id'])
            temp = order['temperature'] if serve is item else ('HOT' if serve['hot'] else 'ICE')
            reaction_prompt = json.dumps(dict(task='reaction', persona=person, order=order,
                served={'menu':serve,'temperature':temp}, matchesOrder=(serve['id']==item['id'] and temp==order['temperature']),
                instruction='실제 받은 메뉴와 주문의 차이 및 취향을 기준으로 솔직히 반응하고 별점 1~5를 정한다. 평가도 손님인 나의 말로 쓴다. 페르소나나 시스템 설명을 언급하지 않는다.',
                format={'speech':'손님의 반응', 'stars':'1~5 정수', 'reason':'페르소나에 맞는 한 문장 평가'}),ensure_ascii=False)
            reaction, rt = chat(base,[{'role':'system','content':system},{'role':'user','content':reaction_prompt}],True)
            reaction = json.loads(reaction)
            assert type(reaction['stars']) is int and 1 <= reaction['stars'] <= 5
            assert all(isinstance(reaction[k],str) and 1 <= len(reaction[k]) <= 300 for k in ['speech','reason'])
            assert all('페르소나' not in reaction[k] for k in ['speech','reason'])
            samples.append(dict(kind='persona-order-reaction',persona=person['id'],order=order,reaction=reaction,
                                served=serve['id'],temperature=temp,orderSeconds=elapsed,reactionSeconds=rt))
        print('Text and JSON structural checks passed; saving candidate for semantic review', flush=True)
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=15)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
        log.close()
    (WORK / 'candidate.json').write_text(json.dumps(dict(sourceHash=digest(source),sourceName=source.name,samples=samples),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(samples,ensure_ascii=True),flush=True)
    if args.test_only:
        return
    copy_payload(source, executable, samples, args.model_source, args.crt_dir)

def copy_payload(source, executable, samples, model_source, crt_dir=None):
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    target_engine = PAYLOAD / 'engine'
    shutil.copytree(executable.parent, target_engine, dirs_exist_ok=True)
    model_target = PAYLOAD / 'model.gguf'
    source_hash = digest(source)
    if not model_target.exists() or digest(model_target) != source_hash:
        partial = PAYLOAD / 'model.gguf.partial'
        shutil.copy2(source, partial)
        assert digest(partial) == source_hash
        os.replace(partial, model_target)
    # Redistribution notices live with the payload.
    (PAYLOAD / 'LLAMA-LICENSE.txt').write_bytes(get(f'https://raw.githubusercontent.com/ggml-org/llama.cpp/{TAG}/LICENSE'))
    try:
        license_text = get(model_source + '/resolve/main/LICENSE')
    except urllib.error.HTTPError as e:
        if e.code != 404:
            raise
        # Qwen3-VL Instruct publishes Apache-2.0 in its model card, without a LICENSE file.
        license_text = get('https://www.apache.org/licenses/LICENSE-2.0.txt')
    (PAYLOAD / 'QWEN-LICENSE.txt').write_bytes(license_text)
    (PAYLOAD / 'MODEL-NOTICE.txt').write_text('Qwen model by Alibaba Cloud / Qwen Team.\nModel source: ' + model_source + '\nQuantized GGUF: existing local lmstudio-community Q4_K_M, used for text only.\nLicense: Apache-2.0; no model weights modified.\n',encoding='utf-8')
    candidates=[]
    for base in [Path('C:/Program Files/Microsoft Visual Studio'),Path('C:/Program Files (x86)/Microsoft Visual Studio')]:
        if base.exists():candidates.extend(base.glob('*/**/VC/Redist/MSVC/*/x64/Microsoft.VC*.CRT'))
    crt = crt_dir or (sorted(candidates)[-1] if candidates else Path('missing-app-local-crt'))
    if not crt.exists():
        raise FileNotFoundError('App-local Visual C++ x64 redistributable folder required')
    for dll in crt.glob('*.dll'):
        shutil.copy2(dll, target_engine / dll.name)
    manifest = dict(engine=TAG, engineArchiveSha256=ARCHIVE_SHA, model=source.name,
                    modelFile='model.gguf', modelSha256=source_hash, modelBytes=source.stat().st_size,
                    platform='Windows x64 CPU', testedAt=time.strftime('%Y-%m-%dT%H:%M:%S%z'),
                    normalTextPassed=True, samples=samples,
                    sources=[URL,model_source])
    manifest['files'] = [dict(path=str(f.relative_to(PAYLOAD)).replace('\\','/'),bytes=f.stat().st_size,sha256=digest(f))
                         for f in sorted(PAYLOAD.rglob('*')) if f.is_file() and f.name != 'manifest.json' and not f.name.endswith('.partial')]
    (PAYLOAD / 'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    evidence = ROOT / 'docs/cafe-local-model-verification.json'
    evidence.parent.mkdir(exist_ok=True)
    evidence.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'passed':True,'copiedBytes':source.stat().st_size,'sha256':source_hash,'samples':samples},ensure_ascii=True),flush=True)

if __name__ == '__main__':
    main()
