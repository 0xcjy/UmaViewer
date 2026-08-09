using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime wireframe display for generated skirt collision volumes.</summary>
public sealed class SkirtCollisionVisualizer : MonoBehaviour
{
    private const int Segments = 24;
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();
    private DynamicBoneCollider _collider;
    private bool _visible;
    private float _lastRadius = -1f;
    private float _lastHeight = -1f;

    private void Awake()
    {
        _collider = GetComponent<DynamicBoneCollider>();
        CreateLines();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (_visible && _collider != null &&
            (!Mathf.Approximately(_lastRadius, _collider.m_Radius) ||
             !Mathf.Approximately(_lastHeight, _collider.m_Height)))
            UpdateGeometry();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        foreach (LineRenderer line in _lines)
            line.enabled = visible;
        if (visible)
            UpdateGeometry();
    }

    private void CreateLines()
    {
        Color color = name.Contains("Hip") ? new Color(1f, 0.35f, 0.2f) : new Color(0.2f, 0.85f, 1f);
        for (int i = 0; i < 6; i++)
        {
            GameObject lineObject = new GameObject("Wire");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = i < 3;
            line.widthMultiplier = 0.006f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = SkirtCollisionWireMaterial.Instance;
            line.startColor = color;
            line.endColor = color;
            _lines.Add(line);
        }
    }

    private void UpdateGeometry()
    {
        if (_collider == null)
            return;

        float radius = _collider.m_Radius;
        _lastRadius = radius;
        _lastHeight = _collider.m_Height;
        float halfSegment = Mathf.Max(0f, _collider.m_Height * 0.5f - radius);
        if (halfSegment == 0f)
        {
            SetCircle(_lines[0], radius, 0f, 0);
            SetCircle(_lines[1], radius, 0f, 1);
            SetCircle(_lines[2], radius, 0f, 2);
            for (int i = 3; i < _lines.Count; i++)
                _lines[i].positionCount = 0;
            return;
        }

        SetCircle(_lines[0], radius, -halfSegment, 0);
        SetCircle(_lines[1], radius, halfSegment, 0);
        _lines[2].positionCount = 0;
        SetLine(_lines[3], new Vector3(radius, 0f, -halfSegment), new Vector3(radius, 0f, halfSegment));
        SetLine(_lines[4], new Vector3(-radius, 0f, -halfSegment), new Vector3(-radius, 0f, halfSegment));
        SetLine(_lines[5], new Vector3(0f, radius, -halfSegment), new Vector3(0f, radius, halfSegment));
    }

    private static void SetCircle(LineRenderer line, float radius, float axisOffset, int plane)
    {
        line.positionCount = Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = Mathf.PI * 2f * i / Segments;
            float a = Mathf.Cos(angle) * radius;
            float b = Mathf.Sin(angle) * radius;
            switch (plane)
            {
                case 0: line.SetPosition(i, new Vector3(a, b, axisOffset)); break;
                case 1: line.SetPosition(i, new Vector3(a, axisOffset, b)); break;
                default: line.SetPosition(i, new Vector3(axisOffset, a, b)); break;
            }
        }
    }

    private static void SetLine(LineRenderer line, Vector3 from, Vector3 to)
    {
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }
}

internal static class SkirtCollisionWireMaterial
{
    private static Material _instance;

    public static Material Instance
    {
        get
        {
            if (_instance == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                _instance = new Material(shader);
            }
            return _instance;
        }
    }
}
