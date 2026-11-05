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
    public enum CafeResponseBackend { LocalModel, CompatibleApi, Gemini }
    // Scene-local owned process. No dependency on the user's LM Studio/Hub or user paths.
    public sealed class LocalCafeModel : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public string Status { get; private set; } = "로컬 모델 준비 전";
        public string Error { get; private set; }
        public int threads = 8;
        public int requestTimeoutSeconds = 300;
        [Header("Live reply connection (database / topic gate stay local)")]
        public CafeResponseBackend backend = CafeResponseBackend.LocalModel;
        [Tooltip("Full OpenAI-compatible chat completions URL. HTTPS required except loopback.")]
        public string apiEndpoint = "";
        public string apiModel = "";
        [Tooltip("Environment variable name only. Never put an API key in scene assets. Empty allows an unauthenticated endpoint.")]
        public string apiKeyEnvironmentVariable = "CAFE_CHAT_API_KEY";
        public bool UseApi => backend != CafeResponseBackend.LocalModel;
        [NonSerialized] public string runtimeApiKey;
        public void ConfigureGemini()
        {backend=CafeResponseBackend.Gemini;apiEndpoint="https://generativelanguage.googleapis.com/v1beta/models/";apiModel="gemini-3.5-flash-lite";apiKeyEnvironmentVariable="GEMINI_API_KEY";}
        Process server;
        UnityWebRequest activeRequest;
        string baseUrl, token;

        [Serializable] sealed class Message { public string role, content; }
        [Serializable] sealed class TemplateOptions { public bool enable_thinking = false; }
        [Serializable] sealed class ResponseFormat { public string type = "json_object"; }
        [Serializable] class Request
        {
            public Message[] messages;
            public float temperature = 0.6f;
            public int max_tokens = 96;
            public bool stream = false;
            public ResponseFormat response_format = new ResponseFormat();
        }
        [Serializable] sealed class LocalRequest : Request { public TemplateOptions chat_template_kwargs = new TemplateOptions(); }
        [Serializable] sealed class ApiRequest : Request { public string model; }
        [Serializable] sealed class Choice { public Message message; public string finish_reason; }
        [Serializable] sealed class Response { public Choice[] choices; }
        [Serializable] sealed class GeminiPart { public string text; }
        [Serializable] sealed class GeminiContent { public GeminiPart[] parts; }
        [Serializable] sealed class GeminiConfig { public float temperature=.6f; public int maxOutputTokens=512; public string responseMimeType="application/json"; }
        [Serializable] sealed class GeminiRequest { public GeminiContent systemInstruction; public GeminiContent[] contents; public GeminiConfig generationConfig=new GeminiConfig(); }
        [Serializable] sealed class GeminiCandidate { public GeminiContent content; public string finishReason; }
        [Serializable] sealed class GeminiResponse { public GeminiCandidate[] candidates; }

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
            ApplyLaunchOptions();
            if(UseApi)
            {
                if(!ValidApiEndpoint(apiEndpoint)||string.IsNullOrWhiteSpace(apiModel))
                {Fail("API 주소(전체 chat/completions URL)와 모델명을 설정해 주세요. 외부 주소는 HTTPS여야 합니다.");yield break;}
                token=string.IsNullOrWhiteSpace(runtimeApiKey)?(string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable)?null:Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable)):runtimeApiKey;
#if UNITY_EDITOR
                string keyPath=Path.Combine(Application.dataPath,"../UserSettings/CafeChatApiKey.txt");
                if(string.IsNullOrWhiteSpace(token)&&backend==CafeResponseBackend.Gemini&&File.Exists(keyPath))token=File.ReadAllText(keyPath).Trim();
#endif
                if(!string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable)&&string.IsNullOrWhiteSpace(token))
                {Fail("API 키 환경변수 "+apiKeyEnvironmentVariable+"를 설정한 뒤 응답 연결을 재시작해 주세요.");yield break;}
                Ready=true;Status="API 설정 준비 완료 · 연결은 전송 시 확인";yield break;
            }
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

        public static bool ValidApiEndpoint(string value)
        {
            return Uri.TryCreate(value,UriKind.Absolute,out var uri)&&string.IsNullOrEmpty(uri.UserInfo)&&
                string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment)&&
                (uri.Scheme=="https"||(uri.Scheme=="http"&&uri.IsLoopback));
        }
        void ApplyLaunchOptions()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)
            {
                switch(args[i])
                {
                    case "--cafe-chat-provider":string provider=args[++i];if(provider=="gemini")ConfigureGemini();else backend=provider=="api"?CafeResponseBackend.CompatibleApi:CafeResponseBackend.LocalModel;break;
                    case "--cafe-chat-endpoint":apiEndpoint=args[++i];break;
                    case "--cafe-chat-model":apiModel=args[++i];break;
                    case "--cafe-chat-key-env":apiKeyEnvironmentVariable=args[++i];break;
                }
            }
        }

        public IEnumerator Generate(string prompt, Action<string> success, Action<string> failure, string systemPrompt = null, int maxTokens = 96)
        {
            if (!Ready || (!UseApi&&(server == null || server.HasExited))) { Ready = false; failure("응답 연결이 끊어졌습니다. 응답 연결 재시작을 눌러 주세요."); yield break; }
            Request data=UseApi?(Request)new ApiRequest{model=apiModel}:new LocalRequest();
            data.max_tokens=Mathf.Clamp(maxTokens,32,256);
            data.messages = new[] { new Message { role = "system", content = systemPrompt ?? CafePrompts.System },
                new Message { role = "user", content = prompt } };
            bool gemini=backend==CafeResponseBackend.Gemini;
            string endpoint=gemini?apiEndpoint.TrimEnd('/')+"/"+Uri.EscapeDataString(apiModel)+":generateContent":UseApi?apiEndpoint:baseUrl+"/v1/chat/completions";
            string payload=JsonUtility.ToJson(data);
            if(gemini)payload=JsonUtility.ToJson(new GeminiRequest{
                systemInstruction=new GeminiContent{parts=new[]{new GeminiPart{text=systemPrompt??CafePrompts.System}}},
                contents=new[]{new GeminiContent{parts=new[]{new GeminiPart{text=prompt}}}}
            });
            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if(!string.IsNullOrWhiteSpace(token))request.SetRequestHeader(gemini?"x-goog-api-key":"Authorization",gemini?token:"Bearer "+token);
                request.timeout = requestTimeoutSeconds;
                yield return request.SendWebRequest();
                activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                { failure(ApiFailure(request.responseCode)); yield break; }
                string text = null;
                try
                {
                    if(gemini)
                    {
                        var response=JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                        if(response?.candidates?.Length>0&&response.candidates[0].finishReason=="STOP")
                        {var parts=response.candidates[0].content?.parts;if(parts!=null){text="";foreach(var part in parts)text+=part.text;text=text.Trim();}}
                    }
                    else
                    {
                        var response=JsonUtility.FromJson<Response>(request.downloadHandler.text);
                        if(response?.choices?.Length>0&&response.choices[0].finish_reason=="stop")text=response.choices[0].message?.content?.Trim();
                    }
                }
                catch (Exception) { }
                if (string.IsNullOrWhiteSpace(text) || text.Length > 3000)
                { failure("손님 응답이 비어 있거나 잘렸습니다. 다시 시도해 주세요."); yield break; }
                success(text);
            }
        }
        public static string ApiFailure(long code)
        {
            if(code==402)return "Gemini 프로젝트의 선불 크레딧이 소진되었습니다 (HTTP 402). 충전하거나 다른 API 키를 적용해 주세요.";
            if(code==401||code==403)return "API 인증 또는 접근 권한을 확인해 주세요 (HTTP "+code+").";
            if(code==429)return "API 요청 한도에 도달했습니다 (HTTP 429). 잠시 후 다시 시도해 주세요.";
            return "손님 응답을 받지 못했습니다 (HTTP "+code+"). 연결 상태를 확인한 뒤 다시 시도해 주세요.";
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
