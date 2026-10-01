using System;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// 把 LiveTimeline 的 Transform 轨道（OnUpdateTransform）应用到 StageObjectMap 里对应的 GameObject 上
    /// 关键点：支持 name 带路径（/ \ :) 的情况,自动取最后一段来匹配子物体名

    public class StageTransformDriver : MonoBehaviour
    {
        public bool verboseLog = false;

        [Header("Debug (optional)")]
        public bool debugPrint = false;
        public string debugNameContains = "washlight"; // 改成 "truss" / "laser" / "spot" 等
        public int debugMaxLogsPerFrame = 10;

        private LiveTimelineControl _ctl;
        private StageController _stage;

        private int _dbgFrame = -1;
        private int _dbgCount = 0;

        private void OnEnable()
        {
            BindIfPossible();
        }

        private void LateUpdate()
        {
            // Director/Stage 在进入 live 后可能才有，LateUpdate 里兜底绑定
            if (_ctl == null || _stage == null)
                BindIfPossible();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void BindIfPossible()
        {
            var dir = Director.instance;
            if (!dir) return;

            var ctl = dir._liveTimelineControl;
            var stage = dir._stageController;
            if (ctl == null || stage == null) return;

            if (_ctl == ctl && _stage == stage) return;

            Unbind();

            _ctl = ctl;
            _stage = stage;

            _ctl.OnUpdateTransform += OnUpdateTransform;

            if (verboseLog)
                Debug.Log("[StageTransformDriver] bound");
        }

        private void Unbind()
        {
            if (_ctl != null)
                _ctl.OnUpdateTransform -= OnUpdateTransform;

            _ctl = null;
            _stage = null;
        }

        private void OnUpdateTransform(ref TransformUpdateInfo info)
        {
            if (_stage == null || _stage.StageObjectMap == null) return;
            if (info.data == null) return;

            string raw = info.data.name;
            if (string.IsNullOrEmpty(raw)) return;

            if (!TryResolveStageObject(raw, out var go) || go == null) return;

            // 这里用 local*，因为 timeline 给的是局部变换（和你贴的 AlterUpdate_TransformControl 一致）
            var tr = go.transform;
            tr.localPosition = info.updateData.position;
            tr.localRotation = info.updateData.rotation;
            tr.localScale    = info.updateData.scale;

            if (debugPrint && ShouldDebug(raw))
            {
                int f = Time.frameCount;
                if (_dbgFrame != f) { _dbgFrame = f; _dbgCount = 0; }
                if (_dbgCount++ < Mathf.Max(1, debugMaxLogsPerFrame))
                {
                    Debug.Log($"[StageTransformDriver] frame={f} name={raw} -> go={go.name} pos={tr.localPosition} rot={tr.localEulerAngles}");
                }
            }
        }

        private bool ShouldDebug(string rawName)
        {
            if (string.IsNullOrEmpty(debugNameContains)) return true;
            return rawName.IndexOf(debugNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 把 timeline 的 name 映射到 StageObjectMap 里的 GameObject。
        /// 适配：直接名字 / 带 (Clone) / 带路径 / 带 ':' '\' '/' 等分隔符
        /// </summary>
        private bool TryResolveStageObject(string raw, out GameObject go)
        {
            go = null;

            // 1) 原样匹配
            if (_stage.StageObjectMap.TryGetValue(raw, out go) && go) return true;

            // 2) 去掉 (Clone)
            string n = StripClone(raw);
            if (!string.Equals(n, raw, StringComparison.Ordinal))
            {
                if (_stage.StageObjectMap.TryGetValue(n, out go) && go) return true;
            }

            // 3) 带路径：取最后一段
            // 常见分隔符：/  \  :
            string last = TakeLastSegment(raw);
            if (!string.IsNullOrEmpty(last))
            {
                last = StripClone(last);
                if (_stage.StageObjectMap.TryGetValue(last, out go) && go) return true;
            }

            // 4) 还有一种情况：name 里带了多段路径，尝试从后往前逐段试（更鲁棒，但仍便宜）
            // 例如 a/b/c -> 试 c、b、a
            if (raw.IndexOf('/') >= 0 || raw.IndexOf('\\') >= 0 || raw.IndexOf(':') >= 0)
            {
                var parts = raw.Split(new[] { '/', '\\', ':' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    var p = StripClone(parts[i]);
                    if (_stage.StageObjectMap.TryGetValue(p, out go) && go) return true;
                }
            }

            return false;
        }

        private static string StripClone(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("(Clone)", "");
        }

        private static string TakeLastSegment(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            int a = raw.LastIndexOf('/');
            int b = raw.LastIndexOf('\\');
            int c = raw.LastIndexOf(':');

            int idx = Mathf.Max(a, Mathf.Max(b, c));
            if (idx < 0 || idx >= raw.Length - 1) return null;

            return raw.Substring(idx + 1);
        }
    }
}
