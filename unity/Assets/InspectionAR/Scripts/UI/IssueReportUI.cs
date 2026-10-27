using System;
using System.Collections;
using System.Collections.Generic;
using InspectionAR.AR;
using InspectionAR.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace InspectionAR.UI
{
    public enum ToastKind
    {
        Info,
        Success,
        Error,
    }

    /// <summary>
    /// 界面层（只负责显示和收集输入，不包含业务逻辑）：
    /// 顶部状态栏 + 后端地址，底部上报表单，中间提示条。界面在 Awake 中用代码搭建，场景里不需要手工摆 UI。
    /// </summary>
    public class IssueReportUI : MonoBehaviour
    {
        public event Action SubmitClicked;
        public event Action CancelClicked;
        public event Action TestConnectionClicked;
        public event Action PlaneLockClicked;
        public event Action ListClicked;
        public event Action<string> ServerUrlEdited;

        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.14f, 0.88f);
        static readonly Color LabelColor = new Color(0.78f, 0.82f, 0.88f);
        static readonly Color AccentColor = new Color(0.16f, 0.45f, 0.93f);
        static readonly Color NeutralColor = new Color(0.33f, 0.36f, 0.42f);
        static readonly Color InfoToastColor = new Color(0.15f, 0.18f, 0.24f, 0.95f);
        static readonly Color SuccessToastColor = new Color(0.12f, 0.55f, 0.3f, 0.95f);
        static readonly Color ErrorToastColor = new Color(0.78f, 0.18f, 0.18f, 0.95f);
        static readonly Color GoodQualityColor = new Color(0.45f, 0.85f, 0.55f);
        static readonly Color FairQualityColor = new Color(1f, 0.78f, 0.3f);
        static readonly Color PoorQualityColor = new Color(1f, 0.5f, 0.45f);

        readonly Dictionary<string, Button> priorityButtons = new Dictionary<string, Button>();
        Text statusText;
        Text positionText;
        Text qualityText;
        Text planeLockLabel;
        Text syncText;
        Text listButtonLabel;
        Text toastText;
        Text submitLabel;
        InputField serverInput;
        InputField titleInput;
        InputField descriptionInput;
        Button submitButton;
        Button cancelButton;
        GameObject formPanel;
        Image toastPanel;
        Coroutine toastRoutine;

        public string Title => titleInput.text;
        public string Description => descriptionInput.text;
        public string SelectedPriority { get; private set; } = IssuePriority.Medium;

        /// <summary>历史问题列表面板，由「问题列表」按钮打开。</summary>
        public IssueListPanel ListPanel { get; private set; }

        void Awake()
        {
            EnsureEventSystem();
            Build();
            HideForm();
            toastPanel.gameObject.SetActive(false);
        }

        // ---------- 供控制器调用的接口 ----------

        public void SetStatus(string text) => statusText.text = text;

        public void SetServerUrl(string url) => serverInput.SetTextWithoutNotify(url);

        public void ShowForm(Vector3 position)
        {
            positionText.text = $"标记坐标（米）  x = {position.x:F2}   y = {position.y:F2}   z = {position.z:F2}";
            formPanel.SetActive(true);
        }

        public void HideForm() => formPanel.SetActive(false);

        public void SetPlacementQuality(string text, PlacementLevel level)
        {
            qualityText.text = text;
            qualityText.color = level == PlacementLevel.Good ? GoodQualityColor
                : level == PlacementLevel.Fair ? FairQualityColor : PoorQualityColor;
        }

        public void SetPlaneLocked(bool locked) => planeLockLabel.text = locked ? "继续扫描" : "锁定平面";

        public void SetSyncStatus(string text, bool ok)
        {
            syncText.text = text;
            syncText.color = ok ? LabelColor : PoorQualityColor;
        }

        public void SetIssueCount(int count) => listButtonLabel.text = $"问题列表（{count}）";

        public void ResetForm()
        {
            titleInput.text = string.Empty;
            descriptionInput.text = string.Empty;
            SelectPriority(IssuePriority.Medium);
        }

        public void SetSubmitting(bool submitting)
        {
            submitButton.interactable = !submitting;
            cancelButton.interactable = !submitting;
            titleInput.interactable = !submitting;
            descriptionInput.interactable = !submitting;
            foreach (var button in priorityButtons.Values) button.interactable = !submitting;
            submitLabel.text = submitting ? "提交中…" : "提交";
        }

        public void ShowToast(string message, ToastKind kind, float seconds = 3f)
        {
            toastText.text = message;
            toastPanel.color = kind == ToastKind.Error ? ErrorToastColor : kind == ToastKind.Success ? SuccessToastColor : InfoToastColor;
            toastPanel.gameObject.SetActive(true);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToastAfter(seconds));
        }

        IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            toastPanel.gameObject.SetActive(false);
            toastRoutine = null;
        }

        void SelectPriority(string priority)
        {
            SelectedPriority = priority;
            foreach (var pair in priorityButtons)
            {
                pair.Value.targetGraphic.color = pair.Key == priority ? IssueMarker.ColorForPriority(pair.Key) : NeutralColor;
            }
        }

        // ---------- 界面搭建 ----------

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            // 使用新输入系统的 UI 模块，与 AR Foundation 的输入方式保持一致
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void Build()
        {
            var canvasObject = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f; // 竖屏按宽度缩放

            // 避开刘海和底部手势条
            var safeArea = UiFactory.CreateRect("Safe Area", canvasObject.transform);
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;

            BuildTopBar(safeArea);
            BuildForm(safeArea);
            ListPanel = new IssueListPanel(safeArea); // 在提示条之前创建，提示条显示在面板上方
            BuildToast(safeArea);
        }

        void BuildTopBar(Transform parent)
        {
            var bar = UiFactory.CreatePanel("Top Bar", parent, PanelColor);
            UiFactory.DockTop(bar.rectTransform);
            UiFactory.AddVerticalLayout(bar.gameObject, 24, 14);

            statusText = UiFactory.CreateText(bar.transform, "正在启动 AR…", 34, Color.white, TextAnchor.MiddleLeft);

            var row = UiFactory.CreateRow(bar.transform, 84, 12, equalWidths: false);
            serverInput = UiFactory.CreateInputField(row, "后端地址，如 http://192.168.1.100:8080", false, 200, 84);
            serverInput.keyboardType = TouchScreenKeyboardType.URL;
            serverInput.onEndEdit.AddListener(value => ServerUrlEdited?.Invoke(value));
            UiFactory.SetWidth(serverInput, 0, 1);

            var testButton = UiFactory.CreateButton(row, "测试", AccentColor, 84, () => TestConnectionClicked?.Invoke());
            UiFactory.SetWidth(testButton, 140, 0);

            var lockButton = UiFactory.CreateButton(row, "锁定平面", NeutralColor, 84, () => PlaneLockClicked?.Invoke());
            UiFactory.SetWidth(lockButton, 210, 0);
            planeLockLabel = lockButton.GetComponentInChildren<Text>();

            // 第二行：与后端的同步状态 + 历史问题列表入口
            var syncRow = UiFactory.CreateRow(bar.transform, 76, 12, equalWidths: false);
            syncText = UiFactory.CreateText(syncRow, "正在同步问题列表…", 28, LabelColor, TextAnchor.MiddleLeft);
            UiFactory.SetWidth(syncText, 0, 1);
            var listButton = UiFactory.CreateButton(syncRow, "问题列表", AccentColor, 76, () => ListClicked?.Invoke());
            UiFactory.SetWidth(listButton, 280, 0);
            listButtonLabel = listButton.GetComponentInChildren<Text>();
        }

        void BuildForm(Transform parent)
        {
            var panel = UiFactory.CreatePanel("Report Form", parent, PanelColor);
            UiFactory.DockBottom(panel.rectTransform);
            UiFactory.AddVerticalLayout(panel.gameObject, 32, 14);
            formPanel = panel.gameObject;

            UiFactory.CreateText(panel.transform, "上报问题", 42, Color.white, TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
            positionText = UiFactory.CreateText(panel.transform, string.Empty, 28, LabelColor, TextAnchor.MiddleLeft);
            qualityText = UiFactory.CreateText(panel.transform, string.Empty, 28, LabelColor, TextAnchor.MiddleLeft);

            UiFactory.CreateText(panel.transform, "标题（必填）", 30, LabelColor, TextAnchor.MiddleLeft);
            titleInput = UiFactory.CreateInputField(panel.transform, "例如：入口墙面破损", false, 100, 96);

            UiFactory.CreateText(panel.transform, "描述（可选）", 30, LabelColor, TextAnchor.MiddleLeft);
            descriptionInput = UiFactory.CreateInputField(panel.transform, "补充现场情况", true, 1000, 170);

            UiFactory.CreateText(panel.transform, "优先级", 30, LabelColor, TextAnchor.MiddleLeft);
            var priorityRow = UiFactory.CreateRow(panel.transform, 88, 12, equalWidths: true);
            AddPriorityButton(priorityRow, IssuePriority.Low, "低 low");
            AddPriorityButton(priorityRow, IssuePriority.Medium, "中 medium");
            AddPriorityButton(priorityRow, IssuePriority.High, "高 high");
            SelectPriority(IssuePriority.Medium);

            var actions = UiFactory.CreateRow(panel.transform, 100, 16, equalWidths: true);
            cancelButton = UiFactory.CreateButton(actions, "取消", NeutralColor, 100, () => CancelClicked?.Invoke());
            submitButton = UiFactory.CreateButton(actions, "提交", AccentColor, 100, () => SubmitClicked?.Invoke());
            submitLabel = submitButton.GetComponentInChildren<Text>();
        }

        void AddPriorityButton(Transform row, string priority, string label)
        {
            priorityButtons[priority] = UiFactory.CreateButton(row, label, NeutralColor, 88, () => SelectPriority(priority));
        }

        void BuildToast(Transform parent)
        {
            toastPanel = UiFactory.CreatePanel("Toast", parent, InfoToastColor);
            toastPanel.raycastTarget = false; // 提示条不拦截点击
            var rect = toastPanel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.7f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(940f, 0f);
            UiFactory.AddVerticalLayout(toastPanel.gameObject, 28, 0);
            toastText = UiFactory.CreateText(toastPanel.transform, string.Empty, 32, Color.white, TextAnchor.MiddleCenter);
        }
    }
}
