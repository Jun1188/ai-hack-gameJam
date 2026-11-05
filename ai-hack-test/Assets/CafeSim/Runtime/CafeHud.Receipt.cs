using System.Collections;
using UnityEngine;

namespace CafeSim
{
    // Closing-time settlement drawn as a printed receipt ("마감 정산") over a blurred still of the cafe.
    // Every number comes from CafeBusinessDay.Ledger, i.e. what actually happened during the day.
    public sealed partial class CafeHud
    {
        static readonly Color receiptPaper = new Color32(251, 246, 232, 255);
        static readonly Color receiptInk = new Color32(29, 58, 92, 255);
        static readonly Color receiptMuted = new Color32(96, 112, 132, 255);
        static readonly Color receiptRed = new Color32(176, 52, 44, 255);
        static readonly Color receiptBox = new Color32(238, 234, 222, 255);
        const float ReceiptWidth = 384, ReceiptPad = 26;

        RenderTexture receiptBackdrop;
        bool backdropRequested, backdropReady, receiptPaint;
        Texture2D zigzagTex, stampTex, softBoxTex, primaryTex, primaryHoverTex, secondaryTex, secondaryHoverTex;
        GUIStyle rText, rSmall, rTiny, rBold, rTitle, rHead, rTotal, rQuote, rStampLabel, rStampGrade, rBoxStyle, rStampStyle, rPrimary, rSecondary;
        Font receiptFont;
        // Shader looks (Assets/CafeSim/Shaders); loaded from Resources so builds include them. Null falls back to flat fills.
        Material paperMat, stampWearMat;

        void ReceiptStyles()
        {
            if (rText != null && zigzagTex != null) return;
            // Malgun Gothic has a real bold face (the receipt leans on bold numbers); other systems keep the game font.
            receiptFont = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor
                ? Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕" }, 16) : null;
            if (receiptFont == null) receiptFont = artText.font;
            rText = RStyle(13, receiptInk, false);
            rSmall = RStyle(11, receiptMuted, false);
            rTiny = RStyle(9, receiptInk, true);
            rBold = RStyle(13, receiptInk, true);
            rTitle = RStyle(34, receiptInk, true);
            rTitle.alignment = TextAnchor.MiddleCenter;
            rHead = RStyle(16, receiptInk, true);
            rTotal = RStyle(23, receiptInk, true);
            rQuote = RStyle(13, receiptInk, true);
            rStampLabel = RStyle(10, receiptRed, true);
            rStampLabel.alignment = TextAnchor.MiddleCenter;
            rStampGrade = RStyle(30, receiptRed, true);
            rStampGrade.alignment = TextAnchor.MiddleCenter;
            zigzagTex = Zigzag(14, 7, receiptPaper);
            stampTex = Rounded(32, 5, new Color(0, 0, 0, 0), receiptRed, 2.6f);
            softBoxTex = Rounded(32, 7, receiptBox, new Color32(222, 216, 200, 255), 1.2f);
            rBoxStyle = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
            rBoxStyle.normal.background = softBoxTex;
            rStampStyle = new GUIStyle { border = new RectOffset(9, 9, 9, 9) };
            rStampStyle.normal.background = stampTex;
            // Buttons in the receipt's own ink: a filled primary action and an outlined secondary one.
            primaryTex = Rounded(32, 8, receiptInk, receiptInk, 0);
            primaryHoverTex = Rounded(32, 8, Color.Lerp(receiptInk, Color.white, .16f), receiptInk, 0);
            secondaryTex = Rounded(32, 8, receiptPaper, receiptInk, 2);
            secondaryHoverTex = Rounded(32, 8, receiptBox, receiptInk, 2);
            rPrimary = ReceiptButton(primaryTex, primaryHoverTex, receiptPaper);
            rSecondary = ReceiptButton(secondaryTex, secondaryHoverTex, receiptInk);
            paperMat = LoadMaterial("CafeShaders/ReceiptPaper");
            stampWearMat = LoadMaterial("CafeShaders/StampWear");
        }

        static Material LoadMaterial(string path)
        {
            var source = Resources.Load<Material>(path);
            return source != null && source.shader.isSupported ? new Material(source) : null;
        }

        GUIStyle ReceiptButton(Texture2D normal, Texture2D hover, Color text)
        {
            var style = RStyle(15, text, true);
            style.alignment = TextAnchor.MiddleCenter;
            style.clipping = TextClipping.Clip;
            style.border = new RectOffset(10, 10, 10, 10);
            style.normal.background = normal;
            style.hover.background = style.active.background = hover;
            style.hover.textColor = style.active.textColor = text;
            return style;
        }

        GUIStyle RStyle(int size, Color color, bool bold)
        {
            var style = new GUIStyle { font = receiptFont, fontSize = size, wordWrap = false, alignment = TextAnchor.MiddleLeft,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, clipping = TextClipping.Overflow };
            style.normal.textColor = color;
            return style;
        }

        void DestroyReceiptTextures()
        {
            foreach (var t in new[] { zigzagTex, stampTex, softBoxTex, primaryTex, primaryHoverTex, secondaryTex, secondaryHoverTex }) if (t != null) Destroy(t);
            if (receiptBackdrop != null) { receiptBackdrop.Release(); Destroy(receiptBackdrop); }
            if (paperMat != null) Destroy(paperMat);
            if (stampWearMat != null) Destroy(stampWearMat);
        }

        // ----- Blurred backdrop: one capture at closing time, downsampled so bilinear scaling blurs it -----

        void DrawClosing()
        {
            if (!backdropReady)
            {
                if (!backdropRequested) { backdropRequested = true; StartCoroutine(CaptureBackdrop()); }
                return;   // nothing drawn on the capture frame, so the still shows the cafe itself
            }
            if (Event.current.type == EventType.Repaint)
            {
                var matrix = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                var screen = new Rect(0, 0, Screen.width, Screen.height);
                if (receiptBackdrop != null) GUI.DrawTexture(screen, receiptBackdrop, ScaleMode.ScaleAndCrop);
                var old = GUI.color;
                GUI.color = new Color(.1f, .06f, .03f, .32f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = old;
                GUI.matrix = matrix;
            }
            DrawReceipt();
        }

        IEnumerator CaptureBackdrop()
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                var previous = RenderTexture.active;
                int w = shot.width, h = shot.height;
                var quarter = RenderTexture.GetTemporary(Mathf.Max(4, w / 4), Mathf.Max(4, h / 4));
                var twelfth = RenderTexture.GetTemporary(Mathf.Max(4, w / 12), Mathf.Max(4, h / 12));
                quarter.filterMode = twelfth.filterMode = FilterMode.Bilinear;
                receiptBackdrop = new RenderTexture(Mathf.Max(4, w / 24), Mathf.Max(4, h / 24), 0) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                Graphics.Blit(shot, quarter);
                Graphics.Blit(quarter, twelfth);
                Graphics.Blit(twelfth, receiptBackdrop);
                RenderTexture.ReleaseTemporary(quarter);
                RenderTexture.ReleaseTemporary(twelfth);
                RenderTexture.active = previous;
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[Cafe] 정산 배경 캡처 실패, 어두운 배경으로 대체: " + error.Message);
            }
            finally
            {
                if (shot != null) Destroy(shot);
                backdropReady = true;
            }
        }

        // ----- Receipt -----

        void DrawReceipt()
        {
            ReceiptStyles();
            receiptPaint = false;
            float height = LayoutReceipt(new Rect(0, 0, ReceiptWidth, 0));
            // Receipt and the button row below it are centred as one group. A long day's receipt is
            // shrunk as a whole so the group always fits the 720-high layout.
            const float margin = 18, gap = 18, buttonHeight = 46;
            float fit = Mathf.Min(1f, (720 - margin * 2 - gap - buttonHeight) / (height + 7));
            float group = (height + 7) * fit + gap + buttonHeight, top = Mathf.Max(margin, (720 - group) * .5f);
            var pivot = new Vector2(640, top + (height + 7) * fit * .5f);
            var paper = new Rect(pivot.x - ReceiptWidth * .5f, pivot.y - (height + 7) * .5f, ReceiptWidth, height);
            var matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * fit, pivot);
            receiptPaint = Event.current.type == EventType.Repaint;
            if (receiptPaint)
            {
                Fill(new Rect(paper.x + 2, paper.y + 4, paper.width + 2, paper.height + 6), new Color(0, 0, 0, .16f));
                Fill(new Rect(paper.x + 1, paper.y + 2, paper.width, paper.height + 4), new Color(0, 0, 0, .12f));
                if (paperMat != null)
                {
                    paperMat.SetVector("_Size", new Vector4(paper.width, paper.height, 0, 0));
                    paperMat.SetFloat("_Seed", CafeBusinessDay.Day);
                    Graphics.DrawTexture(paper, Texture2D.whiteTexture, paperMat);
                }
                else Fill(paper, receiptPaper);
                GUI.DrawTextureWithTexCoords(new Rect(paper.x, paper.yMax, paper.width, 7), zigzagTex,
                    new Rect(0, 0, paper.width / 14f, 1));
            }
            LayoutReceipt(paper);
            GUI.matrix = matrix;

            // Two equal buttons spanning the receipt's width: secondary on the left, primary on the right.
            float rowWidth = ReceiptWidth * fit, buttonGap = 12, buttonWidth = (rowWidth - buttonGap) * .5f;
            float bx = 640 - rowWidth * .5f, by = top + (height + 7) * fit + gap;
            // On the day the last customer became a regular, dismissing the receipt saves the pending ending
            // (it plays next morning, even if the game is quit in between).
            var collection = CafeCollection.Instance;
            if (GUI.Button(new Rect(bx, by, buttonWidth, buttonHeight), "메인화면으로", rSecondary))
            {
                collection.MarkEndingPending(CafeBusinessDay.Day + 1);
                businessDay.ExitToMenu();
            }
            if (GUI.Button(new Rect(bx + buttonWidth + buttonGap, by, buttonWidth, buttonHeight), "다음 영업일  →", rPrimary))
            {
                if (collection.EndingDue) BeginEnding();
                else businessDay.NextDay();
            }
            var hint = Center(Derive(rSmall, 12));
            hint.normal.textColor = new Color(1, .95f, .85f, .9f);
            GUI.Label(new Rect(bx, by + buttonHeight + 2, rowWidth, 16), "Q  영업 수첩 보기", hint);
        }

        // Lays out (and, when receiptPaint is set, draws) the receipt; returns its height.
        float LayoutReceipt(Rect paper)
        {
            var ledger = businessDay.Ledger;
            int day = CafeBusinessDay.Day;
            float x = paper.x + ReceiptPad, w = paper.width - ReceiptPad * 2, y = paper.y + 22;

            Rule(x, y, w, 1.6f); y += 6;
            RText(new Rect(x, y, w, 16), "마감 정산", rTiny);
            RText(new Rect(x, y, w, 16), "NO. " + day.ToString("0000"), rTiny, TextAnchor.MiddleRight);
            y += 18;
            RText(new Rect(x, y, w, 46), "모두의 카페", rTitle);
            y += 54;

            Dots(x, y, w); y += 10;
            float c1 = x, c2 = x + w * .27f, c3 = x + w * .62f;
            RText(new Rect(c1, y, 90, 14), "영업일", rSmall);
            RText(new Rect(c2, y, 90, 14), "근무자", rSmall);
            RText(new Rect(c3, y, 120, 14), "영업 시간", rSmall);
            y += 17;
            RText(new Rect(c1, y, 90, 20), day + "일차", rHeadSmall);
            RText(new Rect(c2, y, 90, 20), "알바생", rHeadSmall);
            RText(new Rect(c3, y, 140, 20), Hour(businessDay.openHour) + " ~ " + Hour(businessDay.closeHour), rHeadSmall);
            y += 30;

            Dots(x, y, w); y += 10;
            RText(new Rect(x, y, w, 24), "주문 내역", rHead);
            RText(new Rect(x, y, w, 24), ledger.Orders.Count + "건  /  원", rBoldSmall, TextAnchor.MiddleRight);
            y += 25;
            RText(new Rect(x, y, w, 14), "메뉴", rSmall);
            RText(new Rect(x, y, w, 14), "결제 금액", rSmall, TextAnchor.MiddleRight);
            y += 18;
            Dots(x, y, w); y += 8;

            const int shown = 3;
            if (ledger.Orders.Count == 0)
            {
                RText(new Rect(x, y, w, 20), "오늘은 주문이 없었어요", rText);
                y += 21;
            }
            for (int i = 0; i < ledger.Orders.Count && i < shown; i++)
            {
                var o = ledger.Orders[i];
                bool failed = !o.success;
                string label = o.label + (o.timedOut ? " · 떠남" : failed ? " · 실패" : "");
                RText(new Rect(x, y, w - 70, 20), label, failed ? rFail : rText);
                RText(new Rect(x, y, w, 20), o.paid.ToString("N0"), rBold, TextAnchor.MiddleRight);
                y += 21;
            }
            if (ledger.Orders.Count > shown)
            {
                int rest = ledger.Orders.Count - shown, restOk = 0, restPaid = 0;
                for (int i = shown; i < ledger.Orders.Count; i++)
                {
                    if (ledger.Orders[i].success) restOk++;
                    restPaid += ledger.Orders[i].paid;
                }
                RText(new Rect(x, y, w - 70, 20), $"외 {rest}건 (성공 {restOk}건 / 실패 {rest - restOk}건)", rText);
                RText(new Rect(x, y, w, 20), restPaid.ToString("N0"), rBold, TextAnchor.MiddleRight);
                y += 21;
            }
            y += 4;

            Dots(x, y, w); y += 10;
            if (ledger.BestCustomer(out string personaId, out string bestName, out int bestCount))
            {
                var box = new Rect(x, y, w, 50);
                if (receiptPaint) rBoxStyle.Draw(box, false, false, false, false);
                var icon = Resources.Load<Texture2D>("CustomerIcons/" + personaId);
                if (icon != null && receiptPaint) GUI.DrawTexture(new Rect(box.x + 7, box.y + 6, 38, 38), icon, ScaleMode.ScaleToFit);
                float tx = box.x + (icon != null ? 54 : 12);
                RText(new Rect(tx, box.y + 6, w - 60, 14), "가장 많이 응대 성공한 손님", rSmall);
                RText(new Rect(tx, box.y + 22, w - 60, 22), bestName, rHeadSmall);
                RText(new Rect(box.x, box.y + 22, box.width - 12, 22), "성공 " + bestCount + "회", rBold, TextAnchor.MiddleRight);
                y += 60;
                Dots(x, y, w); y += 10;
            }

            // 오늘 배운 말: phrases added to the 동네 사전 today (they show as NEW in the notebook).
            var learned = CafeCollection.Instance.LearnedToday;
            RText(new Rect(x, y, w, 20), "오늘 배운 말", rHeadSmall);
            RText(new Rect(x, y, w, 20), learned.Count + "개", rBoldSmall, TextAnchor.MiddleRight);
            y += 23;
            if (learned.Count == 0)
            {
                RText(new Rect(x, y, w, 18), "오늘은 새로 배운 말이 없어요", rSmall);
                y += 20;
            }
            for (int i = 0; i < learned.Count && i < 3; i++)
            {
                RText(new Rect(x, y, w * .6f, 19), "“" + learned[i].text + "”", rBold);
                RText(new Rect(x, y, w, 19), learned[i].meaning, rText, TextAnchor.MiddleRight);
                y += 20;
            }
            if (learned.Count > 3) { RText(new Rect(x, y, w, 18), $"외 {learned.Count - 3}개는 영업 수첩에", rSmall); y += 20; }
            y += 4;
            Dots(x, y, w); y += 10;

            Row(ref y, x, w, "매출", Won(ledger.Sales));
            Row(ref y, x, w, "팁", "+" + Won(ledger.Tips));
            Row(ref y, x, w, $"재료 손실 (버린 음료 {ledger.WastedCups}잔)", ledger.WasteCost > 0 ? "-" + Won(ledger.WasteCost) : Won(0));
            y += 5;
            Rule(x, y, w, 1.4f); Rule(x, y + 3.5f, w, 1.4f); y += 12;
            RText(new Rect(x, y, w, 30), "총 수익", rHead);
            RText(new Rect(x, y - 2, w, 32), (ledger.Profit < 0 ? "-" : "") + Won(Mathf.Abs(ledger.Profit)), rTotal, TextAnchor.MiddleRight);
            y += 38;

            Dots(x, y, w); y += 8;
            Row(ref y, x, w, "응대 손님 수", $"{ledger.Orders.Count}명 (성공 {ledger.Successes} / 실패 {ledger.Failures})");
            Row(ref y, x, w, "평균 응대 시간", Mathf.RoundToInt(ledger.AverageServiceSeconds) + "초");
            Row(ref y, x, w, "되물은 횟수", ledger.Questions + "회");
            Row(ref y, x, w, "넘겨짚은 말", ledger.Guesses + "회");
            y += 4;

            Dots(x, y, w); y += 10;
            RText(new Rect(x, y, w, 14), "오늘의 한마디", rSmall);
            RText(new Rect(x, y + 20, w - 90, 22), "“" + ledger.Quote + "”", rQuote);
            Stamp(new Rect(x + w - 80, y - 2, 78, 60), ledger.Grade);
            y += 66;

            Dots(x, y, w); y += 12;
            Barcode(new Rect(x + w * .5f - 78, y, 156, 26), day);
            y += 30;
            RText(new Rect(x, y, w, 12), "MODU CAFE   /   DAY " + day.ToString("0000"), rTinyCenter);
            y += 15;
            RText(new Rect(x, y, w, 18), CafeCollection.Instance.EndingDue ? "내일 아침, 카페 앞으로 와 주세요" : "다음 영업일에 또 만나요", rBoldCenter);
            y += 30;
            return y - paper.y;
        }

        GUIStyle rHeadSmall => rHeadSmallStyle ??= Derive(rBold, 15);
        GUIStyle rBoldSmall => rBoldSmallStyle ??= Derive(rBold, 11);
        GUIStyle rFail => rFailStyle ??= Tint(rText, receiptRed);
        GUIStyle rTinyCenter => rTinyCenterStyle ??= Center(rTiny);
        GUIStyle rBoldCenter => rBoldCenterStyle ??= Center(Derive(rBold, 12));
        GUIStyle rHeadSmallStyle, rBoldSmallStyle, rFailStyle, rTinyCenterStyle, rBoldCenterStyle;
        static GUIStyle Derive(GUIStyle s, int size) => new GUIStyle(s) { fontSize = size };
        static GUIStyle Tint(GUIStyle s, Color c) { var t = new GUIStyle(s); t.normal.textColor = c; return t; }
        static GUIStyle Center(GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };

        void Row(ref float y, float x, float w, string label, string value)
        {
            RText(new Rect(x, y, w, 21), label, rText);
            RText(new Rect(x, y, w, 21), value, rBold, TextAnchor.MiddleRight);
            y += 22;
        }

        void RText(Rect rect, string value, GUIStyle style, TextAnchor? align = null)
        {
            if (!receiptPaint) return;
            if (align.HasValue && style.alignment != align.Value)
            {
                var anchor = style.alignment;
                style.alignment = align.Value;
                GUI.Label(rect, value, style);
                style.alignment = anchor;
            }
            else GUI.Label(rect, value, style);
        }

        void Rule(float x, float y, float w, float thickness)
        {
            if (receiptPaint) Fill(new Rect(x, y, w, thickness), receiptInk);
        }

        void Dots(float x, float y, float w)
        {
            if (!receiptPaint) return;
            var dot = new Color(receiptInk.r, receiptInk.g, receiptInk.b, .55f);
            for (float d = 0; d < w; d += 4.2f) Fill(new Rect(x + d, y, 1.6f, 1.6f), dot);
        }

        void Stamp(Rect rect, string grade)
        {
            if (!receiptPaint) return;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-9, rect.center);
            var old = GUI.color;
            // Bleed: the ink soaks a little into the paper, so a faint halo is printed first.
            var offsets = new[] { new Vector2(-.9f, 0), new Vector2(.9f, 0), new Vector2(0, -.9f), new Vector2(0, .9f),
                new Vector2(.7f, .7f), new Vector2(-.7f, -.7f) };
            GUI.color = new Color(1, 1, 1, .16f);
            foreach (var o in offsets) StampInk(new Rect(rect.position + o, rect.size), grade);
            GUI.color = new Color(1, 1, 1, .88f);
            StampInk(rect, grade);
            GUI.color = old;
            // Wear: paper shows through where the rubber did not take ink.
            if (stampWearMat != null)
            {
                stampWearMat.SetVector("_Size", new Vector4(rect.width, rect.height, 0, 0));
                stampWearMat.SetFloat("_Seed", CafeBusinessDay.Day * 3.1f + (grade.Length > 0 ? grade[0] % 7 : 0));
                Graphics.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Texture2D.whiteTexture, stampWearMat);
            }
            GUI.matrix = matrix;
        }

        void StampInk(Rect rect, string grade)
        {
            rStampStyle.Draw(rect, false, false, false, false);
            rStampStyle.Draw(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), false, false, false, false);
            // GUI.color (set by the caller) tints the labels too, giving the halo and the main print their strength.
            GUI.Label(new Rect(rect.x, rect.y + 7, rect.width, 14), "응대 등급", rStampLabel);
            GUI.Label(new Rect(rect.x, rect.y + 20, rect.width, 36), grade, rStampGrade);
        }

        // Decorative barcode, stable per day number.
        void Barcode(Rect rect, int seed)
        {
            if (!receiptPaint) return;
            var random = new System.Random(seed * 7919 + 17);
            for (float bx = rect.x; bx < rect.xMax;)
            {
                float bar = random.Next(1, 4) * .9f;
                Fill(new Rect(bx, rect.y, Mathf.Min(bar, rect.xMax - bx), rect.height), receiptInk);
                bx += bar + random.Next(1, 3) * 1.1f;
            }
        }

        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string Hour(float hour) => Mathf.FloorToInt(hour).ToString("00") + ":" + Mathf.RoundToInt((hour % 1) * 60).ToString("00");

        // Torn-paper teeth along the bottom edge (tiled horizontally).
        static Texture2D Zigzag(int w, int h, Color fill)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fromTop = h - 1 - y + .5f;
                float half = w * .5f, edge = (1 - Mathf.Abs(x + .5f - half) / half) * h;
                var c = fill;
                c.a = Mathf.Clamp01(edge - fromTop + .5f);
                pixels[y * w + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
