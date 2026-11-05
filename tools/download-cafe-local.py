"""Preview by default. Download into staging only after an agent obtains user approval."""
import argparse,hashlib,json,urllib.request
from pathlib import Path
root=Path(__file__).resolve().parents[1]
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(8*1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def main():
    p=argparse.ArgumentParser();p.add_argument('--download',action='store_true');p.add_argument('--user-approved',action='store_true');a=p.parse_args()
    manifest=json.loads((root/'tools/cafe-model-downloads.json').read_text(encoding='utf-8'))
    print(json.dumps(manifest,indent=2))
    if not a.download:
        print('PREVIEW ONLY. Ask the user before downloading ~5.05 GB. No inference, installation, or network request performed.');return
    if not a.user_approved:p.error('Download requires explicit user approval in the current session; then pass --user-approved.')
    staging=root/'.utmp/cafe-acquisition';staging.mkdir(parents=True,exist_ok=True)
    for name in ['model','engine']:
        item=manifest[name];dest=staging/item['name']
        if dest.exists() and dest.stat().st_size==item['bytes'] and sha(dest)==item['sha256']:
            print('Already verified: '+item['name']);continue
        partial=dest.with_suffix(dest.suffix+'.partial')
        with urllib.request.urlopen(item['url'],timeout=120) as response,partial.open('wb') as f:
            while chunk:=response.read(8*1024*1024):f.write(chunk)
        if partial.stat().st_size!=item['bytes'] or sha(partial)!=item['sha256']:raise RuntimeError('Download verification failed: '+item['name'])
        partial.replace(dest);print('Verified: '+str(dest))
    print('Staged only. Test normal local text output, inspect it, then prepare the build payload. See docs/cafe-agent-setup.md.')
if __name__=='__main__':main()
