using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using UnityEngine;

public static class LiveAutoSelection
{
    private sealed class Candidate
    {
        public CharaEntry Character;
        public string CostumeId;
        public Sprite Icon;
        public int BodyTypeSub;
    }

    // Existing character, body costume and head costume choices are never replaced.
    public static bool FillMissing(UmaViewerMain main, LiveEntry live,
        IList<LiveCharacterSelect> slots, Sprite fallbackIcon = null)
    {
        if (main == null || live == null || slots == null) return false;
        bool needsDefaults = false;
        var used = new HashSet<int>();
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (slot.CharaEntry != null && slot.CharaEntry.Id > 0) used.Add(slot.CharaEntry.Id);
            if (slot.CharaEntry == null || slot.CharaEntry.Id <= 0 || string.IsNullOrEmpty(slot.CostumeId)) needsDefaults = true;
        }
        if (!needsDefaults) return true;

        var defaults = GetWinningCostumes(main, fallbackIcon);
        var singerIds = GetSingerIds(main, live.MusicId);
        var singers = new List<Candidate>();
        var others = new List<Candidate>();
        foreach (var character in main.Characters)
        {
            if (character == null || character.IsMob || !defaults.TryGetValue(character.Id, out var candidate)) continue;
            candidate.Character = character;
            (singerIds.Contains(character.Id) ? singers : others).Add(candidate);
        }
        Shuffle(singers);
        Shuffle(others);

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            bool missingCharacter = slot.CharaEntry == null || slot.CharaEntry.Id <= 0;
            bool missingCostume = string.IsNullOrEmpty(slot.CostumeId);
            if (!missingCharacter && !missingCostume) continue;

            Candidate selected;
            if (missingCharacter)
            {
                selected = FindCandidate(main, singers, used, slot.CostumeId, true)
                    ?? FindCandidate(main, others, used, slot.CostumeId, true)
                    ?? FindCandidate(main, singers, used, slot.CostumeId, false)
                    ?? FindCandidate(main, others, used, slot.CostumeId, false);
            }
            else
            {
                defaults.TryGetValue(slot.CharaEntry.Id, out selected);
                if (slot.CharaEntry.IsMob) selected = null;
            }
            if (selected == null) return false;

            if (missingCharacter) slot.CharaEntry = selected.Character;
            if (missingCostume) slot.CostumeId = selected.CostumeId;
            used.Add(slot.CharaEntry.Id);
            if (slot.CharaImage != null)
            {
                slot.CharaImage.enabled = true;
                slot.CharaImage.sprite = slot.CharaEntry.Icon;
            }
            if (missingCostume && slot.CostumeImage != null)
                slot.CostumeImage.sprite = selected.Icon;
        }
        return true;
    }

    private static Dictionary<int, Candidate> GetWinningCostumes(UmaViewerMain main, Sprite fallbackIcon)
    {
        var result = new Dictionary<int, Candidate>();
        var rows = UmaDatabaseController.Instance.DressData;
        if (rows == null) return result;
        foreach (DataRow row in rows)
        {
            if (Convert.ToInt32(row["costume_type"]) != 1) continue;
            int characterId = Convert.ToInt32(row["chara_id"]);
            int subtype = Convert.ToInt32(row["body_type_sub"]);
            string costumeId = subtype.ToString("D2", CultureInfo.InvariantCulture);
            string body = UmaDatabaseController.BodyPath + $"bdy{characterId}_{costumeId}/pfb_bdy{characterId}_{costumeId}";
            if (characterId <= 0 || !main.AbList.TryGetValue(body, out var entry) || entry == null) continue;
            if (result.TryGetValue(characterId, out var previous) && previous.BodyTypeSub <= subtype) continue;
            Sprite icon = fallbackIcon;
            foreach (var costume in main.Costumes)
            {
                if (costume.CharaId != characterId || costume.BodyTypeSub != subtype) continue;
                if (costume.Icon != null) icon = costume.Icon;
                break;
            }
            result[characterId] = new Candidate { CostumeId = costumeId, Icon = icon, BodyTypeSub = subtype };
        }
        return result;
    }

    private static HashSet<int> GetSingerIds(UmaViewerMain main, int musicId)
    {
        string song = musicId.ToString("D4", CultureInfo.InvariantCulture);
        string prefix = $"sound/l/{song}/snd_bgm_live_{song}_chara_";
        var result = new HashSet<int>();
        foreach (var sound in main.AbSounds)
        {
            string name = sound.Name;
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith("awb", StringComparison.OrdinalIgnoreCase)) continue;
            int separator = name.IndexOf('_', prefix.Length);
            if (separator < 0 || !name.Substring(separator).StartsWith("_01", StringComparison.Ordinal)) continue;
            if (int.TryParse(name.Substring(prefix.Length, separator - prefix.Length), out int characterId))
                result.Add(characterId);
        }
        return result;
    }

    private static Candidate FindCandidate(UmaViewerMain main, List<Candidate> candidates,
        HashSet<int> used, string costumeId, bool distinct)
    {
        if (candidates.Count == 0) return null;
        int start = distinct ? 0 : UnityEngine.Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[(start + i) % candidates.Count];
            if (distinct && used.Contains(candidate.Character.Id)) continue;
            if (string.IsNullOrEmpty(costumeId) || HasBody(main, candidate.Character, costumeId)) return candidate;
        }
        return null;
    }

    private static bool HasBody(UmaViewerMain main, CharaEntry character, string costumeId)
    {
        if (costumeId.Length < 4)
            return main.AbList.ContainsKey(UmaDatabaseController.BodyPath + $"bdy{character.Id}_{costumeId}/pfb_bdy{character.Id}_{costumeId}");
        int separator = costumeId.LastIndexOf('_');
        if (separator < 0) return false;
        var rows = UmaDatabaseController.Instance.CharaData;
        if (rows == null) return false;
        foreach (var row in rows)
        {
            if (Convert.ToInt32(row["id"]) != character.Id) continue;
            string shortId = costumeId.Substring(0, separator);
            string body = UmaDatabaseController.BodyPath + $"bdy{shortId}/pfb_bdy{costumeId}_{row["height"]}_{row["shape"]}_{row["bust"]}";
            return main.AbList.ContainsKey(body);
        }
        return false;
    }

    private static void Shuffle(List<Candidate> candidates)
    {
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int other = UnityEngine.Random.Range(0, i + 1);
            var candidate = candidates[i];
            candidates[i] = candidates[other];
            candidates[other] = candidate;
        }
    }
}
