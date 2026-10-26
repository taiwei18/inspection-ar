using InspectionAR.AR;
using InspectionAR.Data;
using InspectionAR.Net;
using InspectionAR.UI;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace InspectionAR
{
    /// <summary>
    /// 主流程：点击水平面 → 放置待提交标记 → 填写表单 → 提交到 Go 后端 → 按结果提示。
    /// 提交失败时保留标记和表单内容，用户可以直接重试。
    /// </summary>
    public class InspectionAppController : MonoBehaviour
    {
        [SerializeField] ArTapPlacer tapPlacer;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] IssueMarker markerPrefab;
        [SerializeField] IssueReportUI ui;

        IssueApiClient api;
        IssueMarker pendingMarker;
        ARAnchorManager anchorManager;
        ARAnchor pendingAnchor;
        float trackingStableSince = -1f;
        const float TrackingWarmupSeconds = 1.5f;
        bool submitting;
        int submittedCount;
        string lastStatus;

        void Awake()
        {
            if (tapPlacer == null) tapPlacer = FindFirstObjectByType<ArTapPlacer>();
            if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>();
            anchorManager = FindFirstObjectByType<ARAnchorManager>();
            if (ui == null) ui = GetComponent<IssueReportUI>();
            api = new IssueApiClient(() => BackendSettings.BaseUrl);
        }

        void OnEnable()
        {
            tapPlacer.PlaneTapped += OnPlaneTapped;
            ui.SubmitClicked += Submit;
            ui.CancelClicked += CancelPending;
            ui.TestConnectionClicked += TestConnection;
            ui.ServerUrlEdited += OnServerUrlEdited;
        }

        void OnDisable()
        {
            tapPlacer.PlaneTapped -= OnPlaneTapped;
            ui.SubmitClicked -= Submit;
            ui.CancelClicked -= CancelPending;
            ui.TestConnectionClicked -= TestConnection;
            ui.ServerUrlEdited -= OnServerUrlEdited;
        }

        void Start()
        {
            ui.SetServerUrl(BackendSettings.BaseUrl);
        }

        void Update()
        {
            if (ARSession.state != ARSessionState.SessionTracking) trackingStableSince = -1f;
            else if (trackingStableSince < 0f) trackingStableSince = Time.unscaledTime;
            var status = BuildStatusText();
            if (status == lastStatus) return;
            lastStatus = status;
            ui.SetStatus(status);
        }

        void OnPlaneTapped(ARPlane plane, Pose pose)
        {
            if (submitting) return;
            if (ARSession.state != ARSessionState.SessionTracking || trackingStableSince < 0f
                || Time.unscaledTime - trackingStableSince < TrackingWarmupSeconds)
            {
                ui.ShowToast("定位尚未稳定，请缓慢扫描周围后再放置", ToastKind.Info);
                return;
            }
            if (anchorManager == null || !anchorManager.isActiveAndEnabled || anchorManager.subsystem == null)
            {
                ui.ShowToast("空间锚点尚未就绪，请稍后重试", ToastKind.Info);
                return;
            }

            var anchor = anchorManager.AttachAnchor(plane, pose);
            if (anchor == null)
            {
                ui.ShowToast("无法创建空间锚点，请继续扫描后重试", ToastKind.Info);
                return;
            }

            // 每次只有一个待提交标记；再次点击平面相当于把标记移到新位置。
            RemovePendingMarker();
            pendingAnchor = anchor;
            pendingMarker = Instantiate(markerPrefab, anchor.transform, false);
            pendingMarker.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            pendingMarker.BindAnchor(anchor);
            pendingMarker.ShowPending();
            ui.ShowForm(pose.position);
            Debug.Log($"Inspection AR: plane anchor created {anchor.trackableId}");
        }

        void Submit()
        {
            if (submitting || pendingMarker == null) return;
            if (!pendingMarker.HasTracking)
            {
                ui.ShowToast("定位暂时丢失，请对准原来区域，恢复后再提交", ToastKind.Info);
                return;
            }

            var title = ui.Title.Trim();
            if (title.Length == 0)
            {
                ui.ShowToast("请填写标题（必填）", ToastKind.Error);
                return;
            }

            var position = pendingMarker.transform.position;
            var request = new CreateIssueRequest
            {
                title = title,
                description = ui.Description.Trim(),
                priority = ui.SelectedPriority,
                position = new IssuePosition { x = position.x, y = position.y, z = position.z },
            };

            submitting = true;
            ui.SetSubmitting(true);
            StartCoroutine(api.CreateIssue(request, OnSubmitSucceeded, OnSubmitFailed));
        }

        void OnSubmitSucceeded(Issue issue)
        {
            submitting = false;
            ui.SetSubmitting(false);
            if (pendingMarker != null) pendingMarker.ShowSubmitted(issue);
            pendingMarker = null; // 已提交的标记留在场景中，不再跟随点击移动
            pendingAnchor = null;
            submittedCount++;

            ui.ResetForm();
            ui.HideForm();
            ui.ShowToast($"提交成功：{issue.title}\nID {issue.id}", ToastKind.Success);
        }

        void OnSubmitFailed(string error)
        {
            submitting = false;
            ui.SetSubmitting(false);
            ui.ShowToast("提交失败：" + error, ToastKind.Error, 6f);
        }

        void CancelPending()
        {
            if (submitting) return;
            RemovePendingMarker();
            ui.HideForm();
        }

        void RemovePendingMarker()
        {
            if (pendingAnchor != null) Destroy(pendingAnchor.gameObject);
            else if (pendingMarker != null) Destroy(pendingMarker.gameObject);
            pendingAnchor = null;
            pendingMarker = null;
        }

        void TestConnection()
        {
            var url = BackendSettings.BaseUrl;
            ui.ShowToast("正在连接 " + url + " …", ToastKind.Info, 10f);
            StartCoroutine(api.CheckHealth(
                health => ui.ShowToast($"后端连接正常\n{url}\n已有 {health.issueCount} 条问题", ToastKind.Success),
                error => ui.ShowToast("连接失败：" + error, ToastKind.Error, 6f)));
        }

        void OnServerUrlEdited(string value)
        {
            BackendSettings.BaseUrl = value;
            ui.SetServerUrl(BackendSettings.BaseUrl); // 显示补全后的地址
        }

        string BuildStatusText()
        {
            string text;
            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                    text = "此设备不支持 AR（需要支持 ARCore 的手机）";
                    break;
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing:
                    text = "正在安装 / 更新 Google Play Services for AR…";
                    break;
                case ARSessionState.None:
                case ARSessionState.CheckingAvailability:
                    text = "正在检查 AR 支持…";
                    break;
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                    text = "定位未稳定或已丢失：请缓慢扫描原来区域，标记恢复前暂时隐藏";
                    break;
                default:
                    text = TrackingHint();
                    break;
            }
            return submittedCount > 0 ? $"{text}\n本次已上报 {submittedCount} 条" : text;
        }

        string TrackingHint()
        {
            if (trackingStableSince < 0f || Time.unscaledTime - trackingStableSince < TrackingWarmupSeconds)
                return "正在稳定定位，请缓慢扫描周围后再放置标记";
            if (pendingMarker != null && !pendingMarker.HasTracking)
                return "标记定位暂时丢失：请对准原来区域，恢复后再提交";
            if (pendingMarker != null) return "填写下方表单后提交；点击其他平面位置可移动标记";
            var planeCount = planeManager != null ? planeManager.trackables.count : 0;
            return planeCount == 0
                ? "正在识别水平面：请缓慢移动手机扫描地面或桌面"
                : $"已识别 {planeCount} 个水平面，点击平面放置问题标记";
        }
    }
}
