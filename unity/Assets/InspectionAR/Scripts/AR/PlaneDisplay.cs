using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.AR
{
    /// <summary>
    /// 控制平面的显示和检测：
    /// - 锁定：停止检测新平面，不再扩展、合并，画面不再"一直在变"；已识别的平面仍可点击放置；
    /// - 淡化：放下标记或锁定后，平面只保留浅色轮廓，突出标记本身。
    /// 颜色通过 MaterialPropertyBlock 设置，不修改共享材质资源。
    /// </summary>
    public class PlaneDisplay
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        // 与场景生成时创建的 PlaneFill / PlaneEdge 材质颜色一致
        static readonly Color FillColor = new Color(0.1f, 0.8f, 1f, 0.22f);
        static readonly Color EdgeColor = new Color(0.1f, 0.8f, 1f, 0.9f);
        const float FadedFill = 0.15f;  // 淡化后填充透明度倍率
        const float FadedEdge = 0.35f;
        const float FadeSpeed = 4f;     // 每秒变化量

        readonly ARPlaneManager planeManager;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        float visibility = 1f; // 1 = 正常显示，0 = 淡化

        public bool Locked { get; private set; }

        public PlaneDisplay(ARPlaneManager planeManager)
        {
            this.planeManager = planeManager;
        }

        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (planeManager != null)
                planeManager.requestedDetectionMode = locked ? PlaneDetectionMode.None : PlaneDetectionMode.Horizontal;
        }

        public void Tick(bool faded, float deltaTime)
        {
            if (planeManager == null) return;
            var target = faded || Locked ? 0f : 1f;
            visibility = Mathf.MoveTowards(visibility, target, FadeSpeed * deltaTime);

            var fill = FillColor;
            fill.a *= Mathf.Lerp(FadedFill, 1f, visibility);
            var edge = EdgeColor;
            edge.a *= Mathf.Lerp(FadedEdge, 1f, visibility);

            foreach (var plane in planeManager.trackables)
            {
                Apply(plane.GetComponent<MeshRenderer>(), fill);
                Apply(plane.GetComponent<LineRenderer>(), edge);
            }
        }

        void Apply(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
