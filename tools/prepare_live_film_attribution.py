#!/usr/bin/env python3
"""Build a reproducible, SHA-pinned PostFilm-only GPU input from local real captures.

The tool does not read the application configuration or infer scene depth.
Power checks come from the captured runtime metadata, not screenshot pixels.
"""
import argparse
import hashlib
import json
from pathlib import Path

TRACKS = ("postFilmKeys", "postFilm2Keys", "postFilm3Keys")


def prepare(source: Path, capture: Path, frames: tuple[int, ...]) -> dict:
    worksheets = json.loads(source.read_text(encoding="utf-8"))
    camera = next(x for x in worksheets if x.get("worksheet") == "son1176_Camera")
    tracks = []
    for name in TRACKS:
        track = camera["tracks"][name]
        rows = []
        for entry in track["thisList"]:
            key = {k: v for k, v in entry.items() if k != "curve"}
            curve = entry["curve"]
            rows.append({"key": key, "curveKeys": curve["m_Curve"],
                         "preWrap": curve["m_PreInfinity"], "postWrap": curve["m_PostInfinity"]})
        tracks.append({"name": name, "attribute": track["_attribute"],
                       "playMode": track["_playMode"], "rows": rows})
    results = []
    for frame in frames:
        name = f"frame{frame:06d}"
        image = capture / (name + "_off.png")
        meta = capture / (name + "_on.txt")
        assert image.is_file() and meta.is_file(), name
        fields = dict(line.split("=", 1) for line in meta.read_text(encoding="utf-8-sig").splitlines() if "=" in line)
        assert int(fields["requestedFrame"]) == frame, f"metadata mismatch {name}"
        powers = [float(fields[f"film{i}"].split(":", 1)[1]) for i in (1, 2, 3)]
        result = {"frame": frame, "imagePath": str(image.resolve()),
                  "sha256": hashlib.sha256(image.read_bytes()).hexdigest(),
                  "expectedPowers": powers}
        if "filmBlinkLookup" in fields and "filmColor0" in fields:
            status = fields["filmBlinkLookup"].split("|")
            colors = [[float(v) for v in c.split(",")] for c in fields["filmColor0"].split("|")]
            assert len(status) == len(colors) == 3 and all(len(c) == 4 for c in colors)
            # Recover raw RGB only from an actively resolved Color0 sync whose authored
            # key has identity brightness/adjustment. Never infer it from screenshot pixels.
            candidates = []
            for i, name in enumerate(TRACKS):
                key = max((k for k in camera["tracks"][name]["thisList"] if k["frame"] <= frame),
                          key=lambda k: k["frame"])
                if (status[i] == "resolved" and key["attribute"] & 0x200000 and
                        key["BlinkLightBrightnessPower"] == 1 and not key["IsAdjustedBlinkLightColor"]):
                    candidates.append(colors[i][:3])
            if candidates:
                raw = candidates[0]
                assert all(max(abs(a-b) for a,b in zip(raw, c)) < 0.0002 for c in candidates), "inconsistent same-container raw Blink RGB"
                result["rawBlinkRgb"] = {"r": raw[0], "g": raw[1], "b": raw[2], "a": 1.0}
                result["expectedColor0"] = [{"r": c[0], "g": c[1], "b": c[2], "a": c[3]} for c in colors]
                result["blinkLookup"] = "|".join(status)
        results.append(result)
    return {"tracks": tracks, "frames": results}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path("tmp/live1176-effect-source.json"))
    parser.add_argument("--capture", type=Path, default=Path("captures/render-smoke/20260926_092541_878"))
    parser.add_argument("--frames", default="308,868,3848")
    parser.add_argument("--output", type=Path, default=Path("tmp/live1176-film-attribution-input.json"))
    args = parser.parse_args()
    data = prepare(args.source, args.capture, tuple(map(int, args.frames.split(","))))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {args.output} ({len(data['tracks'])} tracks, {len(data['frames'])} SHA-pinned frames)")


if __name__ == "__main__":
    main()
