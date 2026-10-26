using System;

namespace InspectionAR.Data
{
    // JsonUtility 按字段名序列化，字段名必须与 Go 后端 / React 保持一致（小写驼峰）。

    public static class IssuePriority
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
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
