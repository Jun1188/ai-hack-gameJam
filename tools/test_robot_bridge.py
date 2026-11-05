import copy
import json
from pathlib import Path
import subprocess
import threading
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import robot_bridge as bridge


def request(prompt="B1을 N1에 놓아"):
    return dict(requestId="r1", stageRunId="s1", prompt=prompt, snapshot={
        "objects": [dict(id="B1", name="큰 돌", kind="large_stone", carried=False), dict(id="P1", kind="pulley")],
        "nodes": [dict(id="N1", capacity=2, count=0), dict(id="A1", capacity=2, count=0)],
        "goals": [{"secret": "answer"}], "ghost": "hidden"})


class BridgeTests(unittest.TestCase):
    def test_answers_removed(self):
        clean = bridge.sanitize_request(request())
        self.assertNotIn("secret", json.dumps(clean))
        self.assertNotIn("goals", clean["snapshot"])
        self.assertNotIn("ghost", clean["snapshot"])

    def test_fallback_place(self):
        r = bridge.fallback(bridge.sanitize_request(request()))
        self.assertEqual([a["type"] for a in r["actions"]], ["pick_up", "place"])
        self.assertEqual(r["actions"][1]["targetId"], "N1")

    def test_held_place(self):
        r = request(); r["snapshot"]["objects"][0]["carried"] = True
        self.assertEqual(len(bridge.fallback(r)["actions"]), 1)

    def test_ambiguous_unknown_multi(self):
        for text in ("알아서 해", "B99를 N1에 놓아", "B1과 P1을 N1에 놓아", "B1을 N1 아니면 A1에 놓아"):
            self.assertEqual(bridge.fallback(request(text))["status"], "clarify")

    def test_rotate(self):
        self.assertEqual(bridge.fallback(request("P1을 90도로 돌려"))["actions"][0]["rotation"], 90)

    def test_house_push_and_adjust(self):
        self.assertEqual(bridge.fallback(request("B1을 N1로 밀어"))["actions"][0]["type"], "push")
        self.assertEqual(bridge.fallback(request("B1을 N1에 맞춰"))["actions"][0]["type"], "adjust")

    def test_explicit_batch(self):
        result = bridge.fallback(request("B1을 N1에, P1을 A1에 놓아"))
        self.assertEqual(result["status"], "plan")
        self.assertEqual([a["targetId"] for a in result["actions"] if a["type"] == "place"], ["N1", "A1"])

    def test_bad_payload(self):
        for value in (None, {}, {**request(), "prompt": "x" * 1001}):
            with self.assertRaises(ValueError): bridge.sanitize_request(value)

    def test_reject_bad_actions(self):
        r = request()
        for action in (bridge.action("shell"), bridge.action("place", "B1", "N99"), bridge.action("rotate", "P1", rotation=45)):
            with self.assertRaises(ValueError):
                bridge.validate_response(dict(status="plan", message="ok", actions=[action]), r)
        with self.assertRaises(ValueError):
            bridge.validate_response(dict(status="plan", message="ok", actions=[bridge.action("say")] * 13), r)

    def test_codex_structured_adapter(self):
        payload = dict(status="plan", message="옮길게요", actions=[bridge.action("pick_up", "B1"), bridge.action("place", "B1", "N1")])
        event = json.dumps(dict(type="item.completed", item=dict(type="agent_message", text=json.dumps(payload))))
        with patch("robot_bridge.subprocess.run", return_value=subprocess.CompletedProcess([], 0, event, "")) as run:
            result = bridge.codex_translate(bridge.sanitize_request(request()), executable="codex.exe")
            self.assertEqual(result["provider"], "Codex CLI")
            command = run.call_args.args[0]
            self.assertIn("--ignore-user-config", command)
            self.assertIn("features.shell_tool=false", command)
            self.assertNotIn("secret", run.call_args.kwargs["input"])
            self.assertNotIn("OPENAI_API_KEY", run.call_args.kwargs["env"])

    def test_tool_events_rejected(self):
        event = json.dumps(dict(type="item.completed", item=dict(type="command_execution")))
        with patch("robot_bridge.subprocess.run", return_value=subprocess.CompletedProcess([], 0, event, "")):
            with self.assertRaises(ValueError): bridge.codex_translate(request(), executable="codex.exe")

    def test_nonzero_and_malformed(self):
        for code, output in ((1, ""), (0, "bad json"), (0, "{}")):
            with patch("robot_bridge.subprocess.run", return_value=subprocess.CompletedProcess([], code, output, "private stderr")):
                with self.assertRaises((ValueError, RuntimeError)): bridge.codex_translate(request(), executable="codex.exe")


class HttpTests(unittest.TestCase):
    def setUp(self):
        self.server = bridge.RobotServer(("127.0.0.1", 0), fallback_only=True)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True); self.thread.start()
        self.base = "http://127.0.0.1:" + str(self.server.server_address[1])

    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join()

    def post(self, value):
        with urlopen(Request(self.base + "/interpret", json.dumps(value).encode(), {"Content-Type": "application/json"}), timeout=5) as response:
            return json.load(response)

    def test_health_and_roundtrip(self):
        with urlopen(self.base + "/health") as response: self.assertEqual(json.load(response)["status"], "ready")
        result = self.post(request()); self.assertEqual(result["stageRunId"], "s1"); self.assertEqual(result["status"], "plan")

    def test_busy(self):
        self.server.gate.acquire()
        try:
            with self.assertRaises(HTTPError) as error: self.post(request())
            self.assertEqual(error.exception.code, 409)
            error.exception.close()
        finally: self.server.gate.release()

    def test_invalid(self):
        with self.assertRaises(HTTPError) as error: self.post({})
        self.assertEqual(error.exception.code, 400)
        error.exception.close()

    def test_timeout_fallback(self):
        self.server.fallback_only = False
        with patch("robot_bridge.codex_translate", side_effect=subprocess.TimeoutExpired("codex", 30)):
            result = self.post(request())
        self.assertIn("시간 초과", result["provider"])
        self.assertEqual(result["status"], "plan")


if __name__ == "__main__":
    unittest.main()
