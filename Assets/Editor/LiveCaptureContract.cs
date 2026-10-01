#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

// Reference manifests describe timeline frames; video extraction offsets must NOT
// be applied a second time when seeking the Live Director.
[Serializable]
public sealed class LiveCapturePlan
{
    public int songId = 1001;
    public string stageId = "10102";
    public string[] characters;
    public int fps = 60, width = 1920, height = 1080;
    public int[] frames;

    public static LiveCapturePlan Default()
    {
        return new LiveCapturePlan {
            characters = new[] { "1068_00", "1006_00", "1030_00" }
                .Concat(Enumerable.Repeat("1003_00", 15)).ToArray(),
            frames = new[] { 1710, 1732, 1740, 1755, 3000 }
        };
    }
    public void Validate()
    {
        if (songId <= 0 || string.IsNullOrEmpty(stageId) || fps != 60 ||
            width < 1 || height < 1 || width > 8192 || height > 8192 ||
            frames == null || frames.Length == 0 || frames.Any(x => x < 0) ||
            frames.Distinct().Count() != frames.Length || characters == null || characters.Length == 0)
            throw new InvalidOperationException("Invalid Live capture manifest (60fps timeline required).");
        foreach (string character in characters)
        {
            ParseCharacterIdentity(character, out _, out _);
        }
    }
    // The first separator belongs to the capture identity; remaining separators
    // are part of a generic costume ID (e.g. 0001_00), not extra characters.
    public static void ParseCharacterIdentity(string identity, out int id, out string costume)
    {
        int separator = (identity ?? "").IndexOf('_');
        id = 0;
        costume = separator >= 0 ? identity.Substring(separator + 1) : "";
        if (separator <= 0 || !int.TryParse(identity.Substring(0, separator), out id) || id <= 0 ||
            costume.Split('_').Any(part => part.Length == 0 || part.Any(c => c < '0' || c > '9')))
            throw new InvalidOperationException("Invalid reference character/costume identity.");
    }

    public void SelectFrames(int[] selected)
    {
        if (selected == null || selected.Length == 0 || selected.Any(x => !frames.Contains(x)))
            throw new InvalidOperationException("Requested capture frames must exist in the reference manifest.");
        var previous = frames;
        frames = selected;
        try { Validate(); } catch { frames = previous; throw; }
    }
    public bool MatchesIdentity(int song, string stage, string[] cast)
    {
        return song == songId && stage == stageId && cast != null && characters.SequenceEqual(cast);
    }
}

// Require consecutive renders at the intended timeline frame, not editor update
// ticks. Duplicate camera callbacks in one Unity frame cannot advance the gate.
public sealed class LiveCaptureStabilityGate
{
    readonly int required;
    int count, lastUnityFrame = -1;
    public LiveCaptureStabilityGate(int requiredFrames = 60) { required = requiredFrames; }
    public void Reset() { count = 0; lastUnityFrame = -1; }
    public bool Observe(float seconds, int timelineFrame, int fps, int unityFrame, bool ready)
    {
        if (unityFrame == lastUnityFrame) return false;
        lastUnityFrame = unityFrame;
        if (!ready || float.IsNaN(seconds) || float.IsInfinity(seconds) ||
            Math.Abs((double)seconds * fps - timelineFrame) > .1)
        { count = 0; return false; }
        return ++count >= required;
    }
}
#endif
