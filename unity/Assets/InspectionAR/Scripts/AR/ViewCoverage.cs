using UnityEngine;

namespace InspectionAR.AR
{
    /// <summary>
    /// 记录用户从哪些方向看过标记：把标记周围水平一圈分成 8 个 45° 扇区，
    /// 标记在画面中、且手机离标记 0.25～4 米时，所在扇区记为已观察。
    /// 从多个角度观察过的区域，ARCore 记录的环境信息更完整，转开再回来时修正得更准。
    /// </summary>
    public class ViewCoverage
    {
        const int SectorCount = 8;
        const float MinDistance = 0.25f;
        const float MaxDistance = 4f;

        public const int RecommendedSectors = 3; // 约覆盖 90°～135° 的视角变化

        readonly bool[] seen = new bool[SectorCount];

        public int Covered { get; private set; }
        public bool IsEnough => Covered >= RecommendedSectors;

        public void Reset()
        {
            System.Array.Clear(seen, 0, seen.Length);
            Covered = 0;
        }

        public void Observe(Camera camera, Vector3 target)
        {
            if (camera == null) return;

            // 标记要在画面中间区域，才算真正"看着"它
            var viewport = camera.WorldToViewportPoint(target);
            if (viewport.z <= 0f || viewport.x < 0.15f || viewport.x > 0.85f || viewport.y < 0.15f || viewport.y > 0.85f) return;

            var offset = camera.transform.position - target;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance < MinDistance || distance > MaxDistance) return;

            var angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg + 180f; // 0～360
            var sector = Mathf.Clamp((int)(angle / (360f / SectorCount)), 0, SectorCount - 1);
            if (seen[sector]) return;
            seen[sector] = true;
            Covered++;
        }
    }
}
