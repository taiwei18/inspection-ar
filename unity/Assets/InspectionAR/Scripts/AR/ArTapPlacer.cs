using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace InspectionAR.AR
{
    /// <summary>
    /// 监听屏幕点击：点在 UI 上（按钮、输入框、面板）时忽略；
    /// 否则对 AR 平面做射线检测，命中水平面时触发 PlaneTapped。
    /// 挂在 XR Origin 上，与 ARRaycastManager 同一个物体。
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class ArTapPlacer : MonoBehaviour
    {
        public event Action<Pose> PlaneTapped;

        static readonly List<ARRaycastHit> ArHits = new List<ARRaycastHit>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        ARRaycastManager raycastManager;

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
        }

        void Update()
        {
            if (!TryGetPressPosition(out var screenPosition)) return;
            if (IsBlockedByUi(screenPosition)) return;

            // PlaneWithinPolygon：只接受落在已识别平面多边形内部的点，避免点到平面延长线上。
            if (!raycastManager.Raycast(screenPosition, ArHits, TrackableType.PlaneWithinPolygon)) return;

            foreach (var hit in ArHits) // 结果已按距离由近到远排序
            {
                if (hit.trackable is ARPlane plane && plane.alignment.IsHorizontal())
                {
                    PlaneTapped?.Invoke(hit.pose);
                    return;
                }
            }
        }

        /// <summary>只在"按下"那一帧响应；真机用触摸，编辑器里用鼠标调试。</summary>
        static bool TryGetPressPosition(out Vector2 position)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// 判断这次点击是否属于 UI：
        /// 1. 软键盘打开时，点空白处只是为了收起键盘；
        /// 2. 输入框正处于编辑状态；
        /// 3. 点击位置下方有任何可接收射线的 UI 元素（按钮、输入框、面板背景）。
        /// 第 3 条直接用 EventSystem.RaycastAll 检测，不依赖具体的输入模块。
        /// </summary>
        bool IsBlockedByUi(Vector2 screenPosition)
        {
            if (TouchScreenKeyboard.visible) return true;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.TryGetComponent<InputField>(out var field) && field.isFocused) return true;

            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
