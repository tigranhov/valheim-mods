using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveMapper.Cartographer
{
    /// <summary>Small helpers for building the kit's screens in code.</summary>
    internal static class Ui
    {
        public static readonly Color InkBrown = new Color(0.2f, 0.14f, 0.09f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = GUIManager.UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors a rect at one point of its parent, sized in canvas units.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Text Text(string name, Transform parent, int size, Color color, TextAnchor alignment)
        {
            RectTransform rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = GUIManager.Instance.AveriaSerifBold;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Beige text with a dark outline, readable over the game world.</summary>
        public static Text HudText(string name, Transform parent, int size, TextAnchor alignment)
        {
            Text text = Text(name, parent, size, GUIManager.Instance.ValheimBeige, alignment);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        public static Button Button(string label, Transform parent, Vector2 size)
        {
            GameObject go = GUIManager.Instance.CreateButton(label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size.x, size.y);
            return go.GetComponent<Button>();
        }

        /// <summary>The height of the canvas the kit draws on, in canvas units.</summary>
        public static float CanvasHeight(Transform canvas)
        {
            float height = ((RectTransform)canvas).rect.height;
            return height > 1f ? height : Screen.height;
        }
    }
}
