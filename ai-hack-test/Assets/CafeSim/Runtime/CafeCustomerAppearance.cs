using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CafeSim
{
    public sealed class CafeCustomerAppearance : MonoBehaviour
    {
        const string AssetPath = "CafeCustomers/";

        static readonly Color32[] ShirtColors =
        {
            new(101, 132, 162, 255), new(179, 104, 88, 255), new(114, 144, 109, 255),
            new(163, 137, 189, 255), new(219, 176, 103, 255), new(195, 152, 142, 255)
        };
        static readonly Color32[] TrouserColors =
        {
            new(65, 86, 111, 255), new(94, 77, 68, 255), new(80, 96, 81, 255),
            new(90, 79, 107, 255), new(112, 103, 88, 255)
        };
        static readonly Color32[] PeachColors =
        {
            new(246, 199, 171, 255), new(231, 176, 146, 255), new(208, 147, 115, 255)
        };

        CafeSession session;
        Material material;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        Transform rightHand, mugGrip;
        float walkWeight;
        bool walking;
        Animator customAnimator;
        string locomotionParameter;
        float animatorSpeed = 1f;

        internal static Transform Spawn(CafeSession session, CafeVisit visit, System.Random random)
        {
            var asset = CafeCustomerCatalog.Choose(visit.Person.id, random);
            if (asset != null)
            {
                var custom = SpawnCatalog(session, visit, asset);
                if (custom != null) return custom;
            }
            // A missing delivered model falls back within the same persona category.
            bool miku = (visit.Person.modelBase ?? "").Contains("miku");
            bool student = (visit.Person.modelBase ?? "").Contains("teen");
            bool female = (visit.Person.gender ?? "").Contains("여성");
            string style = miku ? "Miku" : student ? "Student" : "Black";
            string folder = AssetPath + style + "/";
            var prefab = Resources.Load<GameObject>(folder + (female && !miku ? "Female" : "Model"));
            var root = new GameObject($"Customer {visit.Id} {visit.Person.name}");
            root.transform.SetPositionAndRotation(session.entrance.position, session.entrance.rotation);
            root.name += $" ({style}{(miku ? "" : female ? " F" : " M")})";
            if (prefab == null)
            {
                Debug.LogError("Missing customer model: " + folder, session);
                return root.transform;
            }

            var appearance = root.AddComponent<CafeCustomerAppearance>();
            appearance.session = session;
            var pivot = new GameObject("Appearance").transform;
            pivot.SetParent(root.transform, false);
            var model = Instantiate(prefab, pivot, false);
            appearance.AlignModel(model, pivot, miku ? 1.69f : female ? 1.64f : student ? 1.72f : 1.75f);
            appearance.rightHand = Array.Find(model.GetComponentsInChildren<Transform>(),
                bone => bone.name.EndsWith("RightHand", StringComparison.Ordinal));
            appearance.SetMaterial(model, folder, miku, student, random);
            appearance.SetAnimation(model, folder);
            return root.transform;
        }

        static Transform SpawnCatalog(CafeSession session, CafeVisit visit, CustomerAssetEntry entry)
        {
            var prefab = Resources.Load<GameObject>(entry.resource);
            var idle = CafeCustomerCatalog.Clip(entry.idleResource, entry.idleClip);
            var walk = CafeCustomerCatalog.Clip(entry.walkResource, entry.walkClip);
            var rig = prefab == null ? null : prefab.GetComponentInChildren<Animator>();
            bool controller = !string.IsNullOrEmpty(entry.controllerParameter);
            if (prefab == null || rig == null || controller && rig.runtimeAnimatorController == null || !controller && (idle == null || walk == null))
            {
                Debug.LogWarning("[Cafe] Customer asset unavailable; using existing appearance: " + entry.resource, session);
                return null;
            }
            var root = new GameObject($"Customer {visit.Id} {visit.Person.name} ({entry.resource})");
            root.transform.SetPositionAndRotation(session.entrance.position, session.entrance.rotation);
            var appearance = root.AddComponent<CafeCustomerAppearance>();
            appearance.session = session;
            var pivot = new GameObject("Appearance").transform;
            pivot.SetParent(root.transform, false);
            var model = Instantiate(prefab, pivot, false);
            appearance.AlignModel(model, pivot, entry.height);
            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
                appearance.rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (appearance.rightHand == null)
                appearance.rightHand = Array.Find(model.GetComponentsInChildren<Transform>(),
                    bone => bone.name.EndsWith("RightHand", StringComparison.Ordinal));
            // Retain authored outfit, textures and accessories on imported models.
            if (!string.IsNullOrEmpty(entry.controllerParameter) && animator != null)
            {
                appearance.customAnimator = animator;
                appearance.locomotionParameter = entry.controllerParameter;
                appearance.animatorSpeed = animator.speed;
                animator.applyRootMotion = false;
            }
            else appearance.SetAnimation(model, idle, walk);
            return root.transform;
        }

        internal Transform PrepareMugGrip()
        {
            if (rightHand == null) return null;
            if (mugGrip == null)
            {
                mugGrip = new GameObject("Carried mug grip").transform;
                mugGrip.SetParent(transform, false);
                mugGrip.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            LateUpdate();
            return mugGrip;
        }

        void LateUpdate()
        {
            if (mugGrip == null || rightHand == null) return;
            // Follow the hand's motion while keeping the drink upright and its handle in the palm.
            mugGrip.localPosition = transform.InverseTransformPoint(rightHand.position) + new Vector3(0f, -.09f, .124f);
        }

        void AlignModel(GameObject model, Transform pivot, float height)
        {
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) return;

            // The imported characters use different units. Use their bind-pose geometry to fit the cafe.
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                Bounds meshBounds = renderer.sharedMesh.bounds;
                Matrix4x4 toPivot = pivot.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = toPivot.MultiplyPoint3x4(p);
                    if (hasBounds) bounds.Encapsulate(p);
                    else { bounds = new Bounds(p, Vector3.zero); hasBounds = true; }
                }
            }
            if (bounds.size.y < .001f) return;

            // Orient the model by the direction of its toes, independent of the FBX axis conversion.
            var bones = model.GetComponentsInChildren<Transform>();
            Vector3 forward = FootDirection(bones, "Left") + FootDirection(bones, "Right");
            forward = pivot.InverseTransformDirection(forward);
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-8f)
                pivot.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
            float scale = height / bounds.size.y;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = Vector3.down * (bounds.min.y * scale);
        }

        static Vector3 FootDirection(Transform[] bones, string side)
        {
            var foot = Array.Find(bones, bone => bone.name.EndsWith(side + "Foot", StringComparison.Ordinal));
            var toe = Array.Find(bones, bone => bone.name.EndsWith(side + "ToeBase", StringComparison.Ordinal));
            return foot != null && toe != null ? toe.position - foot.position : Vector3.zero;
        }

        void SetMaterial(GameObject model, string folder, bool miku, bool student, System.Random random)
        {
            var shader = Resources.Load<Shader>(AssetPath + "CustomerSurface");
            if (shader == null) return;
            material = new Material(shader) { name = "Customer outfit" };
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>(folder + "Color"));
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(folder + "Normal"));
            material.SetTexture("_MetallicMap", Resources.Load<Texture2D>(folder + "Metallic"));
            material.SetTexture("_RoughnessMap", Resources.Load<Texture2D>(folder + "Roughness"));
            if (!miku)
            {
                material.SetFloat("_Recolor", 1f);
                material.SetFloat("_SkinReference", student ? .81f : .41f);
                material.SetTexture("_ClothRegions", Resources.Load<Texture2D>(folder + "ClothRegions"));
                material.SetColor("_ShirtColor", ShirtColors[random.Next(ShirtColors.Length)]);
                material.SetColor("_TrouserColor", TrouserColors[random.Next(TrouserColors.Length)]);
                material.SetColor("_SkinColor", PeachColors[random.Next(PeachColors.Length)]);
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }

        void SetAnimation(GameObject model, string folder)
        {
            var idle = Array.Find(Resources.LoadAll<AnimationClip>(folder + "Idle"), clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            var walk = Array.Find(Resources.LoadAll<AnimationClip>(folder + "Walk"), clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (idle == null || walk == null)
            {
                Debug.LogWarning("Missing customer animations: " + folder, this);
                return;
            }
            SetAnimation(model, idle, walk);
        }

        void SetAnimation(GameObject model, AnimationClip idle, AnimationClip walk)
        {
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            graph = PlayableGraph.Create("Customer locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(AnimationClipPlayable.Create(graph, idle), 0, mixer, 0);
            graph.Connect(AnimationClipPlayable.Create(graph, walk), 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            var output = AnimationPlayableOutput.Create(graph, "Customer rig", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        internal void SetWalking(bool value) => walking = value;

        void Update()
        {
            if (customAnimator != null)
            {
                customAnimator.speed = session == null || session.Paused ? 0f : animatorSpeed;
                if (locomotionParameter == "Walking") customAnimator.SetBool(locomotionParameter, walking);
                else customAnimator.SetFloat(locomotionParameter, walking ? 1f : 0f);
                return;
            }
            if (!graph.IsValid()) return;
            if (session == null || session.Paused)
            {
                if (graph.IsPlaying()) graph.Stop();
                return;
            }
            if (!graph.IsPlaying()) graph.Play();
            walkWeight = Mathf.MoveTowards(walkWeight, walking ? 1f : 0f, 7f * Time.unscaledDeltaTime);
            mixer.SetInputWeight(0, 1f - walkWeight);
            mixer.SetInputWeight(1, walkWeight);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (material != null) Destroy(material);
        }
    }
}
