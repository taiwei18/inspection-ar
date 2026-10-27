using System;
using System.Collections;
using System.Text;
using InspectionAR.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace InspectionAR.Net
{
    /// <summary>
    /// 封装对 Go 后端的 HTTP 调用。方法都是协程，用 StartCoroutine 启动；
    /// 回调在主线程执行，可以直接操作 UI。
    /// </summary>
    public class IssueApiClient
    {
        const int TimeoutSeconds = 8;

        readonly Func<string> baseUrl;

        public IssueApiClient(Func<string> baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public IEnumerator CheckHealth(Action<HealthResponse> onSuccess, Action<string> onError)
        {
            return Send("GET", "/api/health", null, onSuccess, onError);
        }

        public IEnumerator CreateIssue(CreateIssueRequest body, Action<Issue> onSuccess, Action<string> onError)
        {
            return Send("POST", "/api/issues", JsonUtility.ToJson(body), onSuccess, onError);
        }

        public IEnumerator ListIssues(Action<IssueList> onSuccess, Action<string> onError)
        {
            return Send("GET", "/api/issues", null, onSuccess, onError);
        }

        public IEnumerator UpdateStatus(string id, string status, Action<Issue> onSuccess, Action<string> onError)
        {
            var body = JsonUtility.ToJson(new StatusUpdateRequest { status = status });
            return Send("PATCH", "/api/issues/" + UnityWebRequest.EscapeURL(id), body, onSuccess, onError);
        }

        IEnumerator Send<T>(string method, string path, string jsonBody, Action<T> onSuccess, Action<string> onError)
        {
            var url = baseUrl() + path;
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.timeout = TimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError(DescribeError(request, url));
                yield break;
            }

            // 解析与回调分开：回调里的异常不应被误报成"响应解析失败"。
            T result = default;
            string parseError = null;
            try
            {
                result = JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
            catch (ArgumentException e)
            {
                parseError = e.Message;
            }

            if (parseError != null || result == null)
            {
                onError("服务器响应无法解析：" + parseError);
                yield break;
            }
            onSuccess(result);
        }

        static string DescribeError(UnityWebRequest request, string url)
        {
            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                // 4xx/5xx：优先显示后端返回的错误信息，例如 "title 不能为空"
                var message = TryReadErrorMessage(request.downloadHandler?.text);
                var kind = request.responseCode == 400 ? "参数错误" : "服务器错误";
                return string.IsNullOrEmpty(message)
                    ? $"{kind}（HTTP {request.responseCode}）"
                    : $"{kind}（HTTP {request.responseCode}）：{message}";
            }

            // ConnectionError：常见原因是地址填错、手机和电脑不在同一网络、电脑防火墙拦截了端口
            return $"无法连接后端 {url}\n{request.error}\n请检查地址、是否同一 Wi-Fi、电脑防火墙";
        }

        static string TryReadErrorMessage(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                return JsonUtility.FromJson<ApiErrorEnvelope>(body)?.error?.message;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
