using System.Collections;
using UnityEngine;
using TMPro;

namespace IAmYourTranslator
{
    /// <summary>
    /// Lightweight toast utility to display short messages that fade out.
    /// </summary>
    public class ToastNotifier : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private TMP_Text text;
        private float duration;
        private float timer;

        public static void Show(string message, float durationSeconds = 5f)
        {
            var hostCanvas = Object.FindObjectOfType<Canvas>();
            if (hostCanvas == null)
                return;

            var go = new GameObject("IAYT_Toast");
            go.transform.SetParent(hostCanvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -80f);
            rect.sizeDelta = new Vector2(700f, 60f);

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0, 0);
            txtRect.anchorMax = new Vector2(1, 1);
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.enableWordWrapping = true;

            var notifier = go.AddComponent<ToastNotifier>();
            notifier.canvasGroup = cg;
            notifier.text = tmp;
            notifier.duration = Mathf.Max(0.5f, durationSeconds);
            notifier.timer = 0f;
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            float fadeStart = duration - 1.5f;
            if (timer > fadeStart)
            {
                float t = Mathf.InverseLerp(duration, fadeStart, timer);
                canvasGroup.alpha = Mathf.Clamp01(1f - t);
            }
            if (timer >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
