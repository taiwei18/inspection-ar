using System;
using UnityEngine;
using UnityEngine.UI;

namespace InspectionAR.UI
{
    /// <summary>
    /// 用代码创建 UGUI 控件的小工具。布局统一交给 Vertical/HorizontalLayoutGroup，
    /// 控件只需要通过 LayoutElement 声明高度或宽度。
    /// </summary>
    internal static class UiFactory
    {
        static Font font;

        // Unity 内置字体；中文字符会自动回退到手机系统字体。
        static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var text = CreateRect("Text", parent).gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Color background, float height, Action onClick)
        {
            var image = CreatePanel("Button " + label, parent, background);
            SetHeight(image, height);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            var text = CreateText(image.transform, label, 34, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform, 0, 0);
            return button;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, bool multiline, int characterLimit, float height)
        {
            var background = CreatePanel("Input " + placeholder, parent, new Color(1f, 1f, 1f, 0.95f));
            SetHeight(background, height);
            // 先禁用再添加 InputField，等文本组件都设置好后再启用，避免初始化时引用为空。
            background.gameObject.SetActive(false);

            var textArea = CreateRect("Text Area", background.transform);
            Stretch(textArea, 20, 12);
            textArea.gameObject.AddComponent<RectMask2D>();

            var alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            var placeholderText = CreateText(textArea, placeholder, 32, new Color(0.55f, 0.57f, 0.6f), alignment);
            placeholderText.fontStyle = FontStyle.Italic;
            Stretch(placeholderText.rectTransform, 0, 0);

            var text = CreateText(textArea, string.Empty, 32, new Color(0.1f, 0.12f, 0.16f), alignment);
            text.supportRichText = false;
            Stretch(text.rectTransform, 0, 0);

            var input = background.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;

            background.gameObject.SetActive(true);
            return input;
        }

        /// <summary>
        /// 纵向排列子物体。fitHeight 为 true 时自身高度随内容变化；
        /// 高度由父级布局或锚点决定的容器（如全屏面板、嵌套在布局里的分组）传 false。
        /// </summary>
        public static void AddVerticalLayout(GameObject go, int padding, float spacing, bool fitHeight = true)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            if (!fitHeight) return;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>横向一行；equalWidths 为 true 时子物体平分宽度。</summary>
        public static RectTransform CreateRow(Transform parent, float height, float spacing, bool equalWidths)
        {
            var row = CreateRect("Row", parent);
            SetHeight(row, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = equalWidths;
            layout.childForceExpandHeight = true;
            return row;
        }

        public static void DockTop(RectTransform rect) => Dock(rect, 1f);

        public static void DockBottom(RectTransform rect) => Dock(rect, 0f);

        static void Dock(RectTransform rect, float y)
        {
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        public static void Stretch(RectTransform rect, float horizontalPadding, float verticalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        public static void SetHeight(Component target, float height)
        {
            var element = GetLayoutElement(target);
            element.minHeight = height;
            element.preferredHeight = height;
        }

        public static void SetWidth(Component target, float preferredWidth, float flexibleWidth)
        {
            var element = GetLayoutElement(target);
            element.preferredWidth = preferredWidth;
            element.flexibleWidth = flexibleWidth;
        }

        static LayoutElement GetLayoutElement(Component target)
        {
            var element = target.GetComponent<LayoutElement>();
            return element != null ? element : target.gameObject.AddComponent<LayoutElement>();
        }
    }
}
