using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InspectionAR.AR;
using InspectionAR.Data;
using UnityEngine;
using UnityEngine.UI;

namespace InspectionAR.UI
{
    /// <summary>
    /// 历史问题列表面板（全屏覆盖）：列出后端的全部问题，点选一条后可查看详情、在手机上修改状态，
    /// 本次 AR 会话中放置过的问题还可以「在 AR 中查找」。只负责显示和收集操作，网络请求由控制器完成。
    /// </summary>
    public class IssueListPanel
    {
        public event Action<Issue, string> StatusChangeRequested;
        public event Action<Issue> LocateRequested;

        static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.12f, 0.97f);
        static readonly Color RowColor = new Color(0.15f, 0.18f, 0.23f, 1f);
        static readonly Color SelectedRowColor = new Color(0.2f, 0.3f, 0.45f, 1f);
        static readonly Color DetailColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        static readonly Color NeutralColor = new Color(0.33f, 0.36f, 0.42f);
        static readonly Color AccentColor = new Color(0.16f, 0.45f, 0.93f);
        static readonly Color MutedText = new Color(0.7f, 0.74f, 0.8f);

        readonly GameObject root;
        readonly Text headerText;
        readonly RectTransform content;
        readonly GameObject detail;
        readonly Text detailTitle;
        readonly Text detailMeta;
        readonly Text detailDescription;
        readonly Dictionary<string, Button> statusButtons = new Dictionary<string, Button>();
        readonly Button locateButton;
        readonly Text locateLabel;

        Issue[] issues = Array.Empty<Issue>();
        string selectedId;
        string renderedSignature;
        bool busy;

        /// <summary>判断某个问题是否在本次 AR 会话中放置过（有对应的标记）。</summary>
        public Func<string, bool> HasMarker { get; set; } = _ => false;

        public bool IsOpen => root.activeSelf;

        public IssueListPanel(Transform parent)
        {
            var panel = UiFactory.CreatePanel("Issue List", parent, PanelColor); // 不透明背景同时拦截点击，打开时不会误放标记
            UiFactory.Stretch(panel.rectTransform, 0, 0);
            UiFactory.AddVerticalLayout(panel.gameObject, 28, 16, fitHeight: false);
            root = panel.gameObject;

            var header = UiFactory.CreateRow(panel.transform, 90, 12, equalWidths: false);
            headerText = UiFactory.CreateText(header, "问题列表", 42, Color.white, TextAnchor.MiddleLeft);
            headerText.fontStyle = FontStyle.Bold;
            UiFactory.SetWidth(headerText, 0, 1);
            var closeButton = UiFactory.CreateButton(header, "关闭", NeutralColor, 90, Close);
            UiFactory.SetWidth(closeButton, 180, 0);

            UiFactory.CreateText(panel.transform,
                "底色：蓝=待处理  琥珀=处理中  绿=已解决。标「本次」的问题在当前 AR 会话中放置过，可以在 AR 中查找。",
                26, MutedText, TextAnchor.MiddleLeft);

            content = CreateScrollList(panel.transform);

            var detailPanel = UiFactory.CreatePanel("Detail", panel.transform, DetailColor);
            UiFactory.AddVerticalLayout(detailPanel.gameObject, 24, 12, fitHeight: false);
            detail = detailPanel.gameObject;
            detailTitle = UiFactory.CreateText(detailPanel.transform, string.Empty, 36, Color.white, TextAnchor.MiddleLeft);
            detailTitle.fontStyle = FontStyle.Bold;
            detailTitle.supportRichText = false;
            detailMeta = UiFactory.CreateText(detailPanel.transform, string.Empty, 26, MutedText, TextAnchor.MiddleLeft);
            detailDescription = UiFactory.CreateText(detailPanel.transform, string.Empty, 28, Color.white, TextAnchor.UpperLeft);
            detailDescription.supportRichText = false;

            var statusRow = UiFactory.CreateRow(detailPanel.transform, 84, 12, equalWidths: true);
            foreach (var status in IssueStatus.All)
            {
                var value = status;
                statusButtons[value] = UiFactory.CreateButton(statusRow, IssueStatus.Label(value), NeutralColor, 84,
                    () => OnStatusClicked(value));
            }

            locateButton = UiFactory.CreateButton(detailPanel.transform, "在 AR 中查找", AccentColor, 90, OnLocateClicked);
            locateLabel = locateButton.GetComponentInChildren<Text>();

            detail.SetActive(false);
            root.SetActive(false);
        }

        public void Open()
        {
            root.SetActive(true);
            renderedSignature = null; // 重新打开时强制刷新一次（标记可能在面板关闭期间新增）
            Render();
        }

        public void Close() => root.SetActive(false);

        public void SetIssues(Issue[] items)
        {
            issues = items ?? Array.Empty<Issue>();
            if (IsOpen) Render();
        }

        /// <summary>手机端修改状态的请求进行中时禁用状态按钮，避免重复提交。</summary>
        public void SetBusy(bool value)
        {
            busy = value;
            foreach (var button in statusButtons.Values) button.interactable = !busy;
        }

        // ---------- 渲染 ----------

        void Render()
        {
            headerText.text = $"问题列表（{issues.Length}）";

            // 数据没变就不重建列表，避免每次同步都打断滚动
            var signature = selectedId + "|" + string.Join(";",
                issues.Select(it => $"{it.id},{it.status},{it.updatedAt},{HasMarker(it.id)}"));
            if (signature == renderedSignature) return;
            renderedSignature = signature;

            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            if (issues.Length == 0)
            {
                UiFactory.CreateText(content, "暂无问题。放置标记并提交后会出现在这里。", 30, MutedText, TextAnchor.MiddleCenter);
            }
            foreach (var issue in issues) CreateRow(issue);

            RenderDetail(issues.FirstOrDefault(it => it.id == selectedId));
        }

        void CreateRow(Issue issue)
        {
            var row = UiFactory.CreatePanel("Row " + issue.id, content, issue.id == selectedId ? SelectedRowColor : RowColor);
            UiFactory.AddVerticalLayout(row.gameObject, 20, 4, fitHeight: false);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            var id = issue.id;
            button.onClick.AddListener(() => Select(id));

            var title = UiFactory.CreateText(row.transform, issue.title, 32, Color.white, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.supportRichText = false;

            var dot = ColorUtility.ToHtmlStringRGB(IssueMarker.ColorForStatus(issue.status));
            var here = HasMarker(issue.id) ? "  ·  <b>本次</b>" : string.Empty;
            UiFactory.CreateText(row.transform,
                $"<color=#{dot}>●</color> {IssueStatus.Label(issue.status)}  ·  {IssuePriority.Label(issue.priority)}优先级  ·  {FormatTime(issue.createdAt)}{here}",
                26, MutedText, TextAnchor.MiddleLeft);
        }

        void RenderDetail(Issue issue)
        {
            detail.SetActive(issue != null);
            if (issue == null) return;

            detailTitle.text = issue.title;
            var p = issue.position;
            var position = p != null ? $"坐标 ({p.x:F2}, {p.y:F2}, {p.z:F2})" : "无坐标";
            detailMeta.text = $"{IssuePriority.Label(issue.priority)}优先级 · 上报 {FormatTime(issue.createdAt)} · 更新 {FormatTime(issue.updatedAt)} · {position}";
            detailDescription.text = string.IsNullOrEmpty(issue.description) ? "（无描述）" : issue.description;

            foreach (var pair in statusButtons)
            {
                pair.Value.targetGraphic.color = pair.Key == issue.status ? IssueMarker.ColorForStatus(pair.Key) : NeutralColor;
                pair.Value.interactable = !busy;
            }

            var hasMarker = HasMarker(issue.id);
            locateButton.interactable = hasMarker;
            locateLabel.text = hasMarker ? "在 AR 中查找" : "不在本次 AR 会话中（暂不支持跨会话定位）";
        }

        void Select(string id)
        {
            selectedId = selectedId == id ? null : id; // 再点一次收起详情
            Render();
        }

        void OnStatusClicked(string status)
        {
            var issue = issues.FirstOrDefault(it => it.id == selectedId);
            if (issue == null || busy || issue.status == status) return;
            StatusChangeRequested?.Invoke(issue, status);
        }

        void OnLocateClicked()
        {
            var issue = issues.FirstOrDefault(it => it.id == selectedId);
            if (issue != null) LocateRequested?.Invoke(issue);
        }

        // ---------- 工具 ----------

        static RectTransform CreateScrollList(Transform parent)
        {
            var scrollRect = UiFactory.CreateRect("Scroll", parent);
            var element = scrollRect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f; // 占满面板剩余高度
            element.minHeight = 300f;

            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = UiFactory.CreatePanel("Viewport", scrollRect, new Color(0f, 0f, 0f, 0.01f)); // 近乎透明，但能接收拖动
            UiFactory.Stretch(viewport.rectTransform, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UiFactory.AddVerticalLayout(content.gameObject, 0, 12);

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return content;
        }

        static string FormatTime(string iso)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? time.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
                : iso;
        }
    }
}
