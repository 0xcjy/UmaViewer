using UnityEngine;
/// <summary>
/// 点歌机绿色框呼吸效果脚本
/// </summary>
[DisallowMultipleComponent]

public sealed class JukeboxSelectionPulse : MonoBehaviour
{
    [Range(0f, 0.15f)]
    public float ScaleAmount = 0.03f;

    [Min(0.1f)]
    public float CycleDuration = 1.1f;

    private Vector3 _baseScale;
    private float _startedAt;

    private void OnEnable()
    {
        _baseScale = transform.localScale;
        _startedAt = Time.unscaledTime;
    }

    private void Update()
    {
        float duration = Mathf.Max(0.1f, CycleDuration);
        float phase = (Time.unscaledTime - _startedAt) / duration * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;
        float scale = 1f + pulse * ScaleAmount;
        transform.localScale = _baseScale * scale;
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale == Vector3.zero ? Vector3.one : _baseScale;
    }
}
