using System.Collections.Generic;
using UnityEngine;
using Gallop;
using System;

public class UmaCharaData
{
    public int id;
    public string tail_model_id;
}

public class UmaHeadData
{
    public int id;
    public string costumeId;
    public int tailId;
    public CharaEntry chara;
}

public class UmaLyricsData
{
    public float time;
    public string text;
}

[System.Serializable]
public class EmotionKey
{
    public FacialMorph morph;
    public float weight;
}

[System.Serializable]
public class FaceTypeData
{
    public string label, eyebrow_l, eyebrow_r, eye_l, eye_r, mouth, inverce_face_type;
    public int mouth_shape_type, set_face_group;
    public FaceEmotionKeyTarget target;
    public List<EmotionKey> emotionKeys;
    public float weight;
}

[System.Serializable]
public class CharaEntry
{
    /// master.mdb 中读取到的原始日文名。
    public string Name;

    /// <summary>
    /// 从在线更新、持久缓存或 LocalizeEn.cs 内置表读取的英文名。
    /// </summary>
    public string EnName;

    public Sprite Icon;
    public int Id;
    public string ThemeColor;
    public bool IsMob;

    public string GetName()
    {
        switch (Config.Instance.Language)
        {
            case Language.En:
                if (!string.IsNullOrEmpty(EnName))
                    return EnName;

                return IsMob
                    ? LocalizeEn.GetMobName(Id, Name)
                    : LocalizeEn.GetCharaName(Id, Name);

            case Language.Cn:
                Localize.CurrentRegion = Localize.Region.CN;

                // LocalizeEn carries the full localised table (1187 constants plus the runtime
                // names loaded from Resources/Localize/zh-CN-names.json). Localize.CN only ever
                // covered 1001..2005 and 9001/9002, so everything from 9003 on fell back to the
                // Japanese name from the database.
                return IsMob
                    ? LocalizeEn.GetMobName(Id, Name)
                    : LocalizeEn.GetCharaName(Id, Name);

            case Language.Jp:
            default:
                Localize.CurrentRegion = Localize.Region.JP;
                return Name ?? string.Empty;
        }
    }
}

[System.Serializable]
public class LiveEntry
{
    public int MusicId;
    public string SongName;
    public int MemberCount;
    public int DefaultDress;
    public string BackGroundId;
    public Sprite Icon;
    public List<string[]> LiveSettings = new List<string[]>();

    public LiveEntry(string data)
    {
        string[] lines = data.Split("\n"[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            LiveSettings.Add(lines[i].Split(','));
        }
        BackGroundId = LiveSettings[1][2];
    }

    private Dictionary<int, string> _projectorTextureNames;
    // Si la canción declara una sola textura de proyector, se usa para cualquier id.
    private string _singleProjectorName;

    // `livesettings` row type 11 is the per-song projector texture table: texture id -> texture
    // asset name. A LightProjection key's TextureId is an id into this table, so the asset name
    // cannot be derived from the number (song 1001: id 1 -> tex_env_live_cmn_projector000;
    // song 1005: id 1/2/3 -> tex_env_live_cmn_projector004/001/002).
    //
    // Row layout: [index, 11, texture asset name, id, content type, ...]. Several songs leave the
    // id column empty (1091, 1156, 1180, 1181). Those songs declare exactly ONE projector texture,
    // so that row is the song's projector regardless of the key's TextureId: the table says what it
    // projects, and the numeric fallback (tex_env_live_cmn_projector0NN) is only a file-name guess.
    public bool TryGetProjectorTextureName(int textureId, out string name)
    {
        if (_projectorTextureNames == null)
        {
            _projectorTextureNames = new Dictionary<int, string>();
            string onlyName = null;
            int rows = 0;
            if (LiveSettings != null)
            {
                foreach (var row in LiveSettings)
                {
                    if (row == null || row.Length <= 2 || row[1].Trim() != "11") continue;
                    string textureName = row[2].Trim();
                    if (string.IsNullOrEmpty(textureName)) continue;
                    rows++;
                    onlyName = textureName;
                    int id = 0;
                    bool explicitId = row.Length > 3 && int.TryParse(row[3].Trim(),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out id);
                    // An id written in the table always wins over an empty cell mapping to 0.
                    if (!explicitId && _projectorTextureNames.ContainsKey(0)) continue;
                    _projectorTextureNames[id] = textureName;
                }
            }
            _singleProjectorName = rows == 1 ? onlyName : null;
        }
        if (_projectorTextureNames.TryGetValue(textureId, out name)) return true;
        // A song with a single declared projector projects that texture for every key.
        name = _singleProjectorName;
        return !string.IsNullOrEmpty(name);
    }
}

public class PartEntry
{
    public Dictionary<string, List<float>> PartSettings = new Dictionary<string, List<float>>();

    //(Live: nº de columnas de PISTA reales de la tabla (center/left/right/left2/right2/left3/
    //right3). Es el límite físico de pistas que admite una canción y el recorte correcto de
    //maxVoices en Director, a diferencia de SingerCount ("columnas con algún valor > 0").)
    public int TrackCount = 0;

    public int SingerCount = 0;
    public PartEntry(string data)
    {
        var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var names = lines[0].Split(',');
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int j = 0; j < names.Length; j++)
        {
            string name = names[j].Trim();
            int suffix = name.IndexOf('_');
            string part = suffix < 0 ? name : name.Substring(0, suffix);
            switch (part)
            {
                case "lleft": part = "left2"; break;
                case "rright": part = "right2"; break;
                case "llleft": part = "left3"; break;
                case "rrright": part = "right3"; break;
            }
            name = part + (suffix < 0 ? "" : name.Substring(suffix));
            columns[name] = j;
            PartSettings[name] = new List<float>();
        }
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            foreach (var column in columns)
                PartSettings[column.Key].Add(float.Parse(values[column.Value], System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var part in PartSettings)
        {
            if (part.Key == "time" || part.Key.Contains("_")) continue;
            TrackCount++;
            if (part.Value.Exists(v => v > 0f))
                SingerCount++;
        }
    }
}


[System.Serializable]
public class CostumeEntry
{
    public string Id;
    public int CharaId;
    public string DressName;
    public int BodyType;
    public int BodyTypeSub;
    public Sprite Icon;
}

//(Live: modo de voz del directo — Solo concert / Mix voices)
public enum LiveVocalMode
{
    SoloConcert = 0,
    MixVoices = 1
}

public enum UIMessageType
{
    Default,
    Warning,
    Error,
    Success,
    Close
}
public enum UmaFileType
{
    _3d_cutt,
    announce,
    atlas,
    bg,
    chara,
    font,
    fontresources,
    gacha,
    gachaselect,
    guide,
    heroes,
    home,
    imageeffect,
    item,
    lipsync,
    live,
    loginbonus,
    manifest,
    manifest2,
    manifest3,
    mapevent,
    master,
    minigame,
    mob,
    movie,
    outgame,
    paddock,
    race,
    shader,
    single,
    sound,
    story,
    storyevent,
    supportcard,
    uianimation,
    transferevent,
    teambuilding,
    challengematch,
    collectevent,
    ratingrace,
    jobs,
    manualdownloadatlas,
}
