#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CyalumeMeshDiagnostics
{
    [MenuItem("Tools/Cyalume/Dump Selected Mesh Filters")]
    private static void DumpSelectedMeshFilters()
    {
        var filters = CollectSelectedMeshFilters();
        if (filters.Count == 0)
        {
            Debug.LogWarning("CyalumeMeshDiagnostics: Select one or more GameObjects with MeshFilter components.");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"Cyalume mesh dump: {filters.Count} selection(s)");

        foreach (var filter in filters)
        {
            report.AppendLine(BuildMeshReport(filter));
            report.AppendLine(new string('-', 80));
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Cyalume/Compare First Two Selected Mesh Filters")]
    private static void CompareFirstTwoSelectedMeshFilters()
    {
        var filters = CollectSelectedMeshFilters();
        if (filters.Count < 2)
        {
            Debug.LogWarning("CyalumeMeshDiagnostics: Select at least two GameObjects with MeshFilter components.");
            return;
        }

        var left = filters[0];
        var right = filters[1];
        var leftMesh = left.sharedMesh;
        var rightMesh = right.sharedMesh;

        var report = new StringBuilder();
        report.AppendLine("Cyalume mesh compare");
        report.AppendLine($"Left : {left.name}");
        report.AppendLine($"Right: {right.name}");
        report.AppendLine();
        report.AppendLine(BuildMeshReport(left));
        report.AppendLine(new string('=', 80));
        report.AppendLine(BuildMeshReport(right));
        report.AppendLine(new string('=', 80));
        report.AppendLine(BuildComparison(leftMesh, rightMesh));

        Debug.Log(report.ToString());
    }

    private static List<MeshFilter> CollectSelectedMeshFilters()
    {
        var results = new List<MeshFilter>();
        var seen = new HashSet<MeshFilter>();

        foreach (var gameObject in Selection.gameObjects)
        {
            if (gameObject == null)
            {
                continue;
            }

            var filter = gameObject.GetComponent<MeshFilter>();
            if (filter != null && seen.Add(filter))
            {
                results.Add(filter);
            }
        }

        return results;
    }

    private static string BuildMeshReport(MeshFilter filter)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GameObject : {filter.name}");

        var renderer = filter.GetComponent<Renderer>();
        sb.AppendLine($"Renderer   : {(renderer != null ? renderer.enabled.ToString() : "missing")}");

        var mesh = filter.sharedMesh;
        if (mesh == null)
        {
            sb.AppendLine("Mesh       : <null>");
            return sb.ToString();
        }

        sb.AppendLine($"Mesh       : {mesh.name}");
        sb.AppendLine($"Readable   : {SafeRead(() => mesh.isReadable, false)}");
        sb.AppendLine($"Vertices   : {SafeRead(() => mesh.vertexCount, -1)}");
        sb.AppendLine($"SubMeshes  : {SafeRead(() => mesh.subMeshCount, -1)}");
        sb.AppendLine($"Bounds     : {SafeBounds(mesh)}");
        sb.AppendLine($"AssetPath  : {AssetDatabase.GetAssetPath(mesh)}");
        sb.AppendLine($"Importer   : {DescribeImporter(mesh)}");
        sb.AppendLine($"Attrs      : {BuildAttributeSummary(mesh)}");
        sb.AppendLine($"AttrLayout : {BuildAttributeLayout(mesh)}");
        sb.AppendLine($"RawData    : {BuildRawAttributeSummary(mesh)}");

        var uv = SafeArrayRead(() => mesh.uv);
        sb.AppendLine($"UV Count   : {(uv != null ? uv.Length.ToString() : "<unavailable>")}");

        var colors32 = SafeArrayRead(() => mesh.colors32);
        if (colors32 == null)
        {
            sb.AppendLine("Colors32   : <unavailable>");
        }
        else
        {
            sb.AppendLine($"Colors32   : {colors32.Length}");
            sb.AppendLine($"ColorStats : {BuildColorStats(colors32)}");
            sb.AppendLine($"ColorSample: {BuildColorSample(colors32, 8)}");
        }

        return sb.ToString();
    }

    private static string BuildComparison(Mesh left, Mesh right)
    {
        var sb = new StringBuilder();

        if (left == null || right == null)
        {
            sb.AppendLine("Comparison unavailable: one of the meshes is null.");
            return sb.ToString();
        }

        sb.AppendLine("Quick compare");
        sb.AppendLine($"Readable equal : {SafeRead(() => left.isReadable, false) == SafeRead(() => right.isReadable, false)}");
        sb.AppendLine($"Vertex count   : {SafeRead(() => left.vertexCount, -1)} vs {SafeRead(() => right.vertexCount, -1)}");
        sb.AppendLine($"UV count       : {SafeArrayRead(() => left.uv)?.Length ?? -1} vs {SafeArrayRead(() => right.uv)?.Length ?? -1}");
        sb.AppendLine($"Attrs          : {BuildAttributeSummary(left)} vs {BuildAttributeSummary(right)}");
        sb.AppendLine($"AttrLayout L   : {BuildAttributeLayout(left)}");
        sb.AppendLine($"AttrLayout R   : {BuildAttributeLayout(right)}");
        sb.AppendLine($"RawData L      : {BuildRawAttributeSummary(left)}");
        sb.AppendLine($"RawData R      : {BuildRawAttributeSummary(right)}");

        var leftColors = SafeArrayRead(() => left.colors32);
        var rightColors = SafeArrayRead(() => right.colors32);

        sb.AppendLine($"Colors count   : {leftColors?.Length ?? -1} vs {rightColors?.Length ?? -1}");

        if (leftColors != null && rightColors != null)
        {
            sb.AppendLine($"Alpha stats    : {BuildAlphaSummary(leftColors)} vs {BuildAlphaSummary(rightColors)}");
        }

        return sb.ToString();
    }

    private static string DescribeImporter(Mesh mesh)
    {
        var path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path))
        {
            return "<no importer>";
        }

        var importer = AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            return "<missing importer>";
        }

        if (importer is ModelImporter modelImporter)
        {
            return $"ModelImporter(readable={modelImporter.isReadable})";
        }

        return importer.GetType().Name;
    }

    private static string BuildRawAttributeSummary(Mesh mesh)
    {
        try
        {
            using var dataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            if (dataArray.Length == 0)
            {
                return "empty";
            }

            var data = dataArray[0];
            if (data.vertexCount <= 0)
            {
                return "vertexCount=0";
            }

            var descriptors = mesh.GetVertexAttributes();
            if (descriptors == null || descriptors.Length == 0)
            {
                return "no descriptors";
            }

            var offsetsByStream = new Dictionary<int, int>();
            var parts = new List<string>();

            foreach (var descriptor in descriptors)
            {
                if (!offsetsByStream.TryGetValue(descriptor.stream, out var offset))
                {
                    offset = 0;
                }

                var part = DescribeRawAttribute(data, descriptor, offset);
                parts.Add(part);
                offsetsByStream[descriptor.stream] = offset + GetFormatSize(descriptor.format) * descriptor.dimension;
            }

            return string.Join(" | ", parts);
        }
        catch (Exception ex)
        {
            return $"<error: {ex.Message}>";
        }
    }

    private static string DescribeRawAttribute(Mesh.MeshData data, VertexAttributeDescriptor descriptor, int offset)
    {
        var stride = data.GetVertexBufferStride(descriptor.stream);
        var raw = data.GetVertexData<byte>(descriptor.stream);
        if (!raw.IsCreated || raw.Length == 0)
        {
            return $"{descriptor.attribute}:<no data>";
        }

        switch (descriptor.attribute)
        {
            case VertexAttribute.Color:
                return $"{descriptor.attribute}:{DescribeColor(raw, data.vertexCount, stride, offset)}";
            case VertexAttribute.TexCoord0:
            case VertexAttribute.TexCoord1:
            case VertexAttribute.TexCoord2:
                return $"{descriptor.attribute}:{DescribeVector2(raw, data.vertexCount, stride, offset, descriptor.format)}";
            default:
                return $"{descriptor.attribute}:ok";
        }
    }

    private static string BuildAttributeSummary(Mesh mesh)
    {
        return
            $"pos={SafeHasAttr(mesh, VertexAttribute.Position)} " +
            $"nrm={SafeHasAttr(mesh, VertexAttribute.Normal)} " +
            $"tan={SafeHasAttr(mesh, VertexAttribute.Tangent)} " +
            $"col={SafeHasAttr(mesh, VertexAttribute.Color)} " +
            $"uv0={SafeHasAttr(mesh, VertexAttribute.TexCoord0)} " +
            $"uv1={SafeHasAttr(mesh, VertexAttribute.TexCoord1)} " +
            $"uv2={SafeHasAttr(mesh, VertexAttribute.TexCoord2)} " +
            $"uv3={SafeHasAttr(mesh, VertexAttribute.TexCoord3)}";
    }

    private static string BuildAttributeLayout(Mesh mesh)
    {
        try
        {
            var attributes = mesh.GetVertexAttributes();
            if (attributes == null || attributes.Length == 0)
            {
                return "empty";
            }

            var sb = new StringBuilder();
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (i > 0)
                {
                    sb.Append(" | ");
                }

                var stride = SafeRead(() => mesh.GetVertexBufferStride(attribute.stream), -1);
                sb.Append(
                    $"{attribute.attribute}:{attribute.format}x{attribute.dimension}@s{attribute.stream}/stride{stride}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"<error: {ex.Message}>";
        }
    }

    private static string DescribeColor(NativeArray<byte> raw, int vertexCount, int stride, int offset)
    {
        byte minR = byte.MaxValue;
        byte minG = byte.MaxValue;
        byte minB = byte.MaxValue;
        byte minA = byte.MaxValue;
        byte maxR = byte.MinValue;
        byte maxG = byte.MinValue;
        byte maxB = byte.MinValue;
        byte maxA = byte.MinValue;
        var zeroAlpha = 0;

        var sample = new StringBuilder();
        var sampleCount = Mathf.Min(vertexCount, 4);

        for (var i = 0; i < vertexCount; i++)
        {
            var baseIndex = i * stride + offset;
            var r = raw[baseIndex + 0];
            var g = raw[baseIndex + 1];
            var b = raw[baseIndex + 2];
            var a = raw[baseIndex + 3];

            if (r < minR) minR = r;
            if (g < minG) minG = g;
            if (b < minB) minB = b;
            if (a < minA) minA = a;
            if (r > maxR) maxR = r;
            if (g > maxG) maxG = g;
            if (b > maxB) maxB = b;
            if (a > maxA) maxA = a;
            if (a == 0) zeroAlpha++;

            if (i < sampleCount)
            {
                if (sample.Length > 0)
                {
                    sample.Append(",");
                }

                sample.Append($"({r},{g},{b},{a})");
            }
        }

        return $"R[{minR},{maxR}] G[{minG},{maxG}] B[{minB},{maxB}] A[{minA},{maxA}] zeroA={zeroAlpha}/{vertexCount} sample={sample}";
    }

    private static string DescribeVector2(NativeArray<byte> raw, int vertexCount, int stride, int offset, VertexAttributeFormat format)
    {
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var sample = new StringBuilder();
        var sampleCount = Mathf.Min(vertexCount, 4);

        for (var i = 0; i < vertexCount; i++)
        {
            var baseIndex = i * stride + offset;
            float x;
            float y;

            switch (format)
            {
                case VertexAttributeFormat.Float16:
                    x = HalfToFloat((ushort)(raw[baseIndex + 0] | (raw[baseIndex + 1] << 8)));
                    y = HalfToFloat((ushort)(raw[baseIndex + 2] | (raw[baseIndex + 3] << 8)));
                    break;
                case VertexAttributeFormat.Float32:
                    x = BitConverter.ToSingle(new[]
                    {
                        raw[baseIndex + 0],
                        raw[baseIndex + 1],
                        raw[baseIndex + 2],
                        raw[baseIndex + 3]
                    }, 0);
                    y = BitConverter.ToSingle(new[]
                    {
                        raw[baseIndex + 4],
                        raw[baseIndex + 5],
                        raw[baseIndex + 6],
                        raw[baseIndex + 7]
                    }, 0);
                    break;
                default:
                    return $"unsupported({format})";
            }

            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;

            if (i < sampleCount)
            {
                if (sample.Length > 0)
                {
                    sample.Append(",");
                }

                sample.Append($"({x:0.###},{y:0.###})");
            }
        }

        return $"x[{minX:0.###},{maxX:0.###}] y[{minY:0.###},{maxY:0.###}] sample={sample}";
    }

    private static int GetFormatSize(VertexAttributeFormat format)
    {
        switch (format)
        {
            case VertexAttributeFormat.Float32:
            case VertexAttributeFormat.UInt32:
            case VertexAttributeFormat.SInt32:
                return 4;
            case VertexAttributeFormat.Float16:
            case VertexAttributeFormat.UNorm16:
            case VertexAttributeFormat.SNorm16:
            case VertexAttributeFormat.UInt16:
            case VertexAttributeFormat.SInt16:
                return 2;
            case VertexAttributeFormat.UNorm8:
            case VertexAttributeFormat.SNorm8:
            case VertexAttributeFormat.UInt8:
            case VertexAttributeFormat.SInt8:
                return 1;
            default:
                return 0;
        }
    }

    private static float HalfToFloat(ushort value)
    {
        var sign = (value >> 15) & 0x1;
        var exponent = (value >> 10) & 0x1F;
        var mantissa = value & 0x3FF;

        if (exponent == 0)
        {
            if (mantissa == 0)
            {
                return sign == 0 ? 0f : -0f;
            }

            return (float)((sign == 0 ? 1 : -1) * Math.Pow(2, -14) * (mantissa / 1024.0));
        }

        if (exponent == 31)
        {
            if (mantissa == 0)
            {
                return sign == 0 ? float.PositiveInfinity : float.NegativeInfinity;
            }

            return float.NaN;
        }

        return (float)((sign == 0 ? 1 : -1) * Math.Pow(2, exponent - 15) * (1.0 + mantissa / 1024.0));
    }

    private static string SafeBounds(Mesh mesh)
    {
        try
        {
            var bounds = mesh.bounds;
            return $"center={bounds.center}, extents={bounds.extents}";
        }
        catch (Exception ex)
        {
            return $"<error: {ex.Message}>";
        }
    }

    private static T SafeRead<T>(Func<T> getter, T fallback)
    {
        try
        {
            return getter();
        }
        catch
        {
            return fallback;
        }
    }

    private static T[] SafeArrayRead<T>(Func<T[]> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static bool SafeHasAttr(Mesh mesh, VertexAttribute attribute)
    {
        try
        {
            return mesh.HasVertexAttribute(attribute);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildColorStats(Color32[] colors)
    {
        if (colors.Length == 0)
        {
            return "empty";
        }

        byte minR = byte.MaxValue;
        byte minG = byte.MaxValue;
        byte minB = byte.MaxValue;
        byte minA = byte.MaxValue;
        byte maxR = byte.MinValue;
        byte maxG = byte.MinValue;
        byte maxB = byte.MinValue;
        byte maxA = byte.MinValue;
        var zeroAlphaCount = 0;

        foreach (var color in colors)
        {
            if (color.r < minR) minR = color.r;
            if (color.g < minG) minG = color.g;
            if (color.b < minB) minB = color.b;
            if (color.a < minA) minA = color.a;
            if (color.r > maxR) maxR = color.r;
            if (color.g > maxG) maxG = color.g;
            if (color.b > maxB) maxB = color.b;
            if (color.a > maxA) maxA = color.a;
            if (color.a == 0) zeroAlphaCount++;
        }

        return $"R[{minR},{maxR}] G[{minG},{maxG}] B[{minB},{maxB}] A[{minA},{maxA}] zeroA={zeroAlphaCount}/{colors.Length}";
    }

    private static string BuildAlphaSummary(Color32[] colors)
    {
        if (colors.Length == 0)
        {
            return "empty";
        }

        byte minA = byte.MaxValue;
        byte maxA = byte.MinValue;
        var zeroAlphaCount = 0;

        foreach (var color in colors)
        {
            if (color.a < minA) minA = color.a;
            if (color.a > maxA) maxA = color.a;
            if (color.a == 0) zeroAlphaCount++;
        }

        return $"A[{minA},{maxA}] zeroA={zeroAlphaCount}/{colors.Length}";
    }

    private static string BuildColorSample(Color32[] colors, int maxCount)
    {
        if (colors.Length == 0)
        {
            return "empty";
        }

        var count = Mathf.Min(colors.Length, maxCount);
        var sb = new StringBuilder();

        for (var i = 0; i < count; i++)
        {
            var color = colors[i];
            if (i > 0)
            {
                sb.Append(" | ");
            }

            sb.Append($"#{i}:({color.r},{color.g},{color.b},{color.a})");
        }

        return sb.ToString();
    }
}
#endif