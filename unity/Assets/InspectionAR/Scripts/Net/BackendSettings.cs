using System;
using UnityEngine;

namespace InspectionAR.Net
{
    /// <summary>
    /// 后端地址保存在 PlayerPrefs 中：在 App 顶部输入框修改后立即生效，重启 App 后仍然保留。
    /// </summary>
    public static class BackendSettings
    {
        const string PrefsKey = "InspectionAR.BackendUrl";

        // 真机无法访问电脑的 localhost，需要改成电脑的局域网 IP（Go 启动日志会打印）。
        public const string DefaultUrl = "http://192.168.1.100:8080";

        public static string BaseUrl
        {
            get => PlayerPrefs.GetString(PrefsKey, DefaultUrl);
            set
            {
                PlayerPrefs.SetString(PrefsKey, Normalize(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>补全 http:// 前缀并去掉末尾斜杠，例如 "192.168.1.5:8080/" → "http://192.168.1.5:8080"。</summary>
        public static string Normalize(string raw)
        {
            var url = (raw ?? string.Empty).Trim();
            if (url.Length == 0) return DefaultUrl;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            return url.TrimEnd('/');
        }
    }
}
