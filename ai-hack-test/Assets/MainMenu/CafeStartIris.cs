using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CafeSim
{
    public sealed class CafeStartIris : MonoBehaviour
    {
        private const float RevealSeconds = 1.8f;
        private static readonly int Aperture = Shader.PropertyToID("_Aperture");
        private static CafeStartIris current;
        internal static bool Active => current != null;

        private Material apertureMaterial;
        private bool cursorWasVisible;

        internal static void Play(string scene, Action onLoadFailed)
        {
            if (Active) return;

            var overlay = new GameObject("Cafe start iris", typeof(RectTransform), typeof(Canvas));
            current = overlay.AddComponent<CafeStartIris>();
            DontDestroyOnLoad(current.gameObject);
            current.BuildOverlay();
            current.cursorWasVisible = Cursor.visible;
            Cursor.visible = false;
            current.StartCoroutine(current.Run(scene, onLoadFailed));
        }

        private IEnumerator Run(string scene, Action onLoadFailed)
        {
            yield return null; // Present black before the menu scene unloads.
            AsyncOperation load = null;
            try
            {
                load = SceneManager.LoadSceneAsync(scene);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }

            if (load == null)
            {
                onLoadFailed?.Invoke();
                Destroy(gameObject);
                yield break;
            }

            yield return load;
            yield return null; // Let the cafe camera and HUD render their first frame under black.
            if (!CafeEnding.Active && apertureMaterial != null)
            {
                float revealStarted = Time.unscaledTime;
                float progress;
                do
                {
                    progress = (Time.unscaledTime - revealStarted) / RevealSeconds;
                    SetOpening(Mathf.SmoothStep(0f, 1f, progress));
                    yield return null;
                }
                while (progress < 1f);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (apertureMaterial != null) Destroy(apertureMaterial);
            if (current != this) return;
            if (!CafeEnding.Active) Cursor.visible = cursorWasVisible;
            current = null;
        }

        private void BuildOverlay()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var mask = new GameObject("Iris mask", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = mask.rectTransform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            mask.raycastTarget = false;

            var shader = Resources.Load<Shader>("CafeStartIris");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Cafe start iris shader is unavailable.", this);
                mask.color = Color.black;
                return;
            }
            apertureMaterial = new Material(shader);
            mask.material = apertureMaterial;
            SetOpening(0f);
        }

        private void SetOpening(float opening)
        {
            Vector2 screen = new(Screen.width, Screen.height);
            float feather = Mathf.Max(1f, screen.y * .008f);
            float radius = (screen.magnitude * .5f + feather) * opening;
            apertureMaterial.SetVector(Aperture, new Vector4(screen.x, screen.y, radius, feather));
        }
    }
}
