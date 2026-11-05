using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MapleCafe
{
    // Something outside the bar that accepts a finished mug, such as a customer at the counter.
    public interface IDrinkReceiver
    {
        Collider Target { get; }
        string Describe(string heldDrink, bool iced);
        bool TryReceive(string drink, bool iced, out Transform mugGrip);
    }

    [DefaultExecutionOrder(20)]
    public sealed class CafeBrewStation : MonoBehaviour
    {
        public IDrinkReceiver Receiver { get; set; }
        internal bool TryGetVisibleBlender(out Bounds bounds)
        {
            bounds = default;
            if (blenderMotor == null || !blenderMotor.enabled || !blenderMotor.gameObject.activeInHierarchy)
                return false;
            Vector3 position = blenderMotor.bounds.center;
            Vector3 point = eyes.WorldToViewportPoint(position);
            if (point.z <= eyes.nearClipPlane || point.z >= eyes.farClipPlane ||
                point.x < .025f || point.x > .975f || point.y < .025f || point.y > .975f) return false;

            Vector3 direction = position - eyes.transform.position;
            Collider obstruction = FirstCollider(new Ray(eyes.transform.position, direction.normalized),
                direction.magnitude, Receiver?.Target);
            if (obstruction != null && !obstruction.transform.IsChildOf(mixingTarget.transform)) return false;
            bounds = blenderMotor.bounds;
            bounds.Encapsulate(mixingTarget.bounds);
            return true;
        }
        internal static string InvalidDrinkName => CafeDrinkSurface.Invalid.Name;
        public enum IngredientKind { Shot, Water, Milk, Vanilla, Condensed, Cocoa, Matcha, Strawberry, Yuja, Lemon, Soda, Ssanghwa, Nuts, Ice }

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
        [SerializeField] private Renderer blenderMotor;
        [SerializeField] private Transform mixingModel;
        [SerializeField] private Renderer mixingLiquid;
        [SerializeField] private Transform mixingSpindle;
        [SerializeField] private Collider mugTarget;
        [SerializeField] private Renderer mugLiquid;
        [SerializeField] private Collider sinkTarget;
        [SerializeField] private AudioClip pourSound;
        [SerializeField] private AudioClip blendSound;
        [SerializeField] private AudioClip nutsSound;
        [SerializeField] private AudioClip yujaSound;
        [SerializeField] private AudioClip strawberrySound;
        [SerializeField] private Collider beanTarget, grinderTarget, espressoTarget;
        [SerializeField] private Transform beanModel, groundsModel;
        [SerializeField] private Material iceMaterial;

        private const float Reach = 1.65f;
        private const float AssistedReach = 1.28f;
        private const float AimMargin = .065f;
        private const int Capacity = 8;
        private const int WaterBit = 1 << (int)IngredientKind.Water;
        private static readonly Vector3 CarryPosition = new(.122f, -.107f, .34f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock liquidProperties;
        private Ingredient heldIngredient;
        private Ingredient shot;
        private enum ShotPrep { None, Beans, Grounds }
        private ShotPrep shotPrep;
        private Transform heldModel;
        private bool holdingBlend, busy;
        private bool hasIce, iceWaterReady, holdingMug;
        private int portions, blendIngredients;
        private Color blendColor;
        private Vector3 mixingStart, spindleStart;
        private Text prompt, status;
        private Text mugName;
        private CafeDrinkSurface drinkSurface;
        private Transform mugVisual;
        private CafeFloatingIce mixingIce, mugIce;
        private LineRenderer pourStream;
        private Material streamMaterial;
        private Collider[] targets;
        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private CharacterController playerCollider;
        private AudioSource sound;
        private string finishedDrink;
        private bool finishedIced;
        private CafeDrinkSurface.Drink blendedDrink;
        private Transform[] warningBubbles;
        private Material bubbleMaterial;
        private Color DrinkColor => CafeDrinkSurface.MatchServing(blendIngredients, iceWaterReady)?.Color ?? blendColor;

        private void Awake()
        {
            liquidProperties = new MaterialPropertyBlock();
            mixingStart = mixingModel.localPosition;
            spindleStart = mixingSpindle.localPosition;
            mixingSpindle.gameObject.SetActive(false);
            BuildHud();
            mugVisual = new GameObject("Pickup mug visual").transform;
            mugVisual.SetParent(mugTarget.transform, false);
            for (int i = mugTarget.transform.childCount - 2; i >= 0; i--)
            {
                var child = mugTarget.transform.GetChild(i);
                if (child.name != "Mug saucer") child.SetParent(mugVisual, true);
            }
            drinkSurface = new CafeDrinkSurface(mugLiquid);
            if (iceMaterial != null)
            {
                mixingIce = new CafeFloatingIce(transform, mixingLiquid.transform, iceMaterial, .033f);
                mugIce = new CafeFloatingIce(mugVisual, mugLiquid.transform, iceMaterial, .042f);
            }
            playerCollider = player.GetComponent<CharacterController>();
            shot = Array.Find(ingredients, item => item.kind == IngredientKind.Shot);
            targets = new Collider[ingredients.Length + 6];
            for (int i = 0; i < ingredients.Length; i++) targets[i] = ingredients[i].target;
            targets[ingredients.Length] = mixingTarget;
            targets[ingredients.Length + 1] = mugTarget;
            targets[ingredients.Length + 2] = sinkTarget;
            targets[ingredients.Length + 3] = beanTarget;
            targets[ingredients.Length + 4] = grinderTarget;
            targets[ingredients.Length + 5] = espressoTarget;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.volume = .62f;
        }

        private void Update()
        {
            bool active = player.CanInteract;
            prompt.transform.parent.gameObject.SetActive(active);
            if (!active) return;

            Collider target = FindTarget();

            var ingredient = target == null ? null : Array.Find(ingredients, item => item.target == target);
            prompt.text = busy ? "" : Describe(target, ingredient);
            if (!busy && Keyboard.current?.fKey.wasPressedThisFrame == true)
                Interact(target, ingredient);
        }

        private Collider FindTarget()
        {
            var ray = eyes.ViewportPointToRay(new Vector3(.5f, .5f));
            var direct = FirstCollider(ray, Reach);
            if (direct != null && (Array.IndexOf(targets, direct) >= 0 || direct == Receiver?.Target))
                return direct;

            Collider best = null;
            float bestScore = AimMargin;
            for (int i = 0; i <= targets.Length; i++)
            {
                var target = i < targets.Length ? targets[i] : Receiver?.Target;
                if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                Vector3 center = target.bounds.center;
                float depth = Vector3.Dot(center - ray.origin, ray.direction);
                if (depth <= 0f || depth > AssistedReach) continue;
                Vector3 edge = target.ClosestPoint(ray.GetPoint(depth));
                float score = Vector3.Distance(edge, ray.GetPoint(depth));
                if (score >= bestScore) continue;
                Vector3 direction = center - ray.origin;
                if (FirstCollider(new Ray(ray.origin, direction.normalized), direction.magnitude) != target) continue;
                best = target;
                bestScore = score;
            }
            return best;
        }

        private Collider FirstCollider(Ray ray, float distance, Collider ignored = null)
        {
            int count = Physics.RaycastNonAlloc(ray, rayHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = rayHits[i];
                if (hit.collider == playerCollider || hit.collider == ignored || hit.distance >= nearestDistance) continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
            }
            return nearest;
        }

        private string Describe(Collider target, Ingredient ingredient)
        {
            if (target == null) return "";
            if (Receiver != null && target == Receiver.Target)
                return Receiver.Describe(holdingMug ? finishedDrink : null, finishedIced);
            if (finishedDrink != null)
            {
                if (target == mugTarget) return holdingMug ? "F  ·  머그잔 내려놓기" : "F  ·  완성된 머그잔 들기";
                return target == sinkTarget ? "F  ·  싱크대에서 머그잔 비우기" : "싱크대에서 머그잔을 먼저 비워 주세요";
            }
            if (target == beanTarget && !holdingBlend)
                return shotPrep == ShotPrep.Beans ? "F  ·  원두 내려놓기" : "F  ·  원두 집기";
            if (target == grinderTarget && !holdingBlend)
                return shotPrep == ShotPrep.Beans ? "F  ·  원두 갈기" : "그라인더  ·  원두를 가져오세요";
            if (target == espressoTarget && !holdingBlend)
                return shotPrep == ShotPrep.Grounds ? "F  ·  에스프레소 샷 추출" : "에스프레소 머신  ·  원두를 먼저 갈아 주세요";
            if (ingredient != null && !holdingBlend)
                return "F  ·  " + ingredient.label + (heldIngredient == ingredient ? " 내려놓기" : " 집기");
            if (target == mixingTarget && !holdingBlend)
            {
                if (heldIngredient != null) return IngredientBlock(heldIngredient.kind) ?? "F  ·  제조 컵에 넣기";
                if (shotPrep != ShotPrep.None) return "원두를 갈아 샷을 추출하세요";
                if (hasIce && !iceWaterReady) return "물을 넣어 얼음물을 먼저 만드세요";
                if (iceWaterReady && blendIngredients == 0) return "얼음물 준비 완료  ·  음료 재료를 넣으세요";
                return portions > 0 ? "F  ·  블렌딩하고 컵 들기" : "제조 컵  ·  재료를 먼저 넣으세요";
            }
            if (target == mugTarget)
                return holdingBlend ? "F  ·  머그잔에 따르기" : "머그잔  ·  블렌딩한 음료를 담는 곳";
            if (target == sinkTarget && (portions > 0 || heldIngredient != null || shotPrep != ShotPrep.None || finishedDrink != null))
                return "F  ·  싱크대에서 비우기";
            return "";
        }

        private void Interact(Collider target, Ingredient ingredient)
        {
            if (target == null) return;
            if (Receiver != null && target == Receiver.Target)
            {
                if (holdingMug && finishedDrink != null && Receiver.TryReceive(finishedDrink, finishedIced, out Transform mugGrip))
                {
                    CopyMugTo(mugGrip);
                    EmptyMug();
                }
            }
            else if (target == sinkTarget) EmptyMug();
            else if (target == mugTarget && finishedDrink != null)
            {
                SetMugHeld(!holdingMug);
                RefreshStatus();
            }
            else if (finishedDrink != null) return;
            else if (target == beanTarget && !holdingBlend)
            {
                bool returning = shotPrep == ShotPrep.Beans;
                ReturnHeld();
                if (!returning)
                {
                    Carry(beanModel);
                    shotPrep = ShotPrep.Beans;
                }
                RefreshStatus();
            }
            else if (target == grinderTarget && shotPrep == ShotPrep.Beans)
                StartCoroutine(PrepareShot(false));
            else if (target == espressoTarget && shotPrep == ShotPrep.Grounds)
                StartCoroutine(PrepareShot(true));
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
                if (heldIngredient != null && IngredientBlock(heldIngredient.kind) == null) StartCoroutine(AddIngredient());
                else if (heldIngredient == null && shotPrep == ShotPrep.None && blendIngredients != 0
                    && (!hasIce || iceWaterReady)) StartCoroutine(Blend());
            }
            else if (target == mugTarget && holdingBlend)
                StartCoroutine(FillMug());
        }

        private void CopyMugTo(Transform grip)
        {
            var copy = Instantiate(mugVisual, grip, false);
            copy.name = "Served mug";
            copy.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.localScale = Vector3.one;

            // The drink and ice use renderer property blocks, which must be copied separately.
            var sourceRenderers = mugVisual.GetComponentsInChildren<Renderer>(true);
            var copyRenderers = copy.GetComponentsInChildren<Renderer>(true);
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                sourceRenderers[i].GetPropertyBlock(properties);
                copyRenderers[i].SetPropertyBlock(properties);
            }
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        private void EmptyMug()
        {
            ReturnHeld();
            SetMugHeld(false);
            ResetMix();
            finishedDrink = null;
            drinkSurface.Clear();
            SetWarningEffect(false);
            mugName.enabled = false;
            mugName.text = "";
            mugLiquid.enabled = false;
            mugIce?.SetVisible(false);
            RefreshStatus();
        }

        private string IngredientBlock(IngredientKind kind)
        {
            if (portions >= Capacity) return "제조 컵이 가득 찼어요";
            if (kind == IngredientKind.Ice)
            {
                if (hasIce) return "얼음이 이미 들어 있어요";
                if (portions > 1 || (blendIngredients != 0 && blendIngredients != WaterBit))
                    return "차가운 음료는 얼음과 물부터 넣어 주세요";
            }
            else if (hasIce && !iceWaterReady && kind != IngredientKind.Water)
                return "물을 넣어 얼음물을 먼저 만드세요";
            return null;
        }

        private IEnumerator PrepareShot(bool extract)
        {
            busy = true;
            status.text = extract ? "에스프레소 추출 중" : "원두 분쇄 중";
            ReturnHeld();
            sound.PlayOneShot(extract ? pourSound : blendSound);
            if (extract)
            {
                shot.model.gameObject.SetActive(true);
                var surface = shot.model.Find("Espresso inside").GetComponent<Renderer>();
                surface.enabled = false;
                Vector3 destination = surface.transform.position;
                EnsurePourStream();
                streamMaterial.color = shot.color;
                pourStream.SetPosition(0, destination + Vector3.up * .13f);
                pourStream.SetPosition(1, destination + Vector3.up * .06f);
                pourStream.SetPosition(2, destination);
                pourStream.enabled = true;
                yield return new WaitForSeconds(.43f);
                pourStream.enabled = false;
                surface.enabled = true;
                Carry(shot.model);
                shot.model.gameObject.SetActive(false);
                heldIngredient = shot;
            }
            else
            {
                yield return new WaitForSeconds(.86f);
                Carry(groundsModel);
                shotPrep = ShotPrep.Grounds;
            }
            busy = false;
            RefreshStatus();
        }

        private IEnumerator AddIngredient()
        {
            busy = true;
            status.text = heldIngredient.label + " 붓는 중";
            yield return TipHeld(mixingLiquid.transform.position, heldIngredient.color, heldIngredient.kind);
            portions++;
            if (heldIngredient.kind == IngredientKind.Ice)
            {
                hasIce = true;
                // A single water portion added before the ice belongs to the base.
                if (blendIngredients == WaterBit)
                {
                    iceWaterReady = true;
                    blendIngredients = 0;
                }
            }
            else
            {
                int liquidPortions = portions - (hasIce ? 1 : 0);
                blendColor = Color.Lerp(blendColor, heldIngredient.color, 1f / liquidPortions);
                if (hasIce && !iceWaterReady && heldIngredient.kind == IngredientKind.Water)
                    iceWaterReady = true;
                else
                    blendIngredients |= 1 << (int)heldIngredient.kind;
                ColorLiquid(mixingLiquid, DrinkColor);
            }
            Vector3 surface = mixingLiquid.transform.localPosition;
            surface.y = Mathf.Lerp(.045f, .123f, portions / (float)Capacity);
            mixingLiquid.transform.localPosition = surface;
            mixingIce?.SetVisible(hasIce);
            ReturnHeld();
            busy = false;
            RefreshStatus();
        }

        private IEnumerator Blend()
        {
            busy = true;
            blendedDrink = CafeDrinkSurface.MatchServing(blendIngredients, iceWaterReady) ?? CafeDrinkSurface.Invalid;
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
            ColorLiquid(carriedLiquid, blendedDrink.Color);
            mixingModel.gameObject.SetActive(false);
            mixingIce?.SetVisible(false);
            holdingBlend = true;
            busy = false;
            RefreshStatus();
        }

        private IEnumerator FillMug()
        {
            busy = true;
            status.text = "머그잔에 따르는 중";
            var drink = blendedDrink;
            yield return TipHeld(mugLiquid.transform.position, drink.Color);
            ColorLiquid(mugLiquid, drink.Color);
            drinkSurface.Show(drink);
            bool invalid = drink == CafeDrinkSurface.Invalid;
            if (!invalid && iceWaterReady) mugIce?.SetDrinkColor(drink.Color);
            mugIce?.SetVisible(iceWaterReady && !invalid);
            finishedDrink = drink.Name;
            finishedIced = iceWaterReady && !invalid;
            mugName.text = drink.Name + (invalid ? "" : iceWaterReady ? "  ·  ICE" : "  ·  HOT");
            mugName.color = invalid ? new Color32(243, 174, 255, 255) : new Color32(255, 238, 210, 255);
            SetWarningEffect(invalid);
            ReturnHeld();
            ResetMix();
            busy = false;
            RefreshStatus();
        }

        private IEnumerator TipHeld(Vector3 destination, Color color, IngredientKind? ingredient = null)
        {
            bool ice = ingredient == IngredientKind.Ice;
            bool nuts = ingredient == IngredientKind.Nuts;
            bool piecesFall = ice || nuts;
            bool strawberry = ingredient == IngredientKind.Strawberry;
            bool jam = ingredient == IngredientKind.Yuja || strawberry;
            bool liquid = !piecesFall && !jam;
            if (liquid) sound.PlayOneShot(pourSound);
            Vector3 start = heldModel.position;
            Vector3 forward = Vector3.ProjectOnPlane(destination - start, Vector3.up).normalized;
            Vector3 pourPosition = destination - forward * .14f + Vector3.up * .19f;
            Quaternion upright = Quaternion.LookRotation(forward);
            Quaternion tipped = Quaternion.AngleAxis(105f, Vector3.Cross(Vector3.up, forward)) * upright;
            Quaternion initialRotation = heldModel.rotation;
            heldModel.SetParent(transform, true);
            EnsurePourStream();
            streamMaterial.color = ingredient == IngredientKind.Yuja ? new Color(.97f, .46f, .056f) : color;
            Transform[] pieces = liquid ? null : CreatePourPieces(piecesFall, ice);
            float duration = liquid ? .43f : .66f;
            bool played = liquid;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float tilt = Mathf.SmoothStep(0f, 1f, Mathf.Sin(elapsed / duration * Mathf.PI) * 1.35f);
                heldModel.position = Vector3.Lerp(start, pourPosition, tilt);
                heldModel.rotation = Quaternion.Slerp(initialRotation, tipped, tilt);
                Vector3 lip = heldModel.position + heldModel.up * (liquid ? .08f : .057f);
                pourStream.enabled = liquid && tilt > .8f;
                if (pourStream.enabled)
                {
                    pourStream.SetPosition(0, lip);
                    pourStream.SetPosition(1, Vector3.Lerp(lip, destination, .54f) + Vector3.up * .025f);
                    pourStream.SetPosition(2, destination);
                }
                if (pieces != null) AnimatePourPieces(pieces, piecesFall, ice, elapsed, lip, destination);
                if (!played && elapsed >= (piecesFall ? .22f : .30f))
                {
                    var clip = piecesFall ? nutsSound : strawberry ? strawberrySound : yujaSound;
                    sound.PlayOneShot(clip != null ? clip : pourSound);
                    played = true;
                }
                yield return null;
            }
            pourStream.enabled = false;
            if (pieces != null)
                foreach (var piece in pieces) Destroy(piece.gameObject);
        }

        private Transform[] CreatePourPieces(bool cubes, bool ice)
        {
            var pieces = new Transform[ice ? 3 : cubes ? 12 : 2];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = GameObject.CreatePrimitive(cubes ? PrimitiveType.Cube : PrimitiveType.Sphere);
                piece.name = ice ? "Falling ice cube" : cubes ? "Falling nut piece" : "Falling jam";
                piece.transform.SetParent(transform, false);
                var collider = piece.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = ice && iceMaterial != null ? iceMaterial : streamMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                piece.SetActive(false);
                pieces[i] = piece.transform;
            }
            return pieces;
        }

        private static void AnimatePourPieces(Transform[] pieces, bool cubes, bool ice, float elapsed, Vector3 lip, Vector3 destination)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                float start = cubes ? .155f + i * (ice ? .065f : .017f) : .173f + i * .143f;
                float flight = cubes ? .153f + (i % 3) * .021f : .213f;
                float progress = (elapsed - start) / flight;
                var piece = pieces[i];
                bool falling = progress >= 0f && progress < 1f;
                if (piece.gameObject.activeSelf != falling) piece.gameObject.SetActive(falling);
                if (!falling) continue;

                float angle = i * 2.39996f;
                Vector3 spread = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (cubes ? .041f : .013f);
                Vector3 end = destination + spread;
                float drop = progress * progress;
                piece.position = Vector3.Lerp(lip, end, progress);
                piece.position = new Vector3(piece.position.x, Mathf.Lerp(lip.y, end.y, drop), piece.position.z);
                if (cubes)
                {
                    float size = ice ? .029f + i * .003f : .009f + (i % 4) * .002f;
                    piece.localScale = new Vector3(size, size * (ice ? .91f : .62f), size * .84f);
                    piece.rotation = Quaternion.Euler(i * 31f + progress * 270f, i * 57f, progress * 190f);
                }
                else
                {
                    float stretch = Mathf.Sin(Mathf.PI * progress);
                    float size = i == 0 ? .033f : .024f;
                    piece.localScale = new Vector3(size, size * (1f + 1.65f * stretch), size);
                    piece.rotation = Quaternion.FromToRotation(Vector3.up, (lip - end).normalized);
                }
            }
        }

        private void EnsurePourStream()
        {
            if (pourStream != null) return;
            pourStream = new GameObject("Liquid stream").AddComponent<LineRenderer>();
            pourStream.transform.SetParent(transform, false);
            pourStream.positionCount = 3;
            pourStream.startWidth = .012f;
            pourStream.endWidth = .007f;
            pourStream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            streamMaterial = new Material(mugLiquid.sharedMaterial);
            pourStream.sharedMaterial = streamMaterial;
            pourStream.enabled = false;
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
            if (heldIngredient != null && heldIngredient != shot) heldIngredient.model.gameObject.SetActive(true);
            heldIngredient = null;
            shotPrep = ShotPrep.None;
            if (holdingBlend) mixingModel.gameObject.SetActive(true);
            holdingBlend = false;
        }

        private void SetMugHeld(bool held)
        {
            holdingMug = held;
            mugVisual.SetParent(held ? eyes.transform : mugTarget.transform, false);
            mugVisual.localPosition = held ? CarryPosition - Vector3.up * .039f : Vector3.zero;
            mugVisual.localRotation = held ? Quaternion.Euler(-12f, -14f, 0f) : Quaternion.identity;
            mugVisual.localScale = Vector3.one * (held ? .61f : 1f);
        }

        private void ResetMix()
        {
            portions = 0;
            blendIngredients = 0;
            blendedDrink = null;
            hasIce = false;
            iceWaterReady = false;
            mixingLiquid.enabled = false;
            mixingIce?.SetVisible(false);
        }

        private void ColorLiquid(Renderer surface, Color color)
        {
            liquidProperties.SetColor(BaseColor, color);
            surface.SetPropertyBlock(liquidProperties);
            surface.enabled = true;
        }

        private void RefreshStatus()
        {
            if (holdingMug)
                status.text = "든 머그잔  ·  " + mugName.text;
            else if (finishedDrink != null)
                status.text = finishedDrink == CafeDrinkSurface.Invalid.Name
                    ? finishedDrink + "  ·  싱크대에서 비워 주세요"
                    : mugName.text + " 완성";
            else if (holdingBlend) status.text = "블렌딩 완료  ·  머그잔에 따르세요";
            else if (heldIngredient != null) status.text = "든 재료  ·  " + heldIngredient.label;
            else if (shotPrep == ShotPrep.Beans) status.text = "든 재료  ·  원두  ·  그라인더에서 갈아 주세요";
            else if (shotPrep == ShotPrep.Grounds) status.text = "분쇄 완료  ·  에스프레소 머신에서 추출하세요";
            else if (hasIce && !iceWaterReady) status.text = "제조 컵  ·  물을 넣어 얼음물을 만드세요";
            else if (portions > 0)
                status.text = $"{(iceWaterReady ? "얼음물 베이스" : "제조 컵")}  ·  {portions}/{Capacity}회 투입";
            else status.text = "";
        }

        private void SetWarningEffect(bool visible)
        {
            if (visible && warningBubbles == null)
            {
                bubbleMaterial = new Material(mugLiquid.sharedMaterial);
                bubbleMaterial.color = new Color(.68f, .16f, .94f);
                bubbleMaterial.EnableKeyword("_EMISSION");
                bubbleMaterial.SetColor("_EmissionColor", new Color(.36f, .06f, .55f));
                warningBubbles = new Transform[7];
                for (int i = 0; i < warningBubbles.Length; i++)
                {
                    var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bubble.name = "Invalid drink bubble";
                    bubble.transform.SetParent(mugLiquid.transform.parent, false);
                    var collider = bubble.GetComponent<Collider>();
                    collider.enabled = false;
                    Destroy(collider);
                    var renderer = bubble.GetComponent<Renderer>();
                    renderer.sharedMaterial = bubbleMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    warningBubbles[i] = bubble.transform;
                }
            }
            if (warningBubbles == null) return;
            foreach (var bubble in warningBubbles) bubble.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            mixingIce?.Animate(Time.time);
            mugIce?.Animate(Time.time);
            if (warningBubbles != null && warningBubbles[0].gameObject.activeSelf)
            {
                for (int i = 0; i < warningBubbles.Length; i++)
                {
                    float life = Mathf.Repeat(Time.time * 1.2f + i * .23f, 1f);
                    float angle = i * 2.4f + life * 2f;
                    float radius = .023f + (i % 3) * .01f;
                    warningBubbles[i].localPosition = mugLiquid.transform.localPosition
                        + new Vector3(Mathf.Cos(angle) * radius, .018f + life * .16f, Mathf.Sin(angle) * radius);
                    warningBubbles[i].localScale = Vector3.one * (.023f * Mathf.Sin(Mathf.PI * life));
                }
            }
            Vector3 anchor = mugLiquid.transform.position + Vector3.up * .12f;
            Vector3 point = eyes.WorldToViewportPoint(anchor);
            Vector3 toLabel = anchor - eyes.transform.position;
            mugName.enabled = !holdingMug && mugName.text.Length > 0 && mugLiquid.enabled
                && point.z > 0f && point.z < 2.25f && point.x > .02f && point.x < .98f && point.y > .04f && point.y < .97f
                && FirstCollider(new Ray(eyes.transform.position, toLabel.normalized), toLabel.magnitude) == null;
            mugName.rectTransform.anchorMin = mugName.rectTransform.anchorMax = new Vector2(point.x, point.y);
        }

        private void OnDestroy()
        {
            drinkSurface?.Dispose();
            if (streamMaterial != null) Destroy(streamMaterial);
            if (bubbleMaterial != null) Destroy(bubbleMaterial);
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
            mugName = MakeLabel(hud.transform, "", new Vector2(.5f, .5f), Vector2.zero, 23);
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
