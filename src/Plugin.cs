using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WSRLetterBoxFix
{
    /// <summary>
    /// WitchSpring R 超宽屏修复（相机 + 黑边二合一）。
    ///
    /// 游戏在 21:9 / 32:9 屏幕上出现黑边，是两处逻辑叠加造成的：
    ///   1. 主相机视口被裁成 16:9（camera.rect ≈ (0.128, 0, 0.744, 1)）
    ///   2. Canvas 下挂着 4 个纯黑 UI 面板 LetterBox_Left/Right/Up/Down
    ///
    /// 本插件每帧把相机拉回整窗，并把这些黑边面板隐藏掉。
    /// 只操作运行时对象，不修改任何游戏文件。
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "witchspringr.letterboxfix";
        public const string Name = "WitchSpring R Ultrawide Fix";
        public const string Version = "2.0.0";

        internal static ManualLogSource LogSource;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> HideLetterBox;
        internal static ConfigEntry<bool> ForceCamera;
        internal static ConfigEntry<bool> ForceAspect;
        internal static ConfigEntry<bool> ForceResolution;
        internal static ConfigEntry<int> ResWidth;
        internal static ConfigEntry<int> ResHeight;

        public override void Load()
        {
            LogSource = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "总开关。关闭后本插件不修改任何东西。");
            HideLetterBox = Config.Bind("General", "HideLetterBox", true,
                "隐藏游戏用 UI 画的 LetterBox 黑边面板（LetterBox_Left/Right/Up/Down）。");
            ForceCamera = Config.Bind("Camera", "ForceCamera", true,
                "每帧把相机视口拉回 (0,0,1,1)，去掉游戏对相机的 16:9 裁剪。");
            ForceAspect = Config.Bind("Camera", "ForceAspect", true,
                "让相机纵横比跟随窗口（推荐）。设为 false 则保持 16:9 投影，画面会被横向拉伸。");
            ForceResolution = Config.Bind("Resolution", "ForceResolution", false,
                "强制把游戏渲染分辨率设为 ResWidth x ResHeight（默认关，一般不需要）。");
            ResWidth = Config.Bind("Resolution", "ResWidth", 3440,
                "ForceResolution 生效时使用的宽度。");
            ResHeight = Config.Bind("Resolution", "ResHeight", 1440,
                "ForceResolution 生效时使用的高度。");

            Log.LogInfo(Name + " v" + Version + " 已加载（相机修复 + 黑边隐藏）");
            Log.LogInfo("配置：BepInEx\\config\\witchspringr.letterboxfix.cfg（改完重启游戏生效）");
            AddComponent<UltrawideBehaviour>();
        }
    }

    internal class UltrawideBehaviour : MonoBehaviour
    {
        // ---- 黑边面板 ----
        private readonly List<Transform> _bars = new List<Transform>();
        private float _nextScan;
        private bool _barsLogged;
        private int _hidden;

        // ---- 相机 ----
        private Camera _cam;
        private int _lastW = -1;
        private int _lastH = -1;
        private bool _camLogged;
        private bool _errorLogged;
        private int _reverts;
        private float _nextReport;
        private float _nextResCheck;
        private bool _resUnsupportedLogged;

        private void LateUpdate()
        {
            try
            {
                if (Plugin.Enabled.Value != true) return;

                if (Plugin.ForceResolution.Value == true) TryForceResolution();
                if (Plugin.ForceCamera.Value == true) ApplyCamera();
                if (Plugin.HideLetterBox.Value == true) HideBars();
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.LogSource?.LogWarning("[超宽屏] 异常: " + e.GetType().Name + " " + e.Message);
                }
            }
        }

        // ================= 黑边面板 =================

        private void HideBars()
        {
            // 场景切换后对象会被重建，定期重扫
            if (Time.unscaledTime >= _nextScan || _bars.Count == 0)
            {
                _nextScan = Time.unscaledTime + 2f;
                ScanBars();
            }

            for (int i = 0; i < _bars.Count; i++)
            {
                var t = _bars[i];
                if (t == null || t.Pointer == IntPtr.Zero) continue;
                try
                {
                    var go = t.gameObject;
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        _hidden++;
                    }
                }
                catch { }
            }

            if (!_barsLogged && _bars.Count > 0 && _hidden > 0)
            {
                _barsLogged = true;
                Plugin.LogSource?.LogInfo("[黑边] 已隐藏 " + _bars.Count + " 个黑边面板: " + BarNames());
            }
        }

        private string BarNames()
        {
            var s = "";
            for (int i = 0; i < _bars.Count && i < 8; i++)
            {
                if (_bars[i] != null) s += _bars[i].name + " ";
            }
            return s.Trim();
        }

        private void ScanBars()
        {
            _bars.Clear();
            try
            {
                var canvases = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Canvas>());
                if (canvases == null) return;
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas cv = null;
                    try { cv = canvases[i]?.TryCast<Canvas>(); } catch { }
                    if (cv == null) continue;
                    try { CollectBars(cv.transform, 0); } catch { }
                }
            }
            catch { }
        }

        private void CollectBars(Transform t, int depth)
        {
            if (t == null || depth > 6) return;
            try
            {
                string n = t.name;
                if (n != null && n.StartsWith("LetterBox", StringComparison.OrdinalIgnoreCase))
                {
                    _bars.Add(t);
                    return;
                }
                int c = t.childCount;
                for (int i = 0; i < c; i++)
                {
                    Transform ch = null;
                    try { ch = t.GetChild(i); } catch { }
                    if (ch != null) CollectBars(ch, depth + 1);
                }
            }
            catch { }
        }

        // ================= 相机 =================

        private void ApplyCamera()
        {
            var cam = GetCamera();
            if (cam == null) return;

            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return;

            // 每 5 秒汇报一次"被游戏改回去"的次数，用来判断有没有在拉锯
            if (Time.unscaledTime >= _nextReport)
            {
                _nextReport = Time.unscaledTime + 5f;
                if (_camLogged && _reverts > 0)
                {
                    Plugin.LogSource?.LogInfo("[相机] 过去 5 秒被游戏改回 " + _reverts + " 次（已重新拉满）");
                    _reverts = 0;
                }
            }

            var full = new Rect(0f, 0f, 1f, 1f);
            float targetAspect = (float)w / h;

            var cur = cam.rect;
            bool rectOff = Mathf.Abs(cur.x) > 0.0005f || Mathf.Abs(cur.y) > 0.0005f
                           || Mathf.Abs(cur.width - 1f) > 0.0005f || Mathf.Abs(cur.height - 1f) > 0.0005f;
            bool aspectOff = Plugin.ForceAspect.Value == true && Mathf.Abs(cam.aspect - targetAspect) > 0.0005f;

            if (!rectOff && !aspectOff) return;

            if (_camLogged) _reverts++;

            float beforeAspect = cam.aspect;
            string beforeRect = Fmt(cur);

            cam.rect = full;
            if (Plugin.ForceAspect.Value == true) cam.aspect = targetAspect;

            // 只在第一次或窗口尺寸变化时写日志，避免每帧刷屏
            if (!_camLogged || w != _lastW || h != _lastH)
            {
                _camLogged = true;
                _lastW = w;
                _lastH = h;
                Plugin.LogSource?.LogInfo("[相机] 窗口 " + w + "x" + h
                    + "，rect " + beforeRect + " -> (0,0,1,1)"
                    + "，aspect " + beforeAspect.ToString("0.###") + " -> " + cam.aspect.ToString("0.###"));
            }
        }

        private static string Fmt(Rect r)
        {
            return string.Format("({0:0.###},{1:0.###},{2:0.###},{3:0.###})", r.x, r.y, r.width, r.height);
        }

        /// <summary>
        /// 可选：强制渲染分辨率。默认关，只允许切换到 Screen.resolutions 里真实存在的模式。
        /// </summary>
        private void TryForceResolution()
        {
            if (Time.unscaledTime < _nextResCheck) return;
            _nextResCheck = Time.unscaledTime + 0.5f;

            int w = Plugin.ResWidth.Value;
            int h = Plugin.ResHeight.Value;
            if (w <= 0 || h <= 0) return;
            if (Screen.width == w && Screen.height == h) return;

            bool supported = false;
            try
            {
                var list = Screen.resolutions;
                if (list != null)
                {
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].width == w && list[i].height == h) { supported = true; break; }
                    }
                }
            }
            catch { }

            if (!supported)
            {
                if (!_resUnsupportedLogged)
                {
                    _resUnsupportedLogged = true;
                    Plugin.LogSource?.LogWarning("[分辨率] 显示器不支持 " + w + "x" + h + "，跳过 ForceResolution。");
                }
                return;
            }

            try
            {
                Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
                Plugin.LogSource?.LogInfo("[分辨率] 已强制 " + Screen.width + "x" + Screen.height + "（目标 " + w + "x" + h + "）");
            }
            catch (Exception e)
            {
                Plugin.LogSource?.LogWarning("[分辨率] SetResolution 失败: " + e.Message);
            }
        }

        private Camera GetCamera()
        {
            try
            {
                if (_cam != null && _cam.Pointer != IntPtr.Zero) return _cam;
            }
            catch { }

            Camera best = null;
            try
            {
                var arr = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Camera>());
                if (arr != null)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        Camera c = null;
                        try { c = arr[i]?.TryCast<Camera>(); } catch { }
                        if (c == null || c.Pointer == IntPtr.Zero) continue;

                        // 跳过渲染到 RenderTexture 的相机（画中画 / UI 相机）
                        bool hasRT = false;
                        try { var t = c.targetTexture; hasRT = t != null && t.Pointer != IntPtr.Zero; } catch { }
                        if (hasRT) continue;

                        // 取 depth 最大的那个作为主渲染相机
                        if (best == null || c.depth > best.depth) best = c;
                    }
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.LogSource?.LogWarning("[相机] 查找失败: " + e.GetType().Name + " " + e.Message);
                }
            }

            _cam = best;
            return _cam;
        }
    }
}
