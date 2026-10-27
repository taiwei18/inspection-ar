using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.AR
{
    public enum PlacementLevel
    {
        Good,
        Fair,
        Poor,
    }

    public readonly struct PlacementAssessment
    {
        public readonly bool Allowed;
        public readonly PlacementLevel Level;
        public readonly string Message;

        public PlacementAssessment(bool allowed, PlacementLevel level, string message)
        {
            Allowed = allowed;
            Level = level;
            Message = message;
        }
    }

    /// <summary>
    /// 评估点击位置是否适合放置标记。ARCore 依靠画面中的特征点定位，
    /// 刚识别出来的平面、面积很小的平面、缺少纹理的位置，转开再回来时最容易偏。
    /// </summary>
    public class PlacementQuality
    {
        const float MinPlaneAgeSeconds = 1f;   // 平面刚出现时位置和高度还在快速修正
        const float MinPlaneSize = 0.15f;      // 米；太小的平面估计不稳定
        const float FeatureRadius = 0.3f;      // 统计落点周围 30cm 内的特征点
        const int GoodFeatureCount = 12;
        const int FairFeatureCount = 4;

        readonly ARPlaneManager planeManager;
        readonly ARPointCloudManager pointCloudManager;
        readonly Dictionary<TrackableId, float> planeFirstSeen = new Dictionary<TrackableId, float>();

        public PlacementQuality(ARPlaneManager planeManager, ARPointCloudManager pointCloudManager)
        {
            this.planeManager = planeManager;
            this.pointCloudManager = pointCloudManager;
        }

        /// <summary>每帧调用，记录每个平面第一次出现的时间。</summary>
        public void Tick(float now)
        {
            if (planeManager == null) return;
            foreach (var plane in planeManager.trackables)
            {
                if (!planeFirstSeen.ContainsKey(plane.trackableId)) planeFirstSeen[plane.trackableId] = now;
            }
        }

        public PlacementAssessment Evaluate(ARPlane plane, Vector3 point, float now)
        {
            var age = planeFirstSeen.TryGetValue(plane.trackableId, out var seen) ? now - seen : 0f;
            if (age < MinPlaneAgeSeconds)
                return new PlacementAssessment(false, PlacementLevel.Poor, "这块平面刚识别出来，请对准它再停留 1 秒");
            if (Mathf.Min(plane.size.x, plane.size.y) < MinPlaneSize)
                return new PlacementAssessment(false, PlacementLevel.Poor, "识别到的平面太小，请缓慢扫描让平面扩大后再放置");

            var features = CountFeaturePoints(point, FeatureRadius);
            if (features >= GoodFeatureCount)
                return new PlacementAssessment(true, PlacementLevel.Good, $"定位质量：良好（附近特征点 {features} 个）");
            if (features >= FairFeatureCount)
                return new PlacementAssessment(true, PlacementLevel.Fair, $"定位质量：一般（附近特征点 {features} 个），转开再回来可能有少量偏差");
            return new PlacementAssessment(true, PlacementLevel.Poor,
                $"定位质量：较差（附近特征点 {features} 个）。此处纹理太少，建议放在有图案、接缝或杂物附近");
        }

        /// <summary>统计 ARCore 当前特征点中，距离 center 不超过 radius 的数量。</summary>
        int CountFeaturePoints(Vector3 center, float radius)
        {
            if (pointCloudManager == null) return 0;
            var count = 0;
            var sqrRadius = radius * radius;
            foreach (var cloud in pointCloudManager.trackables)
            {
                var positions = cloud.positions;
                if (!positions.HasValue) continue;
                var toWorld = cloud.transform; // 点云坐标位于会话空间，需要转换到世界坐标
                foreach (var local in positions.Value)
                {
                    if ((toWorld.TransformPoint(local) - center).sqrMagnitude <= sqrRadius) count++;
                }
            }
            return count;
        }
    }
}
