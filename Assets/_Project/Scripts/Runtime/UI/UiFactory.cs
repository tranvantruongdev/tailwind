using System;
using Template.Feel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// Builds simple uGUI in code for the sample scenes, so the template needs no prefabs to run.
    /// Real games build their screens as prefabs; keep using this for debug and test UI.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color Accent = new Color(1f, 0.78f, 0.18f);
        public static readonly Color Ink = new Color(0.08f, 0.09f, 0.11f);
        public static readonly Color Muted = new Color(0.32f, 0.35f, 0.40f);
        private static Font _font;

        public static Font DefaultFont => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static RectTransform CreateSafeArea(Transform parent)
        {
            var rect = CreateRect("SafeArea", parent);
            Stretch(rect);
            rect.gameObject.AddComponent<SafeArea>();
            return rect;
        }

        public static Image CreatePanel(Transform parent, Color color)
        {
            var rect = CreateRect("Panel", parent);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect("Text", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
        {
            var rect = CreateRect("Button " + label, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Accent;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(rect, label, Mathf.RoundToInt(size.y * 0.36f), Vector2.zero, size);
            text.color = Ink;

            button.onClick.AddListener(() =>
            {
                JuiceFx.Punch(rect, 0.08f, 0.2f);
                onClick?.Invoke();
            });
            return button;
        }

        public static Slider CreateSlider(Transform parent, string label, Vector2 anchoredPosition, Action<float> onChanged)
        {
            CreateText(parent, label, 48, anchoredPosition + new Vector2(0f, 70f), new Vector2(800, 80), TextAnchor.MiddleLeft);
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(800, 60);

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // DefaultControls without sprites makes every part white; colour them so the value is visible.
            var background = go.transform.Find("Background");
            if (background != null)
            {
                background.GetComponent<Image>().color = Muted;
            }

            if (slider.fillRect != null)
            {
                slider.fillRect.GetComponent<Image>().color = Accent;
            }

            if (slider.handleRect != null)
            {
                slider.handleRect.GetComponent<Image>().color = Color.white;
            }

            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string label, Vector2 anchoredPosition, Action<bool> onChanged)
        {
            var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(400, 40);
            rect.localScale = Vector3.one * 2f;

            foreach (var text in go.GetComponentsInChildren<Text>(true))
            {
                text.font = DefaultFont;
                text.text = label;
                text.color = Color.white;
                text.fontSize = 22;
            }

            var toggle = go.GetComponent<Toggle>();
            if (toggle.targetGraphic != null)
            {
                toggle.targetGraphic.color = Muted;
            }

            if (toggle.graphic != null)
            {
                toggle.graphic.color = Accent;
            }

            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return toggle;
        }
    }
}
