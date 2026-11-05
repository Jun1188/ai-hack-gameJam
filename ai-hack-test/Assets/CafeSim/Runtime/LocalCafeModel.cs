using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace CafeSim
{
    // Scene-local owned process. No dependency on the user's LM Studio/Hub or user paths.
    public sealed class LocalCafeModel : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public string Status { get; private set; } = "로컬 모델 준비 전";
        public string Error { get; private set; }
        public int threads = 8;
        public int requestTimeoutSeconds = 300;
        Process server;
        UnityWebRequest activeRequest;
        string baseUrl, token;

        [Serializable] sealed class Message { public string role, content; }
        [Serializable] sealed class TemplateOptions { public bool enable_thinking = false; }
        [Serializable] sealed class ResponseFormat { public string type = "json_object"; }
        [Serializable] sealed class Request
        {
            public Message[] messages;
            public float temperature = 0.6f;
            public int max_tokens = 96;
            public bool stream = false;
            public TemplateOptions chat_template_kwargs = new TemplateOptions();
            public ResponseFormat response_format = new ResponseFormat();
        }
        [Serializable] sealed class Choice { public Message message; public string finish_reason; }
        [Serializable] sealed class Response { public Choice[] choices; }

        public static string PayloadPath
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../LocalInference"));
#else
                return Path.Combine(Application.streamingAssetsPath, "CafeLocal");
#endif
            }
        }
        public IEnumerator Boot()
        {
            Shutdown();
            Error = null;
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            { Fail("이 추론 패키지는 Windows x64용입니다."); yield break; }
            string root = PayloadPath;
            string executable = Path.Combine(root, "engine/llama-server.exe");
            string model = Path.Combine(root, "model.gguf");
            if (!File.Exists(executable) || !File.Exists(model) || !File.Exists(Path.Combine(root, "manifest.json")))
            { Fail("로컬 모델 파일이 없습니다. tools/prepare-cafe-local.py로 검증 후 준비해 주세요."); yield break; }
            int port;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            token = Guid.NewGuid().ToString("N");
            baseUrl = "http://127.0.0.1:" + port;
            var info = new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = "-m \"" + model + "\" --host 127.0.0.1 --port " + port +
                    " -c 4096 -ngl 0 -t " + Mathf.Clamp(threads, 1, 32) +
                    " --parallel 1 --jinja --reasoning-budget 0 --no-warmup --api-key " + token,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            try
            {
                server = new Process { StartInfo = info };
                // Drain both streams without retaining prompts or local dialogue in logs.
                server.OutputDataReceived += DiscardOutput;
                server.ErrorDataReceived += DiscardOutput;
                if (!server.Start()) throw new InvalidOperationException();
                server.BeginOutputReadLine(); server.BeginErrorReadLine();
            }
            catch (Exception e) { Fail("추론 엔진을 시작하지 못했습니다: " + e.GetType().Name); Shutdown(); yield break; }
            Status = "로컬 모델을 메모리에 불러오는 중…";
            float deadline = Time.realtimeSinceStartup + 240f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (server == null || server.HasExited) { Fail("추론 엔진이 종료되었습니다. 메모리와 배포 파일을 확인하세요."); Shutdown(); yield break; }
                using (var health = UnityWebRequest.Get(baseUrl + "/health"))
                {
                    health.timeout = 3;
                    health.SetRequestHeader("Authorization", "Bearer " + token);
                    yield return health.SendWebRequest();
                    if (health.result == UnityWebRequest.Result.Success && !server.HasExited)
                    { Ready = true; Status = "로컬 모델 준비 완료 · 오프라인 CPU"; yield break; }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Fail("모델 로딩 시간이 초과되었습니다. 다시 시도해 주세요."); Shutdown();
        }

        static void DiscardOutput(object sender, DataReceivedEventArgs args) { }

        public IEnumerator Generate(string prompt, Action<string> success, Action<string> failure, string systemPrompt = null)
        {
            if (!Ready || server == null || server.HasExited) { Ready = false; failure("로컬 모델 연결이 끊어졌습니다. 모델 재시작을 눌러 주세요."); yield break; }
            var data = new Request { messages = new[] { new Message { role = "system", content = systemPrompt ?? CafePrompts.System },
                new Message { role = "user", content = prompt } } };
            using (var request = new UnityWebRequest(baseUrl + "/v1/chat/completions", "POST"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = requestTimeoutSeconds;
                yield return request.SendWebRequest();
                activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                { failure("손님 응답을 받지 못했습니다 (HTTP " + request.responseCode + "). 같은 요청을 다시 시도할 수 있습니다."); yield break; }
                string text = null;
                try
                {
                    var response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                    if (response?.choices?.Length > 0 && response.choices[0].finish_reason == "stop")
                        text = response.choices[0].message?.content?.Trim();
                }
                catch (Exception) { }
                if (string.IsNullOrWhiteSpace(text) || text.Length > 3000)
                { failure("손님 응답이 비어 있거나 잘렸습니다. 다시 시도해 주세요."); yield break; }
                success(text);
            }
        }
        void Fail(string message) { Error = message; Status = message; Ready = false; }
        public void CancelRequest() { activeRequest?.Abort(); activeRequest = null; }
        public void Shutdown()
        {
            Ready = false; CancelRequest();
            if (server == null) return;
            try { if (!server.HasExited) { server.Kill(); server.WaitForExit(3000); } }
            catch (Exception e) { Debug.LogWarning("Cafe local process cleanup: " + e.GetType().Name); }
            finally { server.Dispose(); server = null; }
        }
        void OnDisable() { Shutdown(); }
        void OnApplicationQuit() { Shutdown(); }
    }
}
