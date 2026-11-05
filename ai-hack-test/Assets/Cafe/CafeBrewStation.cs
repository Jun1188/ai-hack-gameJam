using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MapleCafe
{
    [DefaultExecutionOrder(20)]
    public sealed class CafeBrewStation : MonoBehaviour
    {
        public enum IngredientKind { Shot, Water, Milk, Vanilla, Condensed }

        [Serializable]
        private class Ingredient
        {
            public IngredientKind kind;
            public string label;
            public Color color;
            public Collider target;
            public Transform model;
        }

        [SerializeField] private BaristaView player;
        [SerializeField] private Camera eyes;
        [SerializeField] private Font font;
        [SerializeField] private Ingredient[] ingredients;
        [SerializeField] private Collider mixingTarget;
        [SerializeField] private Transform mixingModel;
        [SerializeField] private Renderer mixingLiquid;
        [SerializeField] private Transform mixingSpindle;
        [SerializeField] private Collider mugTarget;
        [SerializeField] private Renderer mugLiquid;
        [SerializeField] private Collider sinkTarget;
        [SerializeField] private AudioClip pourSound;
        [SerializeField] private AudioClip blendSound;

        private const float Reach = 1.65f;
        private const int Capacity = 8;
        private const int AmericanoIngredients = (1 << (int)IngredientKind.Shot) | (1 << (int)IngredientKind.Water);
        private static readonly Vector3 CarryPosition = new(.122f, -.107f, .34f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock liquidProperties;
        private Ingredient heldIngredient;
        private Transform heldModel;
        private bool holdingBlend, busy;
        private int portions, blendIngredients;
        private Color blendColor;
        private Vector3 mixingStart, spindleStart;
        private Text prompt, status;
        private AudioSource sound;
        private string finishedDrink;
        private Color DrinkColor => blendIngredients == AmericanoIngredients ? new Color(.25f, .105f, .045f) : blendColor;

        private void Awake()
        {
            liquidProperties = new MaterialPropertyBlock();
            mixingStart = mixingModel.localPosition;
            spindleStart = mixingSpindle.localPosition;
            mixingSpindle.gameObject.SetActive(false);
            BuildHud();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.volume = .62f;
        }

        private void Update()
        {
            bool active = player.CanInteract;
            prompt.transform.parent.gameObject.SetActive(active);
            if (!active) return;

            Collider target = Physics.Raycast(eyes.ViewportPointToRay(new Vector3(.5f, .5f)),
                out RaycastHit hit, Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.collider : null;

            var ingredient = Array.Find(ingredients, item => item.target == target);
            prompt.text = busy ? "" : Describe(target, ingredient);
            if (!busy && Keyboard.current?.fKey.wasPressedThisFrame == true)
                Interact(target, ingredient);
        }

        private string Describe(Collider target, Ingredient ingredient)
        {
            if (ingredient != null && !holdingBlend)
                return "F  ·  " + ingredient.label + (heldIngredient == ingredient ? " 내려놓기" : " 집기");
            if (target == mixingTarget && !holdingBlend)
            {
                if (heldIngredient != null) return portions < Capacity ? "F  ·  제조 컵에 넣기" : "제조 컵이 가득 찼어요";
                return portions > 0 ? "F  ·  블렌딩하고 컵 들기" : "제조 컵  ·  재료를 먼저 넣으세요";
            }
            if (target == mugTarget)
                return holdingBlend ? "F  ·  머그잔에 따르기" : "머그잔  ·  블렌딩한 음료를 담는 곳";
            if (target == sinkTarget && (portions > 0 || heldIngredient != null || finishedDrink != null))
                return "F  ·  싱크대에서 비우기";
            return "";
        }

        private void Interact(Collider target, Ingredient ingredient)
        {
            if (target == sinkTarget)
            {
                ReturnHeld();
                ResetMix();
                finishedDrink = null;
                mugLiquid.enabled = false;
                RefreshStatus();
            }
            else if (ingredient != null && !holdingBlend)
            {
                bool returning = heldIngredient == ingredient;
                ReturnHeld();
                if (!returning)
                {
                    heldIngredient = ingredient;
                    Carry(ingredient.model);
                    ingredient.model.gameObject.SetActive(false);
                }
                RefreshStatus();
            }
            else if (target == mixingTarget && !holdingBlend)
            {
                if (heldIngredient != null && portions < Capacity) StartCoroutine(AddIngredient());
                else if (heldIngredient == null && portions > 0) StartCoroutine(Blend());
            }
            else if (target == mugTarget && holdingBlend)
                StartCoroutine(FillMug());
        }

        private IEnumerator AddIngredient()
        {
            busy = true;
            status.text = heldIngredient.label + " 붓는 중";
            yield return TipHeld();
            blendColor = Color.Lerp(blendColor, heldIngredient.color, 1f / ++portions);
            blendIngredients |= 1 << (int)heldIngredient.kind;
            ColorLiquid(mixingLiquid, DrinkColor);
            Vector3 surface = mixingLiquid.transform.localPosition;
            surface.y = Mathf.Lerp(.045f, .123f, portions / (float)Capacity);
            mixingLiquid.transform.localPosition = surface;
            ReturnHeld();
            busy = false;
            RefreshStatus();
        }

        private IEnumerator Blend()
        {
            busy = true;
            status.text = "블렌딩 중";
            mixingSpindle.gameObject.SetActive(true);
            sound.PlayOneShot(blendSound);
            const float duration = .86f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float progress = elapsed / duration;
                mixingSpindle.localPosition = spindleStart - Vector3.up * (.10f * Mathf.Sin(progress * Mathf.PI));
                mixingSpindle.Rotate(0f, 1120f * Time.deltaTime, 0f);
                mixingModel.localPosition = mixingStart + Vector3.right * (.004f * Mathf.Sin(elapsed * 155f));
                yield return null;
            }
            mixingSpindle.localPosition = spindleStart;
            mixingSpindle.gameObject.SetActive(false);
            mixingModel.localPosition = mixingStart;
            Carry(mixingModel);
            var carriedLiquid = heldModel.Find(mixingLiquid.name).GetComponent<Renderer>();
            ColorLiquid(carriedLiquid, DrinkColor);
            mixingModel.gameObject.SetActive(false);
            holdingBlend = true;
            busy = false;
            RefreshStatus();
        }

        private IEnumerator FillMug()
        {
            busy = true;
            status.text = "머그잔에 따르는 중";
            yield return TipHeld();
            ColorLiquid(mugLiquid, DrinkColor);
            finishedDrink = DrinkName(blendIngredients);
            ReturnHeld();
            ResetMix();
            busy = false;
            RefreshStatus();
        }

        private IEnumerator TipHeld()
        {
            sound.PlayOneShot(pourSound);
            const float duration = .43f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float tilt = Mathf.Sin(elapsed / duration * Mathf.PI);
                heldModel.localPosition = CarryPosition + new Vector3(-.061f, .022f, .015f) * tilt;
                heldModel.localRotation = Quaternion.Euler(0f, -14f, 65f * tilt);
                yield return null;
            }
        }

        private void Carry(Transform model)
        {
            heldModel = Instantiate(model, eyes.transform, false);
            heldModel.localPosition = CarryPosition;
            heldModel.localRotation = Quaternion.Euler(0f, -14f, 0f);
            heldModel.localScale = Vector3.one * .61f;
            heldModel.gameObject.SetActive(true);
        }

        private void ReturnHeld()
        {
            if (heldModel != null)
            {
                heldModel.gameObject.SetActive(false);
                Destroy(heldModel.gameObject);
                heldModel = null;
            }
            if (heldIngredient != null) heldIngredient.model.gameObject.SetActive(true);
            heldIngredient = null;
            if (holdingBlend) mixingModel.gameObject.SetActive(true);
            holdingBlend = false;
        }

        private void ResetMix()
        {
            portions = 0;
            blendIngredients = 0;
            mixingLiquid.enabled = false;
        }

        private void ColorLiquid(Renderer surface, Color color)
        {
            liquidProperties.SetColor(BaseColor, color);
            surface.SetPropertyBlock(liquidProperties);
            surface.enabled = true;
        }

        private void RefreshStatus()
        {
            if (holdingBlend) status.text = "블렌딩 완료  ·  머그잔에 따르세요";
            else if (heldIngredient != null) status.text = "든 재료  ·  " + heldIngredient.label;
            else if (portions > 0) status.text = $"제조 컵  ·  {portions}/{Capacity}회 투입";
            else status.text = finishedDrink == null ? "" : finishedDrink + " 완성";
        }

        private static string DrinkName(int contents)
        {
            const int shot = 1 << (int)IngredientKind.Shot;
            const int milk = 1 << (int)IngredientKind.Milk;
            const int vanilla = 1 << (int)IngredientKind.Vanilla;
            const int condensed = 1 << (int)IngredientKind.Condensed;
            return contents switch
            {
                shot => "에스프레소",
                AmericanoIngredients => "아메리카노",
                (shot | milk) => "카페라떼",
                (shot | milk | vanilla) => "바닐라라떼",
                (shot | milk | condensed) => "연유라떼",
                _ => "블렌드 음료"
            };
        }

        private void BuildHud()
        {
            var hud = new GameObject("Brew interaction HUD", typeof(Canvas), typeof(CanvasScaler));
            hud.transform.SetParent(transform, false);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scale = hud.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920f, 1080f);
            MakeLabel(hud.transform, "+", new Vector2(.5f, .5f), Vector2.zero, 20);
            prompt = MakeLabel(hud.transform, "", new Vector2(.5f, .5f), new Vector2(0f, -34f), 22);
            status = MakeLabel(hud.transform, "", new Vector2(.5f, 0f), new Vector2(0f, 60f), 22);
        }

        private Text MakeLabel(Transform parent, string content, Vector2 anchor, Vector2 offset, int size)
        {
            var label = new GameObject("Brew label", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(670f, 38f);
            rect.anchoredPosition = offset;
            var text = label.GetComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(255, 238, 210, 255);
            text.raycastTarget = false;
            text.text = content;
            label.GetComponent<Outline>().effectColor = new Color32(34, 23, 16, 255);
            return text;
        }
    }
}
