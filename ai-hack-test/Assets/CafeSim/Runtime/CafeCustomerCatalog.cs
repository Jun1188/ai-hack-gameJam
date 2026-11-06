using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CafeSim
{
    [Serializable] public sealed class CustomerAssetTable { public CustomerAssetEntry[] items = Array.Empty<CustomerAssetEntry>(); }
    [Serializable] public sealed class CustomerAssetEntry
    {
        public string source, resource, description, mappingReason, status;
        public string[] personaIds = Array.Empty<string>(), issues = Array.Empty<string>();
        public string idleResource, idleClip, walkResource, walkClip, controllerParameter;
        public float height = 1.7f;
    }

    // Filename evidence describes the supplied asset, never the customer's tastes or dialogue.
    public static class CafeCustomerNames
    {
        sealed class Detail
        {
            internal string field, value, label;
            internal string[] aliases;
            internal Detail(string field, string value, string label, params string[] aliases)
            { this.field = field; this.value = value; this.label = label; this.aliases = aliases; }
        }
        static readonly Detail[] Details =
        {
            new("top", "top_cardigan", "가디건", "cardigan", "가디건"),
            new("top", "top_vest_padding", "패딩 조끼", "padded_vest", "vest", "조끼"),
            new("top", "top_blouse", "블라우스", "blouse", "블라우스"),
            new("top", "top_jacket_work", "작업 재킷", "work_jacket", "작업복"),
            new("top", "top_padding_jumper", "패딩 점퍼", "padding", "puffer", "패딩"),
            new("top", "top_work_shirt", "작업 셔츠", "work_shirt"),
            new("top", "top_coat", "코트", "coat", "코트"),
            new("top", "top_hoodie", "후드", "hoodie", "후드"),
            new("top", "top_uniform", "교복", "uniform", "교복"),
            new("top", "top_sports", "운동복", "sports", "sportswear", "운동복"),
            new("accessory", "acc_backpack", "배낭", "backpack", "배낭"),
            new("accessory", "acc_camera", "카메라", "camera", "카메라"),
            new("accessory", "acc_lanyard", "사원증", "lanyard", "사원증"),
            new("accessory", "acc_notebook", "노트", "notebook", "노트"),
            new("accessory", "acc_tumbler", "텀블러", "tumbler", "텀블러"),
            new("accessory", "acc_cane", "지팡이", "cane", "지팡이"),
            new("accessory", "acc_white_cane", "흰지팡이", "white_cane", "흰지팡이"),
            new("accessory", "acc_straw_hat", "밀짚모자", "straw_hat", "밀짚모자"),
            new("hair", "hair_ponytail", "포니테일", "ponytail", "포니테일"),
            new("hair", "hair_short_gray", "짧은 회색 머리", "short_gray", "회색머리")
        };
        static string Words(string value) => "_" + Regex.Replace(
            Regex.Replace(value ?? "", "([a-z])([A-Z])", "$1_$2").ToLowerInvariant(), "[^a-z0-9가-힣]+", "_").Trim('_') + "_";
        static bool Has(string words, params string[] aliases) => Array.Exists(aliases, a => words.Contains("_" + a + "_"));

        public static CustomerAssetEntry Describe(string name, Persona[] people)
        {
            string words = Words(name);
            var entry = new CustomerAssetEntry { source = name, status = "review" };
            var explicitIds = Array.FindAll(people, p => words.Contains("_" + p.id.ToLowerInvariant() + "_"));
            if (explicitIds.Length > 0)
            {
                if (explicitIds.Length > 1) { entry.description = "서로 다른 페르소나 ID가 함께 있는 파일"; entry.mappingReason = "한 파일에는 하나의 cus_* ID만 지정하세요."; return entry; }
                entry.personaIds = new[] { explicitIds[0].id };
                entry.description = explicitIds[0].name + "용 외형 (파일의 명시적 ID 기준)";
                entry.mappingReason = "파일명에 " + explicitIds[0].id + " 명시";
                entry.height = Height(explicitIds[0]);
                return entry;
            }
            string branchId = Has(words, "miku", "미쿠") ? "cus_miku" :
                Has(words, "trainer", "트레이너", "kim_gyeran", "김계란") ? "cus_kim_gyeran" : "";
            if (branchId != "")
            {
                var branch = Array.Find(people, p => p.id == branchId);
                entry.personaIds = branch == null ? Array.Empty<string>() : new[] { branchId };
                entry.description = branchId == "cus_miku" ? "청년 여성 · 통통한 미쿠 전용 외형" : "중년 남성 · 김계란 전용 외형";
                entry.mappingReason = "사용자 지정 독립 분기만 연결. 기존 연령·성별 범주의 페르소나는 유지.";
                if (branch != null) entry.height = Height(branch);
                return entry;
            }
            string age = Has(words, "elder", "elderly", "senior", "grandpa", "grandma", "노년", "노인", "어르신", "할아버지", "할머니") ? "elder" :
                Has(words, "child", "kid", "아동", "아이") ? "child" :
                Has(words, "teen", "student", "청소년", "학생") ? "teen" :
                Has(words, "middle", "middleaged", "중년") ? "middle" :
                Has(words, "young", "youth", "청년") ? "young" : Has(words, "adult", "성인") ? "adult" : "";
            bool female = Has(words, "f", "female", "woman", "girl", "grandma", "여", "여성", "여자", "할머니");
            bool male = Has(words, "m", "male", "man", "boy", "grandpa", "남", "남성", "남자", "할아버지");
            string role = Has(words, "trainer", "트레이너") ? "cus_trainer" :
                Has(words, "barista", "바리스타") ? "cus_barista" : Has(words, "office", "officeworker", "회사원", "직장인") ? "cus_pangyo" :
                Has(words, "blind", "시각장애인", "white_cane", "흰지팡이") ? "cus_blind" : "";
            if (male && female) { entry.description = "성별 표기가 충돌하는 파일"; entry.mappingReason = "male/female 표기를 확인하세요."; return entry; }
            var matches = new List<Persona>();
            var details = Array.FindAll(Details, d => Has(words, d.aliases));
            foreach (var p in people)
            {
                if (!string.IsNullOrEmpty(p.parentPersonaId)) continue;
                if (role != "" && p.id != role) continue;
                string pa = (p.modelBase ?? "").Contains("elder") ? "elder" : (p.modelBase ?? "").Contains("child") ? "child" :
                    (p.modelBase ?? "").Contains("teen") ? "teen" : (p.ageDescription ?? "").Contains("중년") ? "middle" : "young";
                if (age != "" && age != pa && !(age == "adult" && (pa == "young" || pa == "middle"))) continue;
                if (male && !(p.gender ?? "").Contains("남성")) continue;
                if (female && !(p.gender ?? "").Contains("여성")) continue;
                if (role == "" && !(age == "child" || age == "teen" || age != "" && (male || female))) continue;
                matches.Add(p);
            }
            int Score(Persona p)
            {
                int score = 0;
                foreach (var d in details)
                {
                    string value = d.field == "top" ? p.top : d.field == "hair" ? p.hair : p.accessory;
                    if (Array.IndexOf((value ?? "").Replace(" ", "").Split(','), d.value) >= 0) score++;
                }
                return score;
            }
            int best = 0;
            foreach (var p in matches) best = Math.Max(best, Score(p));
            if (best > 0) matches.RemoveAll(p => Score(p) < best);
            entry.personaIds = matches.ConvertAll(p => p.id).ToArray();
            string ageLabel = age == "elder" ? "노년" : age == "child" ? "아동" : age == "teen" ? "청소년/학생" : age == "middle" ? "중년" : age == "young" ? "청년" : age == "adult" ? "성인" : "연령 미상";
            entry.description = ageLabel + (female ? " 여성" : male ? " 남성" : "") + (role != "" ? " / " + role : "") + " 외형 (파일명 추정)";
            foreach (var detail in details) entry.description += ", " + detail.label;
            entry.mappingReason = matches.Count == 0 ? "이름만으로 연결 불가. cus_* ID 또는 연령·성별을 파일명에 지정하세요." :
                "파일명 연령·성별/역할과 기존 페르소나의 외형 참조 일치. 지역·국적·성격은 추정하지 않음.";
            if (matches.Count > 0) entry.height = Height(matches[0]);
            return entry;
        }

        static float Height(Persona p) => (p.modelBase ?? "").Contains("child") ? 1.2f :
            (p.modelBase ?? "").Contains("teen") ? 1.6f : (p.gender ?? "").Contains("여성") ? 1.64f : 1.75f;
    }

    public static class CafeCustomerCatalog
    {
        const string CatalogResource = "CafeCustomers/CustomerCatalog";
        static CustomerAssetTable table;
        public static void Reload()
        {
            var json = Resources.Load<TextAsset>(CatalogResource);
            table = json == null ? new CustomerAssetTable() : JsonUtility.FromJson<CustomerAssetTable>(json.text);
        }
        public static CustomerAssetEntry Choose(string personaId, System.Random random)
        {
            if (table == null) Reload();
            var matches = Array.FindAll(table?.items ?? Array.Empty<CustomerAssetEntry>(), e =>
                e.status == "ready" && Array.IndexOf(e.personaIds ?? Array.Empty<string>(), personaId) >= 0);
            // Prefer a filename explicitly assigned to one person over a shared body type.
            var specific = Array.FindAll(matches, e => e.personaIds.Length == 1);
            if (specific.Length > 0) matches = specific;
            return matches.Length == 0 ? null : matches[random.Next(matches.Length)];
        }
        public static AnimationClip Clip(string resource, string name) => string.IsNullOrEmpty(resource) ? null :
            Array.Find(Resources.LoadAll<AnimationClip>(resource), c => c.name == name);
    }
}
