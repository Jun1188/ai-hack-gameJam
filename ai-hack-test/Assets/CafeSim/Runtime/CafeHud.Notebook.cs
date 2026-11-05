using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    // 영업 수첩 (Q): 단골 장부 and 동네 사전, drawn on the open-book art. Layout coordinates are written in
    // the 1672x941 reference image's pixels and converted to the HUD's 1280x720 layout by N().
    public sealed partial class CafeHud
    {
        const float BookScale = 1280f / 1672f;
        static readonly string[] PhraseTags = { "전체", "지역 말", "신조어", "외국어", "옛말", "전문 용어", "기타" };
        static readonly Color nbInk = new Color32(29, 58, 92, 255);
        static readonly Color nbMuted = new Color32(122, 106, 84, 255);
        static readonly Color nbGreen = new Color32(78, 104, 70, 255);
        static readonly Color nbBrown = new Color32(118, 92, 72, 255);
        static readonly Color nbRed = new Color32(163, 58, 42, 255);
        static readonly Color nbChip = new Color32(238, 224, 194, 255);
        static readonly Color nbSelect = new Color32(226, 233, 207, 255);
        static readonly Color nbPlayer = new Color32(221, 232, 238, 255);
        static readonly Color nbTrack = new Color32(228, 214, 186, 255);

        bool notebookOpen, notebookPausedDay, notebookAutoChecked, endingAfterNotebook;
        int notebookTab, phraseFilter, phraseCustomerFilter = -1;
        string selectedCustomer, selectedPhrase;
        float notebookOpenedAt, phraseScroll;
        Texture2D bookTex, silhouetteTex, chipTex, roundTex;
        GUIStyle nbStyle, nbRound;

        // ----- Opening and closing -----

        void ToggleNotebook()
        {
            if (notebookOpen)
            {
                notebookOpen = false;
                CafeCollection.Instance.MarkSeen();
                if (endingAfterNotebook)
                {
                    // The 16th stamp has been seen: the night passes and the ending begins.
                    endingAfterNotebook = false;
                    CafeEnding.Play(CafeEnding.Mode.Full, CafeCollection.Instance.EndingDay);
                    return;
                }
                if (notebookPausedDay) { notebookPausedDay = false; Resume(); }
                return;
            }
            if (promptOpen) SetPrompt(false);
            notebookOpen = true;
            notebookOpenedAt = Time.unscaledTime;
            phraseScroll = 0;
            var collection = CafeCollection.Instance;
            // Land on whatever is new: a fresh regular first, otherwise the newest phrase.
            var fresh = collection.Customers.Find(c => collection.Record(c.id).newRegular);
            bool newPhrase = false;
            foreach (var p in collection.Phrases) if (p.isNew) { newPhrase = true; selectedPhrase = p.id; }
            notebookTab = fresh == null && newPhrase ? 1 : 0;
            if (fresh != null) selectedCustomer = fresh.id;
            if (selectedCustomer == null && collection.Customers.Count > 0) selectedCustomer = collection.Customers[0].id;
            if (!businessDay.Closed && !businessDay.Paused) { businessDay.SetPaused(true); notebookPausedDay = true; }
        }

        // New regulars and phrases from yesterday open the notebook once at the start of the next day.
        void CheckNotebookNews()
        {
            if (notebookAutoChecked || businessDay == null || session == null || session.Queue == null) return;
            notebookAutoChecked = true;
            var collection = CafeCollection.Instance;
            if (collection.EndingPending && !CafeEnding.Active)
            {
                // A saved ending replaces this business day: the morning starts with everyone outside.
                businessDay.SetPaused(true);
                CafeEnding.Play(CafeEnding.Mode.Morning, collection.EndingDay);
                return;
            }
            if (collection.HasNews && !businessDay.Closed && !businessDay.Paused) ToggleNotebook();
        }

        // Final receipt → 단골 장부 with the 16th stamp → (closing it) the ending.
        void BeginEnding()
        {
            CafeCollection.Instance.MarkEndingPending(CafeBusinessDay.Day + 1);
            endingAfterNotebook = true;
            if (!notebookOpen) { ToggleNotebook(); notebookTab = 0; }
        }

        void NotebookTextures()
        {
            if (nbStyle != null) return;
            ReceiptStyles();
            bookTex = Resources.Load<Texture2D>("CafeCollection/notebook_bg");
            silhouetteTex = Silhouette(96);
            chipTex = Rounded(32, 6, Color.white, Color.white, 0);
            roundTex = Rounded(64, 31, Color.white, Color.white, 0);
            nbStyle = new GUIStyle { font = receiptFont, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            nbRound = new GUIStyle { border = new RectOffset(7, 7, 7, 7) };
            nbRound.normal.background = chipTex;
        }

        void DestroyNotebookTextures()
        {
            foreach (var t in new[] { silhouetteTex, chipTex, roundTex }) if (t != null) Destroy(t);
        }

        // ----- Drawing -----

        static Rect N(float x, float y, float w, float h) => new Rect(x * BookScale, y * BookScale, w * BookScale, h * BookScale);

        void Label(float x, float y, float w, float h, string text, float size, Color color, bool bold = false,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (Event.current.type != EventType.Repaint) return;
            nbStyle.fontSize = Mathf.RoundToInt(size * BookScale);
            nbStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            nbStyle.alignment = align;
            nbStyle.normal.textColor = color;
            GUI.Label(N(x, y, w, h), text, nbStyle);
        }

        void Box(float x, float y, float w, float h, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            nbRound.Draw(N(x, y, w, h), false, false, false, false);
            GUI.color = old;
        }

        void Dotted(float x, float y, float w)
        {
            if (Event.current.type != EventType.Repaint) return;
            var dot = new Color(nbMuted.r, nbMuted.g, nbMuted.b, .7f);
            for (float d = 0; d < w; d += 6.5f) Fill(N(x + d, y, 2.2f, 2.2f), dot);
        }

        bool Click(float x, float y, float w, float h) => GUI.Button(N(x, y, w, h), GUIContent.none, GUIStyle.none);

        void Portrait(float cx, float cy, float diameter, string id, bool known)
        {
            if (Event.current.type != EventType.Repaint) return;
            var icon = known ? Resources.Load<Texture2D>("CustomerIcons/" + id) : silhouetteTex;
            if (icon != null) GUI.DrawTexture(N(cx - diameter * .5f, cy - diameter * .5f, diameter, diameter), icon, ScaleMode.ScaleToFit);
        }

        void DrawNotebook()
        {
            NotebookTextures();
            if (Event.current.type == EventType.Repaint)
            {
                // The art is wider than 16:9 by a hair; it is drawn over the whole layout with the table colour behind.
                var matrix = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color32(88, 44, 26, 255));
                GUI.matrix = matrix;
                if (bookTex != null) GUI.DrawTexture(new Rect(0, 0, 1280, 1280f * 941 / 1672), bookTex, ScaleMode.StretchToFill);
            }
            DrawCoverRibbons();

            // Bookmarks on the left edge switch tabs.
            Box(34, 205, 176, 48, notebookTab == 0 ? nbGreen : nbBrown);
            Label(54, 205, 150, 48, "단골 장부", 19, Color.white, true);
            if (Click(34, 205, 176, 48)) notebookTab = 0;
            Box(34, 265, 176, 48, notebookTab == 1 ? nbGreen : nbBrown);
            Label(54, 265, 150, 48, "동네 사전", 19, Color.white, true);
            if (Click(34, 265, 176, 48)) { notebookTab = 1; phraseScroll = 0; }

            Label(1340, 116, 124, 30, "Q · ESC  닫기", 15, nbInk, true, TextAnchor.MiddleRight);
            if (Click(1340, 116, 124, 30)) { ToggleNotebook(); return; }

            if (notebookTab == 0) { DrawRegularsPage(); DrawCustomerRecord(); }
            else { DrawDictionaryPage(); DrawPhraseRecord(); }
        }

        // Every 10 phrases in the dictionary hangs another ribbon from the notebook's top edge (spec 5: cover decoration).
        void DrawCoverRibbons()
        {
            int ribbons = Mathf.Min(3, CafeCollection.Instance.Phrases.Count / 10);
            var colors = new Color[] { nbRed, nbGreen, new Color32(196, 150, 60, 255) };
            for (int i = 0; i < ribbons; i++)
            {
                float x = 1240 + i * 34;
                Box(x, 30, 22, 70, colors[i]);
                Box(x, 30, 22, 14, colors[i] * .8f);
            }
        }

        // ----- 단골 장부 -----

        void DrawRegularsPage()
        {
            var collection = CafeCollection.Instance;
            int regulars = collection.RegularCount, total = collection.Customers.Count;
            Label(232, 118, 400, 26, "모두의 카페   /   영업 수첩", 17, nbMuted);
            DrawStamps();
            Label(232, 150, 360, 64, "단골 장부", 46, nbInk, true);
            Label(560, 178, 226, 34, $"단골 {regulars} / {total}", 22, nbInk, true, TextAnchor.MiddleRight);
            Box(232, 220, 554, 9, nbTrack);
            if (regulars > 0) Box(232, 220, Mathf.Max(12, 554f * regulars / Mathf.Max(1, total)), 9, nbGreen);

            float animate = Mathf.Clamp01((Time.unscaledTime - notebookOpenedAt - .25f) / .35f);
            for (int i = 0; i < total && i < 16; i++)
            {
                var info = collection.Customers[i];
                var record = collection.Record(info.id);
                float x = 232 + (i % 4) * 140, y = 248 + (i / 4) * 129, cx = x + 66;
                if (selectedCustomer == info.id)
                {
                    Box(x - 2, y, 136, 126, new Color32(196, 210, 176, 255));
                    Box(x, y + 2, 132, 122, nbSelect);
                }
                bool known = record.state != "unseen";
                Portrait(cx, y + 40, 72, info.id, known);
                Label(x - 4, y + 78, 140, 22, known ? info.name : "???", 16, nbInk, true, TextAnchor.MiddleCenter);
                if (record.state == "regular")
                {
                    // A regular made since the notebook was last opened gets its stamp pressed in.
                    float s = record.newRegular ? Mathf.Lerp(1.8f, 1f, animate) : 1f;
                    var matrix = GUI.matrix;
                    if (s != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * s, N(cx, y + 113, 0, 0).position);
                    Box(cx - 23, y + 102, 46, 23, nbRed);
                    Label(cx - 23, y + 102, 46, 23, "단골", 14, Color.white, true, TextAnchor.MiddleCenter);
                    GUI.matrix = matrix;
                    if (record.newRegular) Label(cx + 26, y + 102, 40, 23, "NEW", 11, nbRed, true);
                }
                else
                    Label(x, y + 102, 132, 22, known ? "만남" : "미발견", 13, nbMuted, false, TextAnchor.MiddleCenter);
                if (Click(x, y, 132, 126)) selectedCustomer = info.id;
            }

            Dotted(232, 786, 554);
            Label(232, 798, 554, 22, "손님을 선택하면 오른쪽에 기록이 펼쳐집니다.", 15, nbMuted);
            // The ending hint: what happens when every page is filled.
            string hint = collection.AllRegular
                ? "동네 사람 모두가 단골이 되었어요. 곧 이 카페에 특별한 날이 찾아올 것 같아요…"
                : regulars >= total / 2
                    ? $"단골이 {total - regulars}명 남았어요. 모두 모이는 날, 이 카페에 특별한 일이 생길 거예요."
                    : "열여섯 명 모두가 단골이 되는 날, 이 카페에 특별한 일이 생길지도 몰라요…";
            Label(232, 824, 554, 24, hint, 15, collection.AllRegular ? nbRed : nbGreen, true);
        }

        // Group stamps and the hidden "편견 없는 카페" stamp, as small seals beside the breadcrumb.
        void DrawStamps()
        {
            var stamps = CafeCollection.Instance.Stamps;
            float x = 786;
            for (int i = stamps.Count - 1; i >= 0; i--)
            {
                string label = CafeCollection.StampName(stamps[i]);
                float w = 22 + label.Length * 14;
                x -= w + 6;
                Box(x, 116, w, 28, nbRed);
                Box(x + 2, 118, w - 4, 24, new Color32(250, 236, 214, 255));
                Label(x, 116, w, 28, label, 12, nbRed, true, TextAnchor.MiddleCenter);
            }
        }

        void DrawCustomerRecord()
        {
            var collection = CafeCollection.Instance;
            Label(912, 156, 400, 40, "손님 기록", 25, nbInk, true);
            Dotted(912, 208, 552);
            var info = selectedCustomer != null ? collection.Info(selectedCustomer) : null;
            if (info == null) return;
            var r = collection.Record(info.id);
            bool known = r.state != "unseen", regular = r.state == "regular";

            Portrait(970, 288, 112, info.id, known);
            if (!known)
            {
                Label(1050, 240, 420, 52, "???", 40, nbInk, true);
                Label(1050, 292, 420, 30, "아직 만나지 못한 손님이에요.", 19, nbMuted);
                Label(914, 380, 548, 30, "언젠가 이 카페 문을 열고 들어올 거예요.", 19, nbMuted);
                return;
            }
            Label(1050, 232, 420, 56, info.name, 40, nbInk, true);
            float bx = 1050;
            if (regular) { Box(bx, 289, 56, 26, nbRed); Label(bx, 289, 56, 26, "단골", 14, Color.white, true, TextAnchor.MiddleCenter); bx += 70; }
            Label(bx, 287, 360, 30, $"방문 {r.visits}  ·  만족 {r.satisfied}", 20, nbInk, true);

            string locked = $"단골이 되면 알 수 있어요 (만족 {r.satisfied}/{CafeCollection.RegularVisitsNeeded})";
            Row(372, "말하는 방식", info.style, true);
            Row(421, "마지막 음료", collection.DrinkLabel(r.lastMenu, r.lastTemp), true);
            Row(470, "좋아하는 맛", regular ? collection.FavouriteTaste(r) : locked, regular);
            Row(519, "반전 요소", regular ? collection.Twist(info.id) : locked, regular);

            Dotted(912, 562, 552);
            var all = collection.SituationsOf(info.id);
            int heard = 0;
            foreach (var s in all) if (r.situationsHeard.Contains(s.id)) heard++;
            Label(914, 572, 300, 36, "들었던 사정", 23, nbInk, true);
            if (all.Count > 0 && heard == all.Count)
            {
                Box(1140, 578, 186, 26, nbGreen);
                Label(1140, 578, 186, 26, "속사정까지 아는 사이", 13, Color.white, true, TextAnchor.MiddleCenter);
            }
            Label(1340, 572, 124, 36, $"{heard} / {all.Count}", 17, nbInk, true, TextAnchor.MiddleRight);
            float y = 614;
            foreach (var s in all)
            {
                bool got = r.situationsHeard.Contains(s.id);
                Label(914, y, 30, 30, got ? "●" : "○", 18, got ? nbInk : nbMuted, false, TextAnchor.MiddleCenter);
                Label(948, y, 510, 30, got ? s.context : "아직 듣지 못한 사정", 18, got ? nbInk : nbMuted);
                y += 36;
            }

            float quoteY = Mathf.Max(y + 8, 706);
            Dotted(912, quoteY, 552);
            Label(914, quoteY + 14, 400, 26, "손님의 한마디", 16, nbMuted);
            Label(914, quoteY + 44, 552, 36,
                regular && !string.IsNullOrEmpty(r.regularLine) ? "“" + r.regularLine + "”" : "단골이 되는 날 들을 수 있어요.",
                regular ? 22 : 18, regular ? nbInk : nbMuted, regular);
        }

        void Row(float y, string label, string value, bool open)
        {
            Label(914, y, 160, 30, label, 15, nbMuted);
            Label(1080, y, 384, 30, string.IsNullOrEmpty(value) ? "-" : value, open ? 19 : 15, open ? nbInk : nbMuted, open);
        }

        // ----- 동네 사전 -----

        List<PhraseRecord> FilteredPhrases()
        {
            var collection = CafeCollection.Instance;
            var list = new List<PhraseRecord>();
            string customer = CustomerFilterId();
            for (int i = collection.Phrases.Count - 1; i >= 0; i--)   // newest first
            {
                var p = collection.Phrases[i];
                if (phraseFilter > 0 && p.tag != PhraseTags[phraseFilter]) continue;
                if (customer != null && p.customer != customer) continue;
                list.Add(p);
            }
            return list;
        }

        // Customers that have at least one phrase, for the "손님별" filter.
        List<string> PhraseCustomers()
        {
            var ids = new List<string>();
            foreach (var p in CafeCollection.Instance.Phrases) if (!ids.Contains(p.customer)) ids.Add(p.customer);
            return ids;
        }

        string CustomerFilterId()
        {
            var ids = PhraseCustomers();
            return phraseCustomerFilter >= 0 && phraseCustomerFilter < ids.Count ? ids[phraseCustomerFilter] : null;
        }

        void DrawDictionaryPage()
        {
            var collection = CafeCollection.Instance;
            Label(232, 118, 400, 26, "모두의 카페   /   영업 수첩", 17, nbMuted);
            Label(232, 150, 360, 64, "동네 사전", 46, nbInk, true);
            Label(560, 178, 226, 34, $"표현 {collection.Phrases.Count}개", 22, nbInk, true, TextAnchor.MiddleRight);

            float x = 232;
            for (int i = 0; i < PhraseTags.Length; i++)
            {
                float w = 30 + PhraseTags[i].Length * 13;
                bool on = phraseFilter == i;
                Box(x, 220, w, 26, on ? nbGreen : nbChip);
                Label(x, 220, w, 26, PhraseTags[i], 13, on ? Color.white : nbInk, on, TextAnchor.MiddleCenter);
                if (Click(x, 220, w, 26)) { phraseFilter = i; phraseScroll = 0; }
                x += w + 10;
            }
            string customer = CustomerFilterId();
            string who = customer == null ? "전체 손님" : collection.Info(customer)?.name ?? customer;
            Label(232, 252, 400, 24, $"손님별  ·  {who}  ▾", 15, nbMuted);
            if (Click(232, 252, 260, 24))
            {
                // Each click moves to the next customer with phrases, then back to everyone.
                phraseCustomerFilter = phraseCustomerFilter + 1 >= PhraseCustomers().Count ? -1 : phraseCustomerFilter + 1;
                phraseScroll = 0;
            }

            var list = FilteredPhrases();
            var area = new Rect(228, 280, 566, 520);
            if (list.Count == 0)
            {
                Label(240, 300, 540, 30, "아직 배운 표현이 없어요.", 19, nbMuted);
                Label(240, 334, 540, 30, "손님의 말을 끝까지 듣고 주문에 성공하면 기록돼요.", 16, nbMuted);
                return;
            }
            const float rowHeight = 83;
            float maxScroll = Mathf.Max(0, list.Count * rowHeight - area.height);
            if (Event.current.type == EventType.ScrollWheel && N(area.x, area.y, area.width, area.height).Contains(Event.current.mousePosition))
            {
                phraseScroll = Mathf.Clamp(phraseScroll + Event.current.delta.y * 18, 0, maxScroll);
                Event.current.Use();
            }
            phraseScroll = Mathf.Clamp(phraseScroll, 0, maxScroll);

            GUI.BeginGroup(N(area.x, area.y, area.width, area.height));
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                float y = i * rowHeight - phraseScroll;
                if (y + rowHeight < 0 || y > area.height) continue;
                if (selectedPhrase == null) selectedPhrase = p.id;
                bool selected = p.id == selectedPhrase;
                if (selected || p.byPlayer) Box(0, y + 2, area.width - 4, rowHeight - 6, p.byPlayer ? nbPlayer : nbSelect);
                Label(12, y + 8, 380, 34, "“" + p.text + "”", 22, nbInk, true);
                Label(330, y + 8, 222, 34, p.meaning, 18, nbInk, true, TextAnchor.MiddleRight);
                string name = collection.Info(p.customer)?.name ?? p.customer;
                Label(12, y + 44, 400, 26, $"{name} · {collection.DrinkLabel(p.menu, p.temp)}", 14, nbMuted);
                Label(352, y + 44, 200, 26, p.byPlayer ? "내가 설명한 말" : p.tag, 13, nbInk, p.byPlayer, TextAnchor.MiddleRight);
                if (p.isNew) Label(440, y + 44, 60, 26, "NEW", 11, nbRed, true);
                if (!selected && !p.byPlayer) Dotted(6, y + rowHeight - 4, area.width - 16);
                if (Click(0, y, area.width, rowHeight)) selectedPhrase = p.id;
            }
            GUI.EndGroup();
            if (maxScroll > 0)
            {
                float bar = area.height * area.height / (list.Count * rowHeight);
                Box(area.xMax + 4, area.y + (area.height - bar) * phraseScroll / maxScroll, 6, bar, new Color(nbMuted.r, nbMuted.g, nbMuted.b, .5f));
            }
        }

        void DrawPhraseRecord()
        {
            var collection = CafeCollection.Instance;
            Label(912, 156, 400, 40, "표현 기록", 25, nbInk, true);
            Dotted(912, 208, 552);
            PhraseRecord p = null;
            foreach (var q in collection.Phrases) if (q.id == selectedPhrase) p = q;
            if (p == null)
            {
                Label(914, 240, 548, 30, "왼쪽에서 표현을 고르면 기록이 펼쳐집니다.", 18, nbMuted);
                return;
            }
            float x = 914;
            string tag = p.byPlayer ? "내가 설명한 말" : p.tag;
            float tw = 30 + tag.Length * 13;
            Box(x, 236, tw, 26, p.byPlayer ? new Color32(70, 110, 140, 255) : nbGreen);
            Label(x, 236, tw, 26, tag, 13, Color.white, true, TextAnchor.MiddleCenter);
            if (p.isNew) { Box(x + tw + 8, 236, 52, 26, nbRed); Label(x + tw + 8, 236, 52, 26, "NEW", 12, Color.white, true, TextAnchor.MiddleCenter); }

            float textSize = p.text.Length > 12 ? 36 : 52;
            Label(912, 268, 552, 76, "“" + p.text + "”", textSize, nbInk, true);
            Label(914, 346, 552, 46, p.meaning, 30, nbBrown, true);
            Dotted(912, 412, 552);

            var info = collection.Info(p.customer);
            Portrait(956, 490, 84, p.customer, true);
            Label(1020, 446, 300, 22, p.byPlayer ? "내 설명을 들은 손님" : "쓴 손님", 15, nbMuted);
            Label(1020, 472, 420, 38, info?.name ?? p.customer, 25, nbInk, true);
            Label(914, 566, 170, 30, "그날 주문한 음료", 15, nbMuted);
            Label(1080, 566, 384, 30, collection.DrinkLabel(p.menu, p.temp), 20, nbInk, true);
            Label(914, 616, 170, 30, "기록한 날", 15, nbMuted);
            Label(1080, 616, 384, 30, $"영업 {p.day}일차", 20, nbInk, true);
            Dotted(912, 668, 552);
            Label(914, 688, 300, 26, "주문 단서", 16, nbMuted);
            string clue = p.byPlayer
                ? "손님이 메뉴를 고르게 만든 내 설명이에요."
                : $"손님이 원하는 {CafeCollection.AttrLabel(p.attr)}을 알아낸 표현이에요.";
            Label(914, 720, 552, 36, clue, 22, nbInk, true);
        }

        // Grey head-and-shoulders silhouette for customers not met yet.
        static Texture2D Silhouette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            var bg = new Color32(226, 222, 214, 255);
            var ring = new Color32(196, 190, 180, 255);
            var figure = new Color32(168, 164, 156, 255);
            float r = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + .5f - r, y + .5f - r);
                float d = p.magnitude;
                Color c = d > r - 3 ? (Color)ring : bg;
                var head = p - new Vector2(0, size * .1f);
                var body = p + new Vector2(0, size * .3f);
                if (head.magnitude < size * .17f || (body.magnitude < size * .3f && p.y < -size * .08f)) c = figure;
                c.a = Mathf.Clamp01(r - d);
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
