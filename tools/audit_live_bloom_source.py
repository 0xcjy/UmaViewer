"""Compare exported game keys with Live logs; only non-interpolated intervals.
Input: live1176-effect-source.json exported from the local game asset typetree.
This checks parameter provenance, not target-viewer evaluation or pixel parity.
"""
import argparse
import json
from pathlib import Path

FIELDS = {"bloomThreshold": "threshold", "bloomIntensity": "intensity",
          "bloomBlurSize": "BloomBlurSize", "bloomDofWeight": "bloomDofWeight",
          "diffusionBright": "diffusionBright", "diffusionThreshold": "diffusionThreshold",
          "diffusionBlurSize": "diffusionBlurSize", "diffusionSaturation": "diffusionSaturation",
          "diffusionContrast": "diffusionContrast"}

def compare(source, records, song):
    keys = source[0]["tracks"]["postEffectBloomDiffusionKeys"]["thisList"]
    results = []
    for r in records:
        if r.get("type") != "live_frame" or r.get("songId") != song:
            continue
        # Old logs serialize absent diagnostic floats as zero: do not claim coverage.
        if not r.get("bloomIntensity") or any(f not in r for f in FIELDS):
            continue
        frame = r.get("seconds", -1) * 60
        before = [k for k in keys if k["frame"] <= frame]
        after = [k for k in keys if k["frame"] > frame]
        # Interpolation is selected by the NEXT key. Curves require Unity evaluation.
        if frame < 0 or not before or (after and after[0]["interpolateType"] != 0):
            continue
        key = before[-1]
        mismatches = {f: {"actual": r[f], "source": key[k]} for f, k in FIELDS.items()
                      if abs(r[f] - key[k]) > 0.0001}
        results.append({"frame": r.get("liveFrame"), "sourceKey": key["frame"],
                        "mismatches": mismatches})
    return results

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("logs", type=Path, nargs="+")
    parser.add_argument("--song", type=int, default=1176)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8-sig"))
    result = []
    for path in args.logs:
        records = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
        result.extend(dict(log=path.name, **r) for r in compare(source, records, args.song))
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Compared {len(result)} held-key snapshots; mismatching: {sum(bool(r['mismatches']) for r in result)}")
