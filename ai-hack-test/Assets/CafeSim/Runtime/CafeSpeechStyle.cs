using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace CafeSim
{
    // Marks the parts of a customer's line that matter for the puzzle:
    //  - Clue: words that describe the drink (milk, sweet, cold, colour…) are drawn in an accent colour.
    //  - Voice: the speaker's own way of talking (dialect endings, slang, jargon, foreign words and the
    //    phrases collected in the 동네 사전) bob up and down above a still shadow.
    public static class CafeSpeechStyle
    {
        public const byte Clue = 1, Voice = 2;

        // Drink attributes as customers describe them (menu facts and their everyday forms).
        static readonly Regex Clues = new Regex(string.Join("|", new[]
        {
            "우유", "커피", "카페인", "달콤", "달달", "달고", "단맛", "(?<![가-힣])단(?=\\s)", "쓴맛", "(?<![가-힣])쓴(?=\\s)", "쓰고",
            "쌉쌀", "쌉싸름", "씁쓸", "씁쓰리", "새콤", "새그러븐", "새큼", "시큼", "상큼", "탄산", "톡 쏘", "톡쏘",
            "따뜻(?:하게|한)?", "뜨겁게|뜨거운", "뜨끈(?:하게|허게)?", "뜨뜻(?:하게|한)?", "따시게", "따땃하게",
            "차갑게|차가운", "시원(?:하게|한|하이|허게)?", "션하게|션하", "얼음",
            "초콜릿", "초코", "딸기", "과육", "과일 껍질", "껍질", "한약재", "약재", "견과", "구수", "고소", "부드러",
            "진하고|진하게|진한", "작은 잔", "검은", "짙은 갈색", "연한 갈색", "갈색", "초록", "분홍", "노란", "연노란", "향긋",
            "녹차", "바닐라", "레몬", "과일"
        }), RegexOptions.Compiled);

        // How each customer talks (from the 말투 규칙 column of the customer table).
        static readonly Dictionary<string, Regex> Voices = new Dictionary<string, Regex>
        {
            { "cus_elder", Words("구먼", "로군", "일세", "자네", "탕약", "숭늉", "다방", "양반", "허허", "겠나", "는가") },
            { "cus_gyeongsang", Words("쪼매", "주이소", "하이소", "아이다", "따시게", "고마", "입니더", "니더", "데이", "아이가", "알제", "있제", "맞제", "예\\.") },
            { "cus_jeolla", Words("거시기", "허벌나게", "당께", "랑께", "아따", "쪼까", "부러", "겄", "잉(?=[\\s.!?,~]|$)", "여(?=[\\s.!?]|$)", "애(?=[\\s.!?]|$)") },
            { "cus_chungcheong", Words("(?<![우이자])유(?=[\\s.!?,~…]|$)", "슈(?=[\\s.!?,~…]|$)", "겄", "뜨뜻", "션하", "그려") },
            { "cus_gangwon", Words("드래요", "드래", "마커", "뜨뜻") },
            { "cus_jeju", Words("마씸", "혼저", "줍서", "쿠다", "수다", "돌코롬", "푸린", "놓앙", "허게", "하영", "헌(?=\\s)") },
            { "cus_student", Words("국룰", "ㄹㅇ", "얼죽아", "달달구리", "할매니얼", "st", "MAX", "ㅋㅋ+", "ㅎㅇ", "찐", "노노") },
            { "cus_pangyo", Words("니즈", "싱크", "얼라인", "블로커", "디플로이", "ASAP", "MVP", "스펙", "디펜던시", "리소스", "이슈") },
            { "cus_barista", Words("싱글", "더블", "데미타세", "산미", "바디감", "추출", "샷", "논커피", "스팀 밀크", "크레마", "원두") },
            { "cus_trainer", Words("당 제로", "벌크업", "단백질", "카테킨", "프리워크아웃", "치팅데이", "쿨다운", "OK") },
            { "cus_japan", Words("카페오레", "스미마셍", "아타타카이", "츠메타이", "아마이", "오차", "코코아") },
            { "cus_child", Words("공주님", "곰돌이", "동동", "사탕") },
        };

        // Foreign-script words (Latin incl. Vietnamese, Han, kana) for the foreign customers.
        static readonly Regex Foreign = new Regex("[A-Za-zÀ-ỹ\\u4E00-\\u9FFF\\u3040-\\u30FF]+(?:\\s[A-Za-zÀ-ỹ\\u4E00-\\u9FFF\\u3040-\\u30FF]+)*", RegexOptions.Compiled);
        static readonly HashSet<string> ForeignSpeakers = new HashSet<string> { "cus_sea", "cus_west", "cus_japan", "cus_china" };

        static Regex Words(params string[] patterns) => new Regex(string.Join("|", patterns), RegexOptions.Compiled);

        public static byte[] Analyze(string text, string personaId)
        {
            var marks = new byte[text?.Length ?? 0];
            if (marks.Length == 0) return marks;
            Mark(marks, Clues, text, Clue);
            if (personaId == null) return marks;
            if (Voices.TryGetValue(personaId, out var voice)) Mark(marks, voice, text, Voice);
            if (ForeignSpeakers.Contains(personaId)) Mark(marks, Foreign, text, Voice);
            // Every phrase this customer can teach the 동네 사전 also moves.
            foreach (var phrase in CafeCollection.Instance.PhraseTexts(personaId))
                for (int at = text.IndexOf(phrase, System.StringComparison.Ordinal); at >= 0; at = text.IndexOf(phrase, at + phrase.Length, System.StringComparison.Ordinal))
                    for (int i = at; i < at + phrase.Length; i++) marks[i] |= Voice;
            return marks;
        }

        static void Mark(byte[] marks, Regex pattern, string text, byte flag)
        {
            foreach (Match m in pattern.Matches(text))
                for (int i = m.Index; i < m.Index + m.Length; i++) marks[i] |= flag;
        }
    }

    // Per-character colour and bounce on a legacy uGUI Text. Unity builds one quad per visible character
    // (whitespace has none), so quads are matched to characters by skipping whitespace; if the counts ever
    // disagree the text is left untouched rather than mis-styled.
    [RequireComponent(typeof(Text))]
    public sealed class CafeSpeechTextEffect : BaseMeshEffect
    {
        public Color clueColor = new Color32(196, 92, 36, 255);
        public Color shadowColor = new Color32(66, 42, 28, 70);
        public float bounce = 3.4f, speed = 7f, phasePerChar = .55f;

        string text = "";
        byte[] marks = new byte[0];
        bool animated;
        readonly List<UIVertex> stream = new List<UIVertex>();
        readonly List<UIVertex> output = new List<UIVertex>();

        public void SetMarks(string value, byte[] value2)
        {
            text = value ?? "";
            marks = value2 ?? new byte[0];
            animated = System.Array.Exists(marks, m => (m & CafeSpeechStyle.Voice) != 0);
            graphic.SetVerticesDirty();
        }

        // Unity skips ordinary whitespace but still emits a quad for no-break spaces (the bubble's indent).
        static bool NoQuad(char c) => char.IsWhiteSpace(c) && c != ' ' && c != ' ' && c != ' ';

        void Update()
        {
            if (animated && graphic != null && graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || marks.Length == 0 || marks.Length != text.Length) return;
            stream.Clear();
            vh.GetUIVertexStream(stream);   // six vertices (two triangles) per quad
            int quads = stream.Count / 6, visible = 0;
            foreach (char c in text) if (!NoQuad(c)) visible++;
            if (quads != visible) return;

            output.Clear();
            float t = Time.unscaledTime * speed;
            int q = 0;
            for (int i = 0; i < text.Length && q < quads; i++)
            {
                if (NoQuad(text[i])) continue;
                byte mark = marks[i];
                int start = q * 6;
                if ((mark & CafeSpeechStyle.Voice) != 0)
                {
                    // A still shadow on the baseline, and the glyph hopping above it.
                    float lift = (Mathf.Sin(t - i * phasePerChar) * .5f + .5f) * bounce + .6f;
                    for (int v = 0; v < 6; v++)
                    {
                        var s = stream[start + v];
                        s.position += new Vector3(.8f, -1.2f, 0);
                        s.color = shadowColor;
                        output.Add(s);
                    }
                    for (int v = 0; v < 6; v++)
                    {
                        var g = stream[start + v];
                        g.position += new Vector3(0, lift, 0);
                        if ((mark & CafeSpeechStyle.Clue) != 0) g.color = clueColor;
                        output.Add(g);
                    }
                }
                else
                    for (int v = 0; v < 6; v++)
                    {
                        var g = stream[start + v];
                        if ((mark & CafeSpeechStyle.Clue) != 0) g.color = clueColor;
                        output.Add(g);
                    }
                q++;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }
    }
}
