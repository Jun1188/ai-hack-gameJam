using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Normal command path: player utterance + scene context -> Claude Messages API -> RobotPlan JSON.
// Uses raw HTTP (UnityWebRequest) because the official C# SDK targets .NET runtimes Unity does not ship.
// Structured outputs (output_config.format) guarantee the reply matches RobotPlan's schema.
[Serializable]
public sealed class BarnClaudeClient
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2026-10-21";
    private const string KeyFile = "anthropic_api_key.txt";

    [Tooltip("Claude model id. claude-haiku-5-5 answers faster if latency matters more than judgement.")]
    public string model = "claude-opus-5-5";
    [Tooltip("low keeps replies fast enough for real-time commands.")]
    public string effort = "low";
    [Min(5)] public int timeoutSeconds = 20;
    [Min(0)] public int historyTurns = 6;

    private readonly List<(string role, string text)> history = new();
    private string apiKey;
    private bool keyLoaded;

    public bool Available => !string.IsNullOrEmpty(ApiKey);

    // The key comes from the ANTHROPIC_API_KEY environment variable or a file in persistentDataPath,
    // never from the scene, so it cannot be committed with the project.
    private string ApiKey
    {
        get
        {
            if (keyLoaded)
                return apiKey;
            keyLoaded = true;
            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                string path = Path.Combine(Application.persistentDataPath, KeyFile);
                if (File.Exists(path))
                    apiKey = File.ReadAllText(path).Trim();
            }
            return apiKey;
        }
    }

    public static string KeyFilePath => Path.Combine(Application.persistentDataPath, KeyFile);

    public IEnumerator Request(string system, string context, string utterance, Action<RobotPlan> done, Action<string> failed)
    {
        string userTurn = $"<scene>\n{context}\n</scene>\n할아버지: {utterance}";
        string body = BuildBody(system, userTurn);
        using var request = new UnityWebRequest(Endpoint, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = timeoutSeconds
        };
        request.SetRequestHeader("content-type", "application/json");
        request.SetRequestHeader("x-api-key", ApiKey);
        request.SetRequestHeader("anthropic-version", ApiVersion);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failed?.Invoke($"HTTP {request.responseCode} {request.error} {Trim(request.downloadHandler?.text)}");
            yield break;
        }

        var reply = JsonUtility.FromJson<MessageReply>(request.downloadHandler.text);
        if (reply == null || reply.content == null)
        {
            failed?.Invoke("빈 응답");
            yield break;
        }
        if (reply.stop_reason == "refusal" || reply.stop_reason == "max_tokens")
        {
            failed?.Invoke("stop_reason=" + reply.stop_reason);
            yield break;
        }
        string json = null;
        foreach (ReplyBlock block in reply.content)
            if (block.type == "text" && !string.IsNullOrEmpty(block.text))
                json = block.text;
        RobotPlan plan = null;
        try
        {
            if (json != null)
                plan = JsonUtility.FromJson<RobotPlan>(json);
        }
        catch (ArgumentException error)
        {
            failed?.Invoke("JSON 파싱 실패: " + error.Message);
            yield break;
        }
        if (plan == null)
        {
            failed?.Invoke("행동 JSON 없음");
            yield break;
        }

        // Only the short utterance and the plan go into history; scene context is resent fresh every turn.
        Remember("user", "할아버지: " + utterance);
        Remember("assistant", json);
        done?.Invoke(plan);
    }

    private void Remember(string role, string text)
    {
        history.Add((role, text));
        while (history.Count > historyTurns * 2)
            history.RemoveAt(0);
    }

    private string BuildBody(string system, string userTurn)
    {
        var json = new StringBuilder(4096);
        json.Append("{\"model\":").Append(BarnText.Quote(model));
        json.Append(",\"max_tokens\":4000");
        json.Append(",\"output_config\":{\"effort\":").Append(BarnText.Quote(effort));
        json.Append(",\"format\":{\"type\":\"json_schema\",\"schema\":").Append(PlanSchema).Append("}}");
        json.Append(",\"system\":").Append(BarnText.Quote(system));
        json.Append(",\"messages\":[");
        foreach (var (role, text) in history)
            json.Append("{\"role\":\"").Append(role).Append("\",\"content\":").Append(BarnText.Quote(text)).Append("},");
        json.Append("{\"role\":\"user\",\"content\":").Append(BarnText.Quote(userTurn)).Append("}]}");
        return json.ToString();
    }

    private const string PlanSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"actions\",\"say\"],\"properties\":{" +
        "\"actions\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false," +
        "\"required\":[\"type\",\"target\",\"slot\",\"direction\",\"text\"],\"properties\":{" +
        "\"type\":{\"type\":\"string\",\"enum\":[\"move_to\",\"push\",\"pick_up\",\"place\",\"rotate\",\"hold\",\"hammer\",\"say\"]}," +
        "\"target\":{\"type\":\"string\",\"description\":\"object id from the scene, or empty\"}," +
        "\"slot\":{\"type\":\"string\",\"description\":\"slot id for push/place, or empty\"}," +
        "\"direction\":{\"type\":\"string\",\"enum\":[\"\",\"left\",\"right\",\"around\"]}," +
        "\"text\":{\"type\":\"string\",\"description\":\"line for a say action, or empty\"}}}}," +
        "\"say\":{\"type\":\"string\",\"description\":\"what the robot says out loud right away\"}}}";

    private static string Trim(string text) => string.IsNullOrEmpty(text) ? "" : text.Length > 300 ? text[..300] : text;

    [Serializable]
    private sealed class MessageReply
    {
        public ReplyBlock[] content;
        public string stop_reason;
    }

    [Serializable]
    private sealed class ReplyBlock
    {
        public string type;
        public string text;
    }
}
