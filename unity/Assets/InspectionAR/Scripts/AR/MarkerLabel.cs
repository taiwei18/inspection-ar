using InspectionAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace InspectionAR.AR
{
    /// <summary>
    /// 标记上方的悬浮标签（World Space Canvas）：第一行标题，第二行状态和优先级，背景色表示状态。
    /// 每帧转向镜头。没有挂 GraphicRaycaster，不会拦截放置标记的点击。
    /// </summary>
    public class MarkerLabel
    {
        const float MetersPerUnit = 0.0006f; // 420 个画布单位 ≈ 25cm 宽
        const float Width = 420f;

        readonly Transform root;
        readonly Canvas canvas;
        readonly Image background;
        readonly Text titleText;
        readonly Text detailText;

        public MarkerLabel(Transform parent, float height)
        {
            var go = new GameObject("Label", typeof(Canvas), typeof(CanvasScaler));
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, height, 0f);
            root.localScale = Vector3.one * MetersPerUnit;

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // 提高世界空间文字的清晰度

            var rect = (RectTransform)root;
            rect.sizeDelta = new Vector2(Width, 0f);
            rect.pivot = new Vector2(0.5f, 0f); // 底边对齐标记顶部，向上展开

            background = go.AddComponent<Image>();
            background.raycastTarget = false;
            UiFactory.AddVerticalLayout(go, 16, 4);

            titleText = UiFactory.CreateText(root, string.Empty, 40, Color.white, TextAnchor.MiddleCenter);
            titleText.fontStyle = FontStyle.Bold;
            titleText.supportRichText = false; // 标题是用户输入，不解析富文本
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText = UiFactory.CreateText(root, string.Empty, 30, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);
        }

        public void Set(string title, string detail, Color color)
        {
            titleText.text = Truncate(title, 18);
            detailText.text = detail;
            detailText.gameObject.SetActive(!string.IsNullOrEmpty(detail));
            color.a = 0.88f;
            background.color = color;
        }

        public void SetVisible(bool visible) => canvas.enabled = visible;

        public void FaceCamera(Camera camera)
        {
            if (camera == null) return;
            var toLabel = root.position - camera.transform.position;
            if (toLabel.sqrMagnitude > 0.0001f) root.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
        }

        static string Truncate(string text, int max) =>
            string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
