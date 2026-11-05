using System;
using CafeSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MapleCafe
{
    [DefaultExecutionOrder(-100)]
    public sealed class CafeMenuBook : MonoBehaviour
    {
        [SerializeField] private BaristaView player;
        public Key toggleKey=Key.Tab;
        [SerializeField] private Texture2D[] cards;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private Font cardFont;
        [SerializeField] private TextAsset cardMenuData;

        private const float CardDuration = .21f;
        private const float CardDelay = .047f;
        private const float AnimationSpeed = 1.5f;
        private const float StripExitDuration = .24f;
        private const float PinDuration = .26f;
        private const float CardAspect = 384f / 600f;
        private const float TopScreenShare = 1f / 3f;
        private const float TemperatureLeft = .05f;
        private const float TemperatureRight = .51f;
        private const float TemperatureWidth = .44f;
        private const float TutorialDimOpacity = .72f;
        private const float TutorialDimFadeTime = .16f;

        private RectTransform canvasRect;
        private CanvasGroup tutorialSpotlight;
        private RectTransform tutorialFrame;
        private RectTransform tutorialDimTop, tutorialDimBottom, tutorialDimLeft, tutorialDimRight;
        private bool tutorialFocusActive;
        private bool tutorialFocusUsed;
        private CardView[] views;
        private CardView pinnedCard;
        private Texture2D beanCursor;
        private AudioSource cardAudio;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private bool isOpen;
        private bool showPinned;
        private bool returnTogether;
        private float waveTime;
        private float stripExitRemaining;
        private float pinProgress;
        internal bool IsListOpen => isOpen;
        internal bool HasSelectedCard => showPinned;
        internal bool HasPinnedCard => showPinned && pinProgress > 0f;
        internal string PinnedDrinkName => showPinned ? pinnedCard?.Item?.name : null;
        internal MenuItem PinnedItem => showPinned ? pinnedCard?.Item : null;
        internal bool AllowConversationToggle { get; set; }
        internal bool HasCards => cards != null && cards.Length > 0;
        internal bool TutorialTabRequired { private get; set; }

        internal void SetTutorialCardFocus(bool choosingFirstCard)
        {
            tutorialFocusActive = choosingFirstCard && isOpen && (tutorialFocusActive || !tutorialFocusUsed);
            tutorialFocusUsed |= tutorialFocusActive;
        }

        private void Update()
        {
            if (CafeStartIris.BlocksInput) return;
            if (Application.isFocused)
                HandleKeys();
            if (canvasRect == null)
                return;

            float dt = Time.unscaledDeltaTime * AnimationSpeed;
            if (stripExitRemaining > 0f)
            {
                stripExitRemaining = Mathf.Max(0f, stripExitRemaining - dt / StripExitDuration);
                if (stripExitRemaining == 0f) waveTime = 0f;
                LayoutCards(stripExitRemaining);
            }
            else if (pinProgress == 0f && (isOpen || waveTime > 0f))
            {
                float end = CardDuration + CardDelay * (views.Length - 1);
                float openTime = returnTogether ? CardDuration : end;
                // Reverse the wave on Tab so a quick toggle does not jump between positions.
                waveTime = Mathf.MoveTowards(waveTime, isOpen ? openTime : 0f, dt);
                LayoutCards(1f);
                if (returnTogether && waveTime == openTime)
                {
                    returnTogether = false;
                    waveTime = end;
                }
                if (isOpen && !returnTogether && !TutorialTabRequired && Application.isFocused) HandleCardPointer();
            }

            if (showPinned && stripExitRemaining == 0f)
            {
                if (pinProgress == 0f && dt > 0f) PlayCardSound(openSound);
                pinProgress = Mathf.MoveTowards(pinProgress, 1f, dt / PinDuration);
            }
            else if (!showPinned)
                pinProgress = Mathf.MoveTowards(pinProgress, 0f, dt / PinDuration);

            bool pinnedVisible = pinProgress > 0f;
            pinnedCard.Rect.gameObject.SetActive(pinnedVisible);
            if (pinnedVisible)
                LayoutPinnedCard();
            float targetOpacity = tutorialFocusActive && isOpen ? 1f : 0f;
            tutorialSpotlight.alpha = Mathf.MoveTowards(tutorialSpotlight.alpha, targetOpacity,
                Time.unscaledDeltaTime / TutorialDimFadeTime);
            canvasRect.gameObject.SetActive(isOpen || waveTime > 0f || showPinned || pinnedVisible ||
                tutorialSpotlight.alpha > 0f);
        }

        private void HandleKeys()
        {
            if (TutorialTabRequired) return;
            var keys = Keyboard.current;
            if (keys!=null && keys[toggleKey].wasPressedThisFrame && (player==null||!player.ExternalUIActive||AllowConversationToggle))
                ToggleList();
            else if (isOpen && keys?.escapeKey.wasPressedThisFrame == true)
            {
                FinishSelection(false);
                PlayCardSound(closeSound);
            }
        }

        internal void ToggleList()
        {
            if (isOpen)
            {
                FinishSelection(false);
                PlayCardSound(closeSound);
                return;
            }
            if (!HasCards) return;

            if (canvasRect == null)
                BuildStrip();
            if (showPinned || pinProgress > 0f)
            {
                showPinned = false;
                returnTogether = true;
            }
            else if (waveTime == 0f)
            {
                returnTogether = false;
            }
            canvasRect.gameObject.SetActive(true);
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            isOpen = true;
            if (player != null) player.SetChoosingMenuCard(true);
            ShowBeanCursor();
            PlayCardSound(openSound);
        }

        private void HandleCardPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 pointer = mouse.position.ReadValue();
            foreach (var view in views)
            {
                if (view.Group.alpha < .55f || !RectTransformUtility.RectangleContainsScreenPoint(view.Rect, pointer))
                    continue;

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    SetCard(pinnedCard, view.Art.texture, view.Item);
                    showPinned = true;
                    stripExitRemaining = 1f;
                    FinishSelection(true);
                    PlayCardSound(closeSound);
                }
                else
                {
                    view.Art.color = new Color(.86f, .86f, .86f);
                }
                break;
            }
        }

        private void FinishSelection(bool pinned)
        {
            isOpen = false;
            if (player != null) player.SetChoosingMenuCard(false);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.lockState = !Application.isFocused ? CursorLockMode.None : pinned ? CursorLockMode.Locked : previousLock;
            Cursor.visible = !Application.isFocused || (!pinned && previousVisible);
        }

        private void ShowBeanCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            beanCursor ??= CreateBeanCursor();
            Cursor.SetCursor(beanCursor, new Vector2(20f, 20f), CursorMode.Auto);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && isOpen) ShowBeanCursor();
        }

        private void OnDisable()
        {
            if (isOpen) FinishSelection(false);
            waveTime = 0f;
            stripExitRemaining = 0f;
            pinProgress = 0f;
            showPinned = false;
            returnTogether = false;
            tutorialFocusActive = false;
            if (tutorialSpotlight != null) tutorialSpotlight.alpha = 0f;
            if (cardAudio != null) cardAudio.Stop();
            if (pinnedCard != null) pinnedCard.Rect.gameObject.SetActive(false);
            if (canvasRect != null) canvasRect.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (beanCursor != null) Destroy(beanCursor);
        }

        private void BuildStrip()
        {
            var canvas = new GameObject("Top drink cards", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(transform, false);
            var overlay = canvas.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 40;
            canvasRect = canvas.GetComponent<RectTransform>();

            MenuItem[] menu = cardMenuData != null ? JsonUtility.FromJson<MenuTable>(cardMenuData.text)?.items : null;
            views = new CardView[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                Texture2D texture = cards[i];
                MenuItem item = menu == null ? null : Array.Find(menu, entry => entry.id == texture.name);
                views[i] = CreateCardView(texture.name);
                SetCard(views[i], texture, item);
            }
            pinnedCard = CreateCardView("Pinned drink card");
            pinnedCard.Rect.gameObject.SetActive(false);
            BuildTutorialSpotlight();
        }

        private sealed class CardView
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public RawImage Art;
            public GameObject Panel;
            public Text Name, HotPrice, IcePrice, Ingredients, IceNote;
            public MenuItem Item;
        }

        private void BuildTutorialSpotlight()
        {
            var layer = AddCardRect(canvasRect, "Tutorial spotlight", new Rect(0f, 0f, 1f, 1f));
            tutorialSpotlight = layer.gameObject.AddComponent<CanvasGroup>();
            tutorialSpotlight.alpha = 0f;
            tutorialDimTop = AddDim("Top shade");
            tutorialDimBottom = AddDim("Bottom shade");
            tutorialDimLeft = AddDim("Left shade");
            tutorialDimRight = AddDim("Right shade");
            tutorialFrame = AddCardRect(layer, "Card list highlight", Rect.zero);
            const float line = 2f;
            AddEdge("Top", new Rect(0f, 1f, 1f, 0f), new Vector2(0f, -line), Vector2.zero);
            AddEdge("Bottom", new Rect(0f, 0f, 1f, 0f), Vector2.zero, new Vector2(0f, line));
            AddEdge("Left", new Rect(0f, 0f, 0f, 1f), Vector2.zero, new Vector2(line, 0f));
            AddEdge("Right", new Rect(1f, 0f, 0f, 1f), new Vector2(-line, 0f), Vector2.zero);

            RectTransform AddDim(string name)
            {
                var dim = AddCardGraphic<Image>(layer, name, Rect.zero);
                dim.color = new Color(0f, 0f, 0f, TutorialDimOpacity);
                return dim.rectTransform;
            }

            void AddEdge(string name, Rect anchors, Vector2 min, Vector2 max)
            {
                var edge = AddCardGraphic<Image>(tutorialFrame, name, anchors);
                edge.color = CafeHud.TutorialHighlightColor;
                edge.rectTransform.offsetMin = min;
                edge.rectTransform.offsetMax = max;
            }
        }

        private void LayoutTutorialSpotlight(float left, float bottom, float right, float top)
        {
            SetAnchors(tutorialFrame, left, bottom, right, top);
            SetAnchors(tutorialDimTop, 0f, top, 1f, 1f);
            SetAnchors(tutorialDimBottom, 0f, 0f, 1f, bottom);
            SetAnchors(tutorialDimLeft, 0f, bottom, left, top);
            SetAnchors(tutorialDimRight, right, bottom, 1f, top);

            static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
            {
                rect.anchorMin = new Vector2(xMin, yMin);
                rect.anchorMax = new Vector2(xMax, yMax);
            }
        }

        private CardView CreateCardView(string name)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)card.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            var view = new CardView { Rect = rect, Group = card.GetComponent<CanvasGroup>() };

            var frame = AddCardGraphic<Image>(rect, "Card frame", new Rect(0f, 0f, 1f, .48f));
            frame.color = new Color32(130, 87, 53, 255);
            var panel = AddCardGraphic<Image>(frame.rectTransform, "Recipe paper", new Rect(.023f, .038f, .954f, .962f));
            panel.color = new Color32(246, 232, 202, 255);
            view.Panel = frame.gameObject;
            view.Art = AddCardGraphic<RawImage>(rect, "Drink illustration", new Rect(0f, .472f, 1f, .528f));
            view.Name = AddCardText(panel.rectTransform, "Drink name", new Rect(.05f, .77f, .9f, .21f), 36);
            view.HotPrice = AddTemperaturePrice(panel.rectTransform, "HOT", new Color32(160, 52, 46, 255), TemperatureLeft);
            view.IcePrice = AddTemperaturePrice(panel.rectTransform, "ICE", new Color32(38, 90, 150, 255), TemperatureRight);
            AddCardText(panel.rectTransform, "Ingredient heading", new Rect(.05f, .45f, .9f, .13f), 19).text = "필요 재료";
            view.Ingredients = AddCardText(panel.rectTransform, "Ingredients", new Rect(.05f, .15f, .9f, .31f), 22);
            view.Ingredients.alignment = TextAnchor.UpperLeft;
            view.IceNote = AddCardText(panel.rectTransform, "Ice base", new Rect(.05f, .025f, .9f, .13f), 18);
            view.IceNote.text = "ICE 기본  ·  얼음 + 물";
            return view;
        }

        private void SetCard(CardView view, Texture texture, MenuItem item)
        {
            view.Item = item;
            view.Art.texture = texture;
            bool renderText = item != null && cardFont != null;
            view.Panel.SetActive(renderText);
            view.Art.rectTransform.anchorMin = new Vector2(0f, renderText ? .472f : 0f);
            view.Art.uvRect = renderText ? new Rect(0f, .472f, 1f, .528f) : new Rect(0f, 0f, 1f, 1f);
            if (!renderText) return;

            view.Name.text = item.name;
            var (hot, ice) = CardPrices(item.id);
            view.HotPrice.transform.parent.gameObject.SetActive(item.hot);
            var iceRow = (RectTransform)view.IcePrice.transform.parent;
            float iceX = item.hot ? TemperatureRight : TemperatureLeft;
            iceRow.anchorMin = new Vector2(iceX, iceRow.anchorMin.y);
            iceRow.anchorMax = new Vector2(iceX + TemperatureWidth, iceRow.anchorMax.y);
            iceRow.gameObject.SetActive(item.ice);
            view.HotPrice.text = hot;
            view.IcePrice.text = ice;
            view.Ingredients.text = "· " + item.recipe.Replace(" + ", "\n· ");
            view.IceNote.enabled = item.ice;
        }

        private Text AddTemperaturePrice(RectTransform parent, string temperature, Color32 color, float x)
        {
            var row = AddCardRect(parent, temperature + " price row", new Rect(x, .60f, TemperatureWidth, .17f));
            var badge = AddCardGraphic<Image>(row, temperature + " badge", new Rect(0f, .12f, .35f, .76f));
            badge.color = color;
            var label = AddCardText(badge.rectTransform, temperature, new Rect(.06f, 0f, .88f, 1f), 21);
            label.text = temperature;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return AddCardText(row, temperature + " price", new Rect(.40f, 0f, .60f, 1f), 25);
        }

        private Text AddCardText(RectTransform parent, string name, Rect bounds, int maxSize)
        {
            var text = AddCardGraphic<Text>(parent, name, bounds);
            text.font = cardFont;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 1;
            text.resizeTextMaxSize = maxSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color32(55, 38, 27, 255);
            return text;
        }

        private static T AddCardGraphic<T>(RectTransform parent, string name, Rect bounds) where T : Graphic
        {
            var rect = AddCardRect(parent, name, bounds);
            var graphic = rect.gameObject.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static RectTransform AddCardRect(RectTransform parent, string name, Rect bounds)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = bounds.min;
            rect.anchorMax = bounds.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static (string hot, string ice) CardPrices(string id) => id switch
        {
            "menu_americano" => ("4,000원", "4,500원"),
            "menu_espresso" => ("3,500원", ""),
            "menu_latte" or "menu_choco" or "menu_yuja" => ("4,500원", "5,000원"),
            "menu_vanilla" or "menu_condensed" or "menu_matcha" => ("5,000원", "5,500원"),
            "menu_strawberry" => ("", "5,500원"),
            "menu_lemonade" => ("", "5,000원"),
            "menu_ssanghwa" => ("5,500원", ""),
            _ => ("", "")
        };

        private void LayoutCards(float remaining)
        {
            Rect viewport = canvasRect.rect;
            float margin = viewport.width * .012f;
            float gap = viewport.width * .004f;
            float top = viewport.height * .012f;
            float availableWidth = viewport.width - margin * 2f - gap * (views.Length - 1);
            float width = Mathf.Min(availableWidth / views.Length, (viewport.height * TopScreenShare - top) * CardAspect);
            float height = width / CardAspect;
            float stripOpacity = Mathf.SmoothStep(0f, 1f, remaining);
            if (tutorialFocusActive || tutorialSpotlight.alpha > 0f)
            {
                const float padding = 6f;
                float rowRight = margin + width * views.Length + gap * (views.Length - 1);
                LayoutTutorialSpotlight(
                    Mathf.Max(0f, margin - padding) / viewport.width,
                    Mathf.Max(0f, 1f - (top + height + padding) / viewport.height),
                    Mathf.Min(1f, (rowRight + padding) / viewport.width),
                    Mathf.Min(1f, 1f - (top - padding) / viewport.height));
            }

            for (int i = 0; i < views.Length; i++)
            {
                float delay = returnTogether && stripExitRemaining == 0f ? 0f : i * CardDelay;
                float progress = Mathf.Clamp01((waveTime - delay) / CardDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                var view = views[i];
                view.Rect.gameObject.SetActive(progress > 0f && remaining > 0f);
                view.Rect.sizeDelta = new Vector2(width, height);
                view.Rect.anchoredPosition = new Vector2(
                    margin + i * (width + gap),
                    Mathf.Lerp(height, -top, eased) + (height + top) * (1f - stripOpacity));
                view.Art.color = Color.white;
                view.Group.alpha = eased * stripOpacity;
            }
        }

        private void LayoutPinnedCard()
        {
            Rect viewport = canvasRect.rect;
            // Reserve the right column; the task/chat panels end at x=920 in the fitted HUD.
            float scale = Mathf.Min(viewport.width / 1280f, viewport.height / 720f);
            Vector2 inset = (viewport.size - new Vector2(1280f, 720f) * scale) * .5f;
            float width = 272f * scale;
            float height = width / CardAspect;
            float eased = Mathf.SmoothStep(0f, 1f, pinProgress);
            pinnedCard.Rect.sizeDelta = new Vector2(width, height);
            pinnedCard.Rect.anchoredPosition = new Vector2(
                viewport.width - inset.x - 24f * scale - width,
                Mathf.Lerp(height, -inset.y - 140f * scale, eased));
            pinnedCard.Group.alpha = eased;
        }

        private void PlayCardSound(AudioClip clip)
        {
            if (clip == null) return;
            if (cardAudio == null)
            {
                cardAudio = gameObject.AddComponent<AudioSource>();
                cardAudio.playOnAwake = false;
                cardAudio.ignoreListenerPause = true;
                cardAudio.spatialBlend = 0f;
                cardAudio.volume = .45f;
            }
            cardAudio.PlayOneShot(clip);
        }

        private static Texture2D CreateBeanCursor()
        {
            const int size = 40;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Roasted coffee bean cursor";
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - 19.5f;
                float dy = y - 19.5f;
                float u = (dx + dy) * .7071f / 17f;
                float v = (dy - dx) * .7071f / 11.5f;
                float radius = Mathf.Sqrt(u * u + v * v);
                if (radius > 1.05f) continue;
                Color color = Color.Lerp(new Color32(153, 88, 48, 255), new Color32(78, 39, 26, 255),
                    Mathf.InverseLerp(.85f, -.90f, v));
                float crease = Mathf.Abs(v - .19f * Mathf.Sin(u * Mathf.PI));
                if (Mathf.Abs(u) < .88f && crease < .13f)
                    color = crease < .065f ? new Color32(241, 183, 122, 255) : new Color32(47, 36, 28, 255);
                if (radius > .85f) color = new Color32(63, 32, 22, 255);
                color.a = Mathf.Clamp01((1.05f - radius) * 14f);
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
