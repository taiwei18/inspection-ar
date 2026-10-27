using InspectionAR.Data;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.AR
{
    /// <summary>
    /// 场景中的问题标记（底座 Base + 细杆 Pole + 球头 Head + 悬浮标签）。
    /// 样式：
    /// - 待提交：整体黄色并轻微缩放闪动；
    /// - 已提交：球头和杆表示优先级（高=红、中=橙、低=灰蓝），底座和标签背景表示状态（待处理=蓝、处理中=琥珀、已解决=绿，与网页一致）；
    ///   处理中时球头上下浮动，已解决时球头和杆变灰。管理端修改状态后，同步到手机时调用 ApplyIssue 更新样式。
    /// 位置：跟随锚点并做平滑过渡，ARCore 每帧对锚点的微小修正不会表现为抖动；
    /// 跳变超过 SnapDistance（例如重新定位后的大幅修正）时直接跳到新位置。
    /// </summary>
    public class IssueMarker : MonoBehaviour
    {
        static readonly Color PendingColor = new Color(1f, 0.85f, 0.1f);
        static readonly Color ResolvedPartColor = new Color(0.62f, 0.64f, 0.66f);
        const float SmoothTime = 0.15f;   // 秒；越大越平滑，但跟随越慢
        const float SnapDistance = 0.3f;  // 米
        const float LabelHeight = 0.24f;  // 米；球头上方

        static Camera arCamera;

        Renderer[] renderers;
        Renderer baseRenderer;
        Renderer poleRenderer;
        Renderer headRenderer;
        Transform head;
        Vector3 headRestPosition;
        MarkerLabel label;
        bool pending;
        string status;
        float highlightUntil;
        ARAnchor anchor;
        Vector3 smoothedPosition;
        Quaternion smoothedRotation;
        bool hasSmoothedPose;

        public bool HasTracking => anchor != null && anchor.trackingState == TrackingState.Tracking
            && ARSession.state == ARSessionState.SessionTracking;

        public void BindAnchor(ARAnchor value) => anchor = value;

        /// <summary>锚点的真实位置（未经平滑），上报坐标时使用。</summary>
        public Vector3 AnchorPosition => anchor != null ? anchor.transform.position : transform.position;

        public string IssueId { get; private set; }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseRenderer = FindPart("Base");
            poleRenderer = FindPart("Pole");
            headRenderer = FindPart("Head");
            head = transform.Find("Head");
            if (head != null) headRestPosition = head.localPosition;
            label = new MarkerLabel(transform, LabelHeight);
        }

        Renderer FindPart(string partName)
        {
            var part = transform.Find(partName);
            return part != null ? part.GetComponent<Renderer>() : null;
        }

        void Update()
        {
            // 暂时隐藏失去跟踪的内容；保留锚点，等待 ARCore 恢复定位。
            var visible = HasTracking;
            foreach (var r in renderers) r.enabled = visible;
            label.SetVisible(visible);

            var scale = 1f;
            if (pending) scale = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            if (Time.time < highlightUntil) scale = 1f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 8f)); // 「在 AR 中查找」时闪动
            transform.localScale = Vector3.one * scale;

            if (head != null)
            {
                var bob = !pending && status == IssueStatus.InProgress ? 0.02f * Mathf.Sin(Time.time * 3f) : 0f;
                head.localPosition = headRestPosition + Vector3.up * bob;
            }
        }

        void LateUpdate()
        {
            if (anchor == null) return;
            if (!HasTracking)
            {
                hasSmoothedPose = false; // 恢复跟踪后直接用修正后的位置，不从旧位置滑过去
                return;
            }

            var target = anchor.transform;
            if (!hasSmoothedPose || (target.position - smoothedPosition).sqrMagnitude > SnapDistance * SnapDistance)
            {
                smoothedPosition = target.position;
                smoothedRotation = target.rotation;
                hasSmoothedPose = true;
            }
            else
            {
                var t = 1f - Mathf.Exp(-Time.deltaTime / SmoothTime); // 与帧率无关的指数平滑
                smoothedPosition = Vector3.Lerp(smoothedPosition, target.position, t);
                smoothedRotation = Quaternion.Slerp(smoothedRotation, target.rotation, t);
            }
            transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);

            if (arCamera == null) arCamera = Camera.main;
            label.FaceCamera(arCamera);
        }

        public void ShowPending()
        {
            pending = true;
            status = null;
            SetPartColors(PendingColor, PendingColor, PendingColor);
            label.Set("待提交", string.Empty, PendingColor);
        }

        public void ShowSubmitted(Issue issue)
        {
            pending = false;
            transform.localScale = Vector3.one;
            IssueId = issue.id;
            name = "IssueMarker_" + issue.id;
            ApplyIssue(issue);
        }

        /// <summary>按问题的最新状态和优先级更新样式（提交成功、同步到新状态时调用）。</summary>
        public void ApplyIssue(Issue issue)
        {
            status = issue.status;
            var statusColor = ColorForStatus(issue.status);
            var partColor = issue.status == IssueStatus.Resolved ? ResolvedPartColor : ColorForPriority(issue.priority);
            SetPartColors(statusColor, partColor, partColor);
            label.Set(issue.title, $"{IssueStatus.Label(issue.status)} · {IssuePriority.Label(issue.priority)}优先级", statusColor);
        }

        public void Highlight(float seconds = 3f) => highlightUntil = Time.time + seconds;

        public static Color ColorForPriority(string priority)
        {
            switch (priority)
            {
                case IssuePriority.High: return new Color(0.86f, 0.2f, 0.2f);
                case IssuePriority.Medium: return new Color(0.96f, 0.55f, 0.1f);
                default: return new Color(0.5f, 0.58f, 0.7f); // 低优先级用灰蓝，避免和「已解决」的绿色混淆
            }
        }

        public static Color ColorForStatus(string issueStatus)
        {
            switch (issueStatus)
            {
                case IssueStatus.InProgress: return new Color(0.92f, 0.62f, 0.08f);
                case IssueStatus.Resolved: return new Color(0.1f, 0.68f, 0.42f);
                default: return new Color(0.2f, 0.56f, 0.92f);
            }
        }

        void SetPartColors(Color baseColor, Color poleColor, Color headColor)
        {
            // renderer.material 会为每个标记复制一份材质，互不影响
            if (baseRenderer != null) baseRenderer.material.color = baseColor;
            if (poleRenderer != null) poleRenderer.material.color = poleColor;
            if (headRenderer != null) headRenderer.material.color = headColor;
        }
    }
}
