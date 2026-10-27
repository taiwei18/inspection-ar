using System;

namespace InspectionAR.Data
{
    // JsonUtility 按字段名序列化，字段名必须与 Go 后端 / React 保持一致（小写驼峰）。

    public static class IssuePriority
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";

        public static string Label(string priority)
        {
            switch (priority)
            {
                case High: return "高";
                case Medium: return "中";
                case Low: return "低";
                default: return priority;
            }
        }
    }

    public static class IssueStatus
    {
        public const string Open = "open";
        public const string InProgress = "in_progress";
        public const string Resolved = "resolved";

        public static readonly string[] All = { Open, InProgress, Resolved };

        // 与 React 管理端的中文名称一致
        public static string Label(string status)
        {
            switch (status)
            {
                case Open: return "待处理";
                case InProgress: return "处理中";
                case Resolved: return "已解决";
                default: return status;
            }
        }
    }

    // 坐标用 double：JsonUtility 输出 float 时会带出 0.41999998 这样的尾数。
    [Serializable]
    public class IssuePosition
    {
        public double x;
        public double y;
        public double z;
    }

    [Serializable]
    public class CreateIssueRequest
    {
        public string title;
        public string description;
        public string priority;
        public IssuePosition position;
    }

    [Serializable]
    public class Issue
    {
        public string id;
        public string title;
        public string description;
        public string priority;
        public string status;
        public IssuePosition position;
        public string createdAt;
        public string updatedAt;
    }

    // GET /api/issues 的响应 {"items": [...]}；JsonUtility 不能直接解析顶层数组，所以后端用对象包了一层
    [Serializable]
    public class IssueList
    {
        public Issue[] items;
    }

    [Serializable]
    public class StatusUpdateRequest
    {
        public string status;
    }

    [Serializable]
    public class HealthResponse
    {
        public string status;
        public int issueCount;
    }

    // 后端统一错误格式：{"error": {"code": "...", "message": "...", "field": "..."}}
    [Serializable]
    public class ApiErrorEnvelope
    {
        public ApiErrorBody error;
    }

    [Serializable]
    public class ApiErrorBody
    {
        public string code;
        public string message;
        public string field;
    }
}
