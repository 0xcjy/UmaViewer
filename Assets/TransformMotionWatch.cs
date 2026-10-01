using UnityEngine;
using System.Text;

[DisallowMultipleComponent]
public class TransformMotionWatch : MonoBehaviour
{
    public bool logPosition = true;
    public bool logRotation = true;
    public bool logScale = false;
    public bool logLightParams = true;

    public float posEps = 1e-4f;
    public float rotEpsDeg = 0.02f;
    public float scaleEps = 1e-4f;

    public int logEveryNFrames = 1;
    public bool pauseOnFirstChange = false;

    Vector3 _lastPos;
    Quaternion _lastRot;
    Vector3 _lastScale;

    Light _light;
    float _lastIntensity;
    Color _lastColor;
    float _lastSpotAngle;

    int _frame;

    void Awake()
    {
        _lastPos = transform.position;
        _lastRot = transform.rotation;
        _lastScale = transform.localScale;

        _light = GetComponent<Light>();
        if (_light != null)
        {
            _lastIntensity = _light.intensity;
            _lastColor = _light.color;
            _lastSpotAngle = _light.spotAngle;
        }
    }

    void LateUpdate()
    {
        _frame++;
        if (logEveryNFrames > 1 && (_frame % logEveryNFrames != 0)) return;

        bool changed = false;
        var sb = new StringBuilder();

        if (logPosition)
        {
            var p = transform.position;
            if ((p - _lastPos).sqrMagnitude > posEps * posEps)
            {
                changed = true;
                sb.Append($"pos {_lastPos} -> {p}; ");
                _lastPos = p;
            }
        }

        if (logRotation)
        {
            var r = transform.rotation;
            float ang = Quaternion.Angle(_lastRot, r);
            if (ang > rotEpsDeg)
            {
                changed = true;
                sb.Append($"rot Δ{ang:F3}deg; ");
                _lastRot = r;
            }
        }

        if (logScale)
        {
            var s = transform.localScale;
            if ((s - _lastScale).sqrMagnitude > scaleEps * scaleEps)
            {
                changed = true;
                sb.Append($"scale {_lastScale} -> {s}; ");
                _lastScale = s;
            }
        }

        if (logLightParams && _light != null)
        {
            if (Mathf.Abs(_light.intensity - _lastIntensity) > 1e-4f)
            {
                changed = true;
                sb.Append($"int {_lastIntensity:F3}->{_light.intensity:F3}; ");
                _lastIntensity = _light.intensity;
            }
            if ((_light.color - _lastColor).maxColorComponent > 1e-4f)
            {
                changed = true;
                sb.Append($"color {_lastColor}->{_light.color}; ");
                _lastColor = _light.color;
            }
            if (Mathf.Abs(_light.spotAngle - _lastSpotAngle) > 1e-4f)
            {
                changed = true;
                sb.Append($"spot {_lastSpotAngle:F2}->{_light.spotAngle:F2}; ");
                _lastSpotAngle = _light.spotAngle;
            }
        }

        if (!changed) return;

        string path = GetPath(transform);
        string hints = GetDriverHints(gameObject);
        Debug.Log($"[MotionWatch] {path} changed: {sb}{hints}", this);

        if (pauseOnFirstChange)
        {
            Debug.Break();
            pauseOnFirstChange = false;
        }
    }

    static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }

    static string GetDriverHints(GameObject go)
    {
        // 向上找几层：Animator/Animation/PlayableDirector + 自定义 Driver/Cutt/Timeline 脚本
        var sb = new StringBuilder();
        Transform t = go.transform;
        int depth = 0;

        while (t != null && depth < 8)
        {
            var anim = t.GetComponent<Animator>();
            var animation = t.GetComponent<Animation>();
            var pd = t.GetComponent<UnityEngine.Playables.PlayableDirector>();

            bool any = anim || animation || pd;
            if (any) sb.Append($" | driver@{t.name}:");
            if (anim) sb.Append(" Animator");
            if (animation) sb.Append(" Animation");
            if (pd) sb.Append(" PlayableDirector");

            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (!mb) continue;
                string n = mb.GetType().Name;
                if (n.Contains("Driver") || n.Contains("Cutt") || n.Contains("Timeline"))
                    sb.Append($" [{n}]");
            }

            t = t.parent;
            depth++;
        }

        return sb.ToString();
    }
}
