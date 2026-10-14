using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Iconos de traje propios guardados en Assets/Resources/CostumeIcons.
/// Clave: comunes = costumeId (p.ej. "0001_00_01"); propios = charaId_costumeId (p.ej. "1013_0055").
/// Si existe el PNG, se usa en vez del icono del traje (o del default). Los que no existan devuelven null.
/// </summary>
public static class CustomCostumeIcons
{
    private const string Folder = "CostumeIcons/";
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        Sprite cached;
        if (Cache.TryGetValue(key, out cached)) return cached;

        var texture = Resources.Load<Texture2D>(Folder + key);
        Sprite sprite = texture == null
            ? null
            : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
        Cache[key] = sprite;
        return sprite;
    }

    public static Sprite Get(int charaId, string costumeId, bool isCommon)
    {
        return Get(isCommon ? costumeId : charaId + "_" + costumeId);
    }
}
