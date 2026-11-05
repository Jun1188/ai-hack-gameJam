"""Loopback-only compatibility test. No external API and no real credentials."""
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
from pathlib import Path

count = 0
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass
    def do_POST(self):
        global count
        body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        assert self.path == '/v1/chat/completions'
        assert self.headers.get('Authorization') == 'Bearer cafe-mock-only'
        assert body['model'] == 'cafe-mock'
        assert body['response_format'] == {'type':'json_object'}
        assert 'chat_template_kwargs' not in body
        assert len(body['messages']) == 2 and any('가' <= ch <= '힣' for ch in body['messages'][1]['content'])
        count += 1
        # Mark transport/context checks without retaining any prompt or authorization values.
        Path('.utmp/cafe-api-mock-evidence.json').write_text(json.dumps(dict(requests=count,
            korean=True, model=True, authorization=True, context=('REPLY\n' in body['messages'][1]['content']))),encoding='utf-8')
        data = json.dumps(dict(choices=[dict(finish_reason='stop',message=dict(role='assistant',
            content=json.dumps(dict(speech='아까 이야기하던 일을 떠올리니 마음이 한결 편안해지네요.'),ensure_ascii=False)))]),ensure_ascii=False).encode()
        self.send_response(200)
        self.send_header('Content-Type','application/json; charset=utf-8')
        self.send_header('Content-Length',str(len(data)))
        self.end_headers()
        self.wfile.write(data)
HTTPServer(('127.0.0.1',18766),Handler).serve_forever()
