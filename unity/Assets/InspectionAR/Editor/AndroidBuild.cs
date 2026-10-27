using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InspectionAR.EditorTools
{
    /// <summary>
    /// 构建 Android APK，输出到 unity/Builds/InspectionAR.apk。
    /// 编辑器中用菜单执行；命令行用：
    ///   Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod InspectionAR.EditorTools.AndroidBuild.BuildApk
    /// </summary>
    public static class AndroidBuild
    {
        const string ApkPath = "Builds/InspectionAR.apk";

        [MenuItem("Inspection AR/3. 构建 APK", priority = 3)]
        public static void BuildApk()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("构建场景列表为空，请先执行「Inspection AR → 2. 生成 AR 场景」");
                return;
            }

            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"APK 构建失败：{report.summary.result}，错误 {report.summary.totalErrors} 个，详见 Editor.log");
                return;
            }
            Debug.Log($"Inspection AR: APK 已生成 {ApkPath}（{new System.IO.FileInfo(ApkPath).Length / 1024 / 1024} MB）");
        }

        static void Fail(string message)
        {
            Debug.LogError("Inspection AR: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1); // 让命令行拿到非 0 退出码
        }
    }
}
