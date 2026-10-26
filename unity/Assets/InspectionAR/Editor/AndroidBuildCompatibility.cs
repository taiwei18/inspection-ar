#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace InspectionAR.EditorTools
{
    /// <summary>ARCore 6.0.x 的两个 AAR 共用命名空间；兼容 Unity 6 LTS 新增的 AGP 9 检查。</summary>
    public sealed class AndroidBuildCompatibility : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var properties = Path.Combine(Directory.GetParent(path).FullName, "gradle.properties");
            const string setting = "android.uniquePackageNames=false";
            var content = File.ReadAllText(properties);
            if (!content.Contains(setting))
                File.AppendAllText(properties, "\n# ARCore 6.0.x AAR namespace compatibility with AGP 9.\n" + setting + "\n");
        }
    }
}
#endif
