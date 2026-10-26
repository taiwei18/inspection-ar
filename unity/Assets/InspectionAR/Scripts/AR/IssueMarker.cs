using InspectionAR.Data;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.AR
{
    /// <summary>
    /// 场景中的问题标记。待提交时为黄色并轻微缩放闪动；提交成功后按优先级着色（高=红、中=橙、低=绿）。
    /// </summary>
    public class IssueMarker : MonoBehaviour
    {
        static readonly Color PendingColor = new Color(1f, 0.85f, 0.1f);

        Renderer[] renderers;
        bool pending;
        ARAnchor anchor;

        public bool HasTracking => anchor != null && anchor.trackingState == TrackingState.Tracking
            && ARSession.state == ARSessionState.SessionTracking;

        public void BindAnchor(ARAnchor value) => anchor = value;

        public string IssueId { get; private set; }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            // 暂时隐藏失去跟踪的内容；保留锚点，等待 ARCore 恢复定位。
            foreach (var renderer in renderers) renderer.enabled = HasTracking;
            if (!pending) return;
            transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 6f));
        }

        public void ShowPending()
        {
            pending = true;
            SetColor(PendingColor);
        }

        public void ShowSubmitted(Issue issue)
        {
            pending = false;
            transform.localScale = Vector3.one;
            IssueId = issue.id;
            name = "IssueMarker_" + issue.id;
            SetColor(ColorForPriority(issue.priority));
        }

        public static Color ColorForPriority(string priority)
        {
            switch (priority)
            {
                case IssuePriority.High: return new Color(0.86f, 0.2f, 0.2f);
                case IssuePriority.Medium: return new Color(0.96f, 0.55f, 0.1f);
                default: return new Color(0.25f, 0.7f, 0.4f);
            }
        }

        void SetColor(Color color)
        {
            // renderer.material 会为每个标记复制一份材质，互不影响
            foreach (var r in renderers) r.material.color = color;
        }
    }
}
