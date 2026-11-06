using System;
using UnityEngine;

namespace CafeSim
{
    public enum CafeApiProvider { Gemini, OpenAI, Compatible }
    [Serializable] public sealed class CafeModelPreferenceData
    {
        public bool useApi = true;
        public CafeApiProvider provider = CafeApiProvider.Gemini;
        public string model = "gemini-3.5-flash-lite";
        public string endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";
    }

    // Persist routing choices only. A key entered in the menu lives in memory, scoped to its provider and origin.
    public static class CafeModelPreferences
    {
        public const string PreferenceKey = "cafe.model.options.v1";
        static string sessionKey = "", keyIdentity = "";
        public static bool HasSelection => PlayerPrefs.HasKey(PreferenceKey);
        public static CafeModelPreferenceData Load()
        {
            try { return HasSelection ? JsonUtility.FromJson<CafeModelPreferenceData>(PlayerPrefs.GetString(PreferenceKey)) ?? new CafeModelPreferenceData() : new CafeModelPreferenceData(); }
            catch (ArgumentException) { return new CafeModelPreferenceData(); }
        }
        public static void SelectProvider(CafeModelPreferenceData data, CafeApiProvider provider)
        {
            data.provider = provider;
            data.endpoint = provider == CafeApiProvider.Gemini ? "https://generativelanguage.googleapis.com/v1beta/models/" :
                provider == CafeApiProvider.OpenAI ? "https://api.openai.com/v1/chat/completions" : "https://";
            data.model = provider == CafeApiProvider.Gemini ? "gemini-3.5-flash-lite" : "";
        }
        public static bool Validate(CafeModelPreferenceData data, out string reason)
        {
            reason = "";
            if (data == null) { reason = "모델 설정을 다시 선택해 주세요."; return false; }
            if (!data.useApi) return true;
            if (!Enum.IsDefined(typeof(CafeApiProvider), data.provider)) { reason = "API 공급자를 선택해 주세요."; return false; }
            if (string.IsNullOrWhiteSpace(data.model)) { reason = "공급자에서 사용할 모델 이름을 입력해 주세요."; return false; }
            if (!LocalCafeModel.ValidApiEndpoint(data.endpoint)) { reason = "HTTPS API 주소를 입력해 주세요. 로컬 서버는 HTTP도 지원합니다."; return false; }
            var uri = new Uri(data.endpoint);
            if (data.provider == CafeApiProvider.Gemini && (uri.Scheme != "https" || uri.Host != "generativelanguage.googleapis.com"))
            { reason = "Gemini는 Google API 주소를 사용해 주세요."; return false; }
            if (data.provider == CafeApiProvider.OpenAI && (uri.Scheme != "https" || uri.Host != "api.openai.com"))
            { reason = "다른 서버 주소는 호환 API를 선택해 주세요."; return false; }
            return true;
        }
        static string Identity(CafeModelPreferenceData data) => Uri.TryCreate(data.endpoint, UriKind.Absolute, out var uri)
            ? data.provider + "|" + uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant() : "";
        public static string SessionKey(CafeModelPreferenceData data) => Identity(data) == keyIdentity ? sessionKey : "";
        public static bool Save(CafeModelPreferenceData data, string apiKey, out string reason)
        {
            if (!Validate(data, out reason)) return false;
            data.model = data.model?.Trim() ?? ""; data.endpoint = data.endpoint?.Trim() ?? "";
            sessionKey = apiKey?.Trim() ?? ""; keyIdentity = Identity(data);
            PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
            return true;
        }
        public static void Apply(LocalCafeModel target)
        {
            if (!HasSelection) return;
            var data = Load();
            target.runtimeApiKey = "";
            if (!data.useApi || !Validate(data, out _)) { target.backend = CafeResponseBackend.LocalModel; return; }
            target.backend = data.provider == CafeApiProvider.Gemini ? CafeResponseBackend.Gemini : CafeResponseBackend.CompatibleApi;
            target.apiEndpoint = data.endpoint; target.apiModel = data.model;
            target.apiKeyEnvironmentVariable = data.provider == CafeApiProvider.Gemini ? "GEMINI_API_KEY" :
                data.provider == CafeApiProvider.OpenAI ? "OPENAI_API_KEY" : "CAFE_CHAT_API_KEY";
            target.runtimeApiKey = SessionKey(data);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearSessionKey() { sessionKey = ""; keyIdentity = ""; }
    }
}
