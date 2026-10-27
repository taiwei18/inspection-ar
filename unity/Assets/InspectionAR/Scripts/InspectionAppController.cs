using InspectionAR.AR;
using InspectionAR.Data;
using InspectionAR.Net;
using InspectionAR.UI;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR
{
    /// <summary>
    /// 主流程：点击水平面 → 评估放置质量 → 放置待提交标记 → 填写表单 → 提交到 Go 后端 → 按结果提示。
    /// 提交失败时保留标记和表单内容，用户可以直接重试。
    /// 定位稳定性相关：放置质量门槛（PlacementQuality）、多角度观察引导（ViewCoverage）、
    /// 平面锁定与淡化（PlaneDisplay）、标记平滑（IssueMarker）。
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
        PlacementQuality placementQuality;
        PlaneDisplay planeDisplay;
        readonly ViewCoverage viewCoverage = new ViewCoverage();
        Camera arCamera;
        float trackingStableSince = -1f;
        const float TrackingWarmupSeconds = 1.5f;
        bool userUnlockedPlanes; // 用户手动解锁过就不再自动锁定
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

            // 特征点用于评估落点纹理；旧场景里没有点云管理器时在运行时补上
            var pointCloudManager = FindFirstObjectByType<ARPointCloudManager>();
            if (pointCloudManager == null && planeManager != null)
                pointCloudManager = planeManager.gameObject.AddComponent<ARPointCloudManager>();
            placementQuality = new PlacementQuality(planeManager, pointCloudManager);
            planeDisplay = new PlaneDisplay(planeManager);
            arCamera = Camera.main;
        }

        void OnEnable()
        {
            tapPlacer.PlaneTapped += OnPlaneTapped;
            ui.SubmitClicked += Submit;
            ui.CancelClicked += CancelPending;
            ui.TestConnectionClicked += TestConnection;
            ui.PlaneLockClicked += TogglePlaneLock;
            ui.ServerUrlEdited += OnServerUrlEdited;
        }

        void OnDisable()
        {
            tapPlacer.PlaneTapped -= OnPlaneTapped;
            ui.SubmitClicked -= Submit;
            ui.CancelClicked -= CancelPending;
            ui.TestConnectionClicked -= TestConnection;
            ui.PlaneLockClicked -= TogglePlaneLock;
            ui.ServerUrlEdited -= OnServerUrlEdited;
        }

        void Start()
        {
            ui.SetServerUrl(BackendSettings.BaseUrl);
            ui.SetPlaneLocked(false);
        }

        void Update()
        {
            if (!IsTrackingNormally()) trackingStableSince = -1f;
            else if (trackingStableSince < 0f) trackingStableSince = Time.unscaledTime;

            placementQuality.Tick(Time.unscaledTime);
            planeDisplay.Tick(faded: pendingMarker != null, Time.unscaledDeltaTime);
            if (pendingMarker != null && pendingMarker.HasTracking) viewCoverage.Observe(arCamera, pendingMarker.AnchorPosition);

            var status = BuildStatusText();
            if (status == lastStatus) return;
            lastStatus = status;
            ui.SetStatus(status);
        }

        static bool IsTrackingNormally() =>
            ARSession.state == ARSessionState.SessionTracking && ARSession.notTrackingReason == NotTrackingReason.None;

        void OnPlaneTapped(ARPlane plane, Pose pose)
        {
            if (submitting) return;
            if (!IsTrackingNormally() || trackingStableSince < 0f
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

            // 平面被合并后，锚点附着到合并后的大平面上
            while (plane.subsumedBy != null) plane = plane.subsumedBy;

            var assessment = placementQuality.Evaluate(plane, pose.position, Time.unscaledTime);
            if (!assessment.Allowed)
            {
                ui.ShowToast(assessment.Message, ToastKind.Info);
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
            viewCoverage.Reset();
            ui.ShowForm(pose.position);
            ui.SetPlacementQuality(assessment.Message, assessment.Level);
            Debug.Log($"Inspection AR: plane anchor created {anchor.trackableId}, {assessment.Message}");

            // 放下标记后自动锁定平面：平面不再扩展合并，标记也不再随之变动
            if (!planeDisplay.Locked && !userUnlockedPlanes)
            {
                SetPlanesLocked(true);
                ui.ShowToast("已自动锁定平面，避免平面变化带动标记移动。\n需要识别新区域时点「继续扫描」", ToastKind.Info, 4f);
            }
        }

        void TogglePlaneLock()
        {
            var locked = !planeDisplay.Locked;
            SetPlanesLocked(locked);
            if (!locked) userUnlockedPlanes = true;
            ui.ShowToast(locked ? "已锁定平面：不再识别新平面" : "继续扫描：会识别新的平面", ToastKind.Info);
        }

        void SetPlanesLocked(bool locked)
        {
            planeDisplay.SetLocked(locked);
            ui.SetPlaneLocked(locked);
            Debug.Log($"Inspection AR: planes locked={locked}, detection={planeManager?.currentDetectionMode}");
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

            var position = pendingMarker.AnchorPosition; // 用锚点真实位置，不用平滑中的显示位置
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
                    text = NotTrackingHint() + "（标记恢复前暂时隐藏）";
                    break;
                default:
                    text = ARSession.notTrackingReason == NotTrackingReason.None ? TrackingHint() : NotTrackingHint();
                    break;
            }
            return submittedCount > 0 ? $"{text}\n本次已上报 {submittedCount} 条" : text;
        }

        /// <summary>把 ARCore 给出的跟踪受限原因翻译成用户能照做的提示。</summary>
        static string NotTrackingHint()
        {
            switch (ARSession.notTrackingReason)
            {
                case NotTrackingReason.InsufficientLight: return "光线不足：请打开照明或移到明亮处";
                case NotTrackingReason.InsufficientFeatures: return "画面缺少纹理：请对准有图案、接缝或物品的区域";
                case NotTrackingReason.ExcessiveMotion: return "移动太快：请放慢速度，平稳移动手机";
                case NotTrackingReason.Relocalizing: return "正在重新定位：请对准之前扫描过的区域并稍作停留";
                default: return "正在初始化：请缓慢移动手机，对准地面或桌面";
            }
        }

        string TrackingHint()
        {
            if (trackingStableSince < 0f || Time.unscaledTime - trackingStableSince < TrackingWarmupSeconds)
                return "正在稳定定位，请缓慢扫描周围后再放置标记";
            if (pendingMarker != null && !pendingMarker.HasTracking)
                return "标记定位暂时丢失：请对准原来区域，恢复后再提交";
            if (pendingMarker != null)
            {
                return viewCoverage.IsEnough
                    ? "已从多个角度观察此处（完成），填写下方表单后提交"
                    : $"建议：拿着手机绕标记换个角度看看（{viewCoverage.Covered}/{ViewCoverage.RecommendedSectors}），转开再回来时更准";
            }
            var planeCount = planeManager != null ? planeManager.trackables.count : 0;
            if (planeCount == 0) return "正在识别水平面：请缓慢移动手机扫描地面或桌面";
            return planeDisplay.Locked
                ? $"平面已锁定（{planeCount} 个），点击平面放置标记；要识别新区域请点「继续扫描」"
                : $"已识别 {planeCount} 个水平面，点击平面放置问题标记";
        }
    }
}
