using System;
using System.Collections.Generic;
/// <summary>
/// //把角色选择界面中的角色数据，复制成一份专门用于 Live 加载的轻量数据
/// </summary>
/// 
[Serializable]
public sealed class LiveCharacterLoadData
{
    public CharaEntry CharaEntry;
    public string CostumeId;
    public string HeadCostumeId;

    public static LiveCharacterLoadData Capture(LiveCharacterSelect source)
    {
        if (source == null)
            return null;

        CharaEntry sourceChara = source.CharaEntry;
        CharaEntry copiedChara = null;

        if (sourceChara != null)
        {
            copiedChara = new CharaEntry
            {
                Name = sourceChara.Name,
                EnName = sourceChara.EnName,
                Id = sourceChara.Id,
                ThemeColor = sourceChara.ThemeColor,
                IsMob = sourceChara.IsMob,
                Icon = null
            };
        }

        return new LiveCharacterLoadData
        {
            CharaEntry = copiedChara,
            CostumeId = source.CostumeId,
            HeadCostumeId = source.HeadCostumeId
        };
    }

    public static List<LiveCharacterLoadData> CaptureAll(IList<LiveCharacterSelect> sources)
    {
        var result = new List<LiveCharacterLoadData>();
        if (sources == null)
            return result;

        for (int i = 0; i < sources.Count; i++)
        {
            LiveCharacterLoadData item = Capture(sources[i]);
            if (item != null)
                result.Add(item);
        }

        return result;
    }
}
