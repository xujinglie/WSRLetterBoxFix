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
    /// WitchSpring R 会把 16:9 之外的多余屏幕用 4 个纯黑 UI 面板（LetterBox_Left/Right/Up/Down）
    /// 盖住，形成"黑边"。本插件每帧把这些面板隐藏掉，让画面铺满整个窗口。
    ///
    /// 注意：相机视口（camera.rect）的 16:9 裁剪是另一处逻辑，需要配合相机修复插件一起使用。
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "witchspringr.letterboxfix";
        public const string Name = "WitchSpring R LetterBox Fix";
        public const string Version = "1.0.0";

        internal static ManualLogSource LogSource;
        internal static ConfigEntry<bool> Enabled;

        public override void Load()
        {
            LogSource = Log;
            Enabled = Config.Bind("General", "Enabled", true,
                "隐藏游戏画在 21:9 屏幕两侧的 LetterBox 黑边面板。");
            Log.LogInfo(Name + " v" + Version + " loaded");
            AddComponent<LetterBoxBehaviour>();
        }
    }

    internal class LetterBoxBehaviour : MonoBehaviour
    {
        private readonly List<Transform> _bars = new List<Transform>();
        private float _nextScan;
        private bool _logged;
        private int _hidden;

        private void LateUpdate()
        {
            try
            {
                if (Plugin.Enabled.Value != true) return;

                // 场景切换后对象会被重建，所以定期重扫
                if (Time.unscaledTime >= _nextScan || _bars.Count == 0)
                {
                    _nextScan = Time.unscaledTime + 2f;
                    Scan();
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

                if (!_logged && _bars.Count > 0 && _hidden > 0)
                {
                    _logged = true;
                    Plugin.LogSource?.LogInfo("[LetterBox] 已隐藏 " + _bars.Count + " 个黑边面板: " + Names());
                }
            }
            catch { }
        }

        private string Names()
        {
            var s = "";
            for (int i = 0; i < _bars.Count && i < 8; i++)
            {
                if (_bars[i] != null) s += _bars[i].name + " ";
            }
            return s.Trim();
        }

        private void Scan()
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
                    try { Collect(cv.transform, 0); } catch { }
                }
            }
            catch { }
        }

        private void Collect(Transform t, int depth)
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
                    if (ch != null) Collect(ch, depth + 1);
                }
            }
            catch { }
        }
    }
}
