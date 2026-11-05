using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MapleCafe
{
    [DefaultExecutionOrder(-100)]
    public sealed class CafeMenuBook : MonoBehaviour
    {
        [SerializeField] private BaristaView player;
        [SerializeField] private Texture2D[] cards;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;

        private const float CardDuration = .21f;
        private const float CardDelay = .047f;
        private const float AnimationSpeed = 1.5f;
        private const float StripExitDuration = .24f;
        private const float PinDuration = .26f;
        private const float CardAspect = 384f / 600f;
        private const float TopScreenShare = 1f / 3f;

        private RectTransform canvasRect;
        private RawImage[] views;
        private RawImage pinnedCard;
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

        private void Update()
        {
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
                if (isOpen && !returnTogether && Application.isFocused) HandleCardPointer();
            }

            if (showPinned && stripExitRemaining == 0f)
            {
                if (pinProgress == 0f && dt > 0f) PlayCardSound(openSound);
                pinProgress = Mathf.MoveTowards(pinProgress, 1f, dt / PinDuration);
            }
            else if (!showPinned)
                pinProgress = Mathf.MoveTowards(pinProgress, 0f, dt / PinDuration);

            pinnedCard.enabled = pinProgress > 0f;
            if (pinnedCard.enabled)
                LayoutPinnedCard();
            else if (!showPinned)
                pinnedCard.texture = null;
            canvasRect.gameObject.SetActive(isOpen || waveTime > 0f || showPinned || pinnedCard.enabled);
        }

        private void HandleKeys()
        {
            var keys = Keyboard.current;
            if (keys?.tabKey.wasPressedThisFrame == true)
            {
                if (isOpen)
                {
                    FinishSelection(false);
                    PlayCardSound(closeSound);
                }
                else if (cards != null && cards.Length > 0)
                {
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
            }
            else if (isOpen && keys?.escapeKey.wasPressedThisFrame == true)
            {
                FinishSelection(false);
                PlayCardSound(closeSound);
            }
        }

        private void HandleCardPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 pointer = mouse.position.ReadValue();
            foreach (var view in views)
            {
                if (view.color.a < .55f || !RectTransformUtility.RectangleContainsScreenPoint(view.rectTransform, pointer))
                    continue;

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    pinnedCard.texture = view.texture;
                    showPinned = true;
                    stripExitRemaining = 1f;
                    FinishSelection(true);
                    PlayCardSound(closeSound);
                }
                else
                {
                    view.color *= new Color(.86f, .86f, .86f, 1f);
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
            if (cardAudio != null) cardAudio.Stop();
            if (pinnedCard != null)
            {
                pinnedCard.enabled = false;
                pinnedCard.texture = null;
            }
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

            views = new RawImage[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                views[i] = CreateCardView(cards[i].name);
                views[i].texture = cards[i];
            }
            pinnedCard = CreateCardView("Pinned drink card");
            pinnedCard.enabled = false;
        }

        private RawImage CreateCardView(string name)
        {
            var item = new GameObject(name, typeof(RectTransform));
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(canvasRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            var view = item.AddComponent<RawImage>();
            view.raycastTarget = false;
            return view;
        }

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

            for (int i = 0; i < views.Length; i++)
            {
                float delay = returnTogether && stripExitRemaining == 0f ? 0f : i * CardDelay;
                float progress = Mathf.Clamp01((waveTime - delay) / CardDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                var view = views[i];
                view.enabled = progress > 0f && remaining > 0f;
                view.rectTransform.sizeDelta = new Vector2(width, height);
                view.rectTransform.anchoredPosition = new Vector2(
                    margin + i * (width + gap),
                    Mathf.Lerp(height, -top, eased) + (height + top) * (1f - stripOpacity));
                view.color = new Color(1f, 1f, 1f, eased * stripOpacity);
            }
        }

        private void LayoutPinnedCard()
        {
            Rect viewport = canvasRect.rect;
            float width = Mathf.Min(viewport.width * .23f, viewport.height * .43f * CardAspect);
            float height = width / CardAspect;
            float eased = Mathf.SmoothStep(0f, 1f, pinProgress);
            pinnedCard.rectTransform.sizeDelta = new Vector2(width, height);
            pinnedCard.rectTransform.anchoredPosition = new Vector2(
                viewport.width * .012f, Mathf.Lerp(height * .12f, -viewport.height * .012f, eased));
            pinnedCard.color = new Color(1f, 1f, 1f, eased);
        }

        private void PlayCardSound(AudioClip clip)
        {
            if (clip == null) return;
            if (cardAudio == null)
            {
                cardAudio = gameObject.AddComponent<AudioSource>();
                cardAudio.playOnAwake = false;
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
