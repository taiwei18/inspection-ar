using System.Collections.Generic;
using System.IO;
using InspectionAR.AR;
using InspectionAR.UI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.EditorTools
{
    /// <summary>
    /// 一键配置项目，避免手工在场景里摆放和连线：
    ///   菜单 Inspection AR / 1. 配置 Android 构建参数
    ///   菜单 Inspection AR / 2. 生成 AR 场景
    /// 生成的预制体、材质和场景都保存在 Assets/InspectionAR 下，可以重复执行。
    /// </summary>
    public static class InspectionProjectSetup
    {
        const string Root = "Assets/InspectionAR";
        const string GeneratedDir = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/InspectionAR.unity";

        [MenuItem("Inspection AR/1. 配置 Android 构建参数", priority = 1)]
        public static void ConfigureAndroid()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "InspectionDemo";
            PlayerSettings.productName = "Inspection AR";
            PlayerSettings.SetApplicationIdentifier(android, "com.inspectiondemo.inspectionar");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // ARCore 要求：64 位 IL2CPP、Android 7.0 (API 24) 以上、使用 OpenGLES3
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 24)
            {
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            }
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // 局域网内用 http 访问 Go 后端，需要允许明文 HTTP
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            AssetDatabase.SaveAssets();

            var todo = new List<string>
            {
                "Project Settings > XR Plug-in Management > Android 页签：勾选 ARCore",
                "XR Plug-in Management > Project Validation：如有提示点击 Fix All",
                "File > Build Profiles：切换到 Android 平台",
            };
#if !ENABLE_INPUT_SYSTEM
            todo.Insert(0, "Player > Active Input Handling 改为 Input System Package 或 Both，并重启编辑器");
#endif
            EditorUtility.DisplayDialog("Android 构建参数已配置", "还需要手动完成：\n\n- " + string.Join("\n- ", todo), "好的");
        }

        [MenuItem("Inspection AR/2. 生成 AR 场景", priority = 2)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(GeneratedDir);
            EnsureFolder(Root + "/Scenes");
            CreateMarkerPrefab();
            var planePrefab = CreatePlanePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Selection.activeObject = null;

            CreateLight();
            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            var origin = CreateXrOrigin();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("生成失败",
                    "没有找到 AR Foundation 的 XR Origin 菜单。请确认已安装 AR Foundation，" +
                    "或手动在 Hierarchy 中右键 XR > XR Origin (Mobile AR) 后重新执行本菜单。", "好的");
                return;
            }
            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // AR 能力都挂在 XR Origin 上：平面检测（仅水平面）、射线检测、锚点、点击放置
            var planeManager = GetOrAdd<ARPlaneManager>(origin.gameObject);
            planeManager.planePrefab = planePrefab;
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            GetOrAdd<ARRaycastManager>(origin.gameObject);
            GetOrAdd<ARAnchorManager>(origin.gameObject);
            var tapPlacer = GetOrAdd<ArTapPlacer>(origin.gameObject);

            var app = new GameObject("Inspection App");
            var ui = app.AddComponent<IssueReportUI>();
            var controller = app.AddComponent<InspectionAppController>();
            // 创建其他资源与新场景可能触发资产重导入，在赋值时获取当前的持久化组件。
            var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedDir + "/IssueMarker.prefab")
                .GetComponent<IssueMarker>();
            if (markerPrefab == null || !EditorUtility.IsPersistent(markerPrefab))
                throw new System.InvalidOperationException("标记预制体引用无效，无法保存 AR 场景");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("tapPlacer").objectReferenceValue = tapPlacer;
            serialized.FindProperty("planeManager").objectReferenceValue = planeManager;
            serialized.FindProperty("markerPrefab").objectReferenceValue = markerPrefab;
            serialized.FindProperty("ui").objectReferenceValue = ui;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorUtility.DisplayDialog("AR 场景已生成", $"场景：{ScenePath}\n已加入 Build 场景列表。", "好的");
        }

        /// <summary>优先使用 AR Foundation 自带菜单创建 XR Origin，相机与 TrackedPoseDriver 都由官方配置好。</summary>
        static XROrigin CreateXrOrigin()
        {
            string[] menuPaths = { "GameObject/XR/XR Origin (Mobile AR)", "GameObject/XR/XR Origin (AR)" };
            foreach (var path in menuPaths)
            {
                if (EditorApplication.ExecuteMenuItem(path)) break;
            }
            return Object.FindFirstObjectByType<XROrigin>();
        }

        static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>标记 = 底座 + 细杆 + 球头，高约 20cm，方便在现场远处看到。</summary>
        static IssueMarker CreateMarkerPrefab()
        {
            var material = CreateMaterial("MarkerMaterial", "Standard", Color.white);
            var root = new GameObject("IssueMarker");
            AddPart(root, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.002f, 0f), new Vector3(0.08f, 0.002f, 0.08f), material);
            AddPart(root, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 0.08f, 0f), new Vector3(0.01f, 0.08f, 0.01f), material);
            AddPart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.18f, 0f), Vector3.one * 0.05f, material);
            root.AddComponent<IssueMarker>();

            var prefabPath = GeneratedDir + "/IssueMarker.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            // 首次创建后重新导入、加载资产，避免序列化到已销毁的临时组件引用。
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var marker = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<IssueMarker>();
            if (marker == null) throw new System.InvalidOperationException("生成的标记预制体缺少 IssueMarker 组件");
            return marker;
        }

        static void AddPart(GameObject parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // 放置依赖 AR 射线检测，不需要物理碰撞体
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>检测到的平面：半透明青色填充 + 边线，用来让用户看到哪里可以点击。</summary>
        static GameObject CreatePlanePrefab()
        {
            var fill = CreateMaterial("PlaneFill", "Sprites/Default", new Color(0.1f, 0.8f, 1f, 0.22f));
            var edge = CreateMaterial("PlaneEdge", "Sprites/Default", new Color(0.1f, 0.8f, 1f, 0.9f));

            var plane = new GameObject("DetectedPlane",
                typeof(ARPlane), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer), typeof(ARPlaneMeshVisualizer));
            var meshRenderer = plane.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = fill;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var line = plane.GetComponent<LineRenderer>();
            line.sharedMaterial = edge;
            line.widthMultiplier = 0.005f;
            line.useWorldSpace = false;
            line.loop = true;
            line.shadowCastingMode = ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(plane, GeneratedDir + "/DetectedPlane.prefab");
            Object.DestroyImmediate(plane);
            return prefab;
        }

        static Material CreateMaterial(string name, string shaderName, Color color)
        {
            var path = $"{GeneratedDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new System.InvalidOperationException($"找不到 Shader \"{shaderName}\"，本项目使用内置渲染管线（Built-in RP）。");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
