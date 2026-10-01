"""Summarize one opt-in Live JSONL session without inventing resource counts.

Usage: python tools/summarize_live_diagnostics.py tmp/live-diagnostics-*.jsonl
"""
import argparse
import collections
import json
from pathlib import Path


def summarize_execution(events):
    """Check submitted pass chains per camera/Unity frame, retaining seek revisits."""
    groups = collections.defaultdict(list)
    scheduled = collections.defaultdict(list)
    for event in events:
        key = (event.get("unityFrame"), event.get("camera"))
        if event.get("type") == "render_execute":
            groups[key].append(event)
        elif event.get("type") == "render_pass":
            scheduled[key].append(event)
    chains = []
    for (unity_frame, camera), steps in groups.items():
        issues = []
        names = [e.get("renderStep") for e in steps]
        repeated = [name for name, count in collections.Counter(names).items() if count > 1]
        if repeated:
            issues.append("Repeated recorded steps: " + ", ".join(repeated))
        if names[0] != "SourceSetup":
            issues.append("Missing leading SourceSetup")
        for previous, current in zip(steps, steps[1:]):
            if not previous.get("outputRT") or previous.get("outputRT") != current.get("inputRT"):
                issues.append("RT handoff differs: %s -> %s" %
                              (previous.get("renderStep"), current.get("renderStep")))
        if len({e.get("liveFrame") for e in steps}) != 1:
            issues.append("Live frame changed within recorded chain")
        enqueue = scheduled.get((unity_frame, camera), [])
        expected = enqueue[0].get("rendererPassCount") if len(enqueue) == 1 else None
        if expected is not None and expected != len(steps):
            issues.append("Submitted record count differs from scheduled count")
        camera_restored = (bool(steps[0].get("outputRT")) and
                           steps[-1].get("outputRT") == steps[0].get("outputRT"))
        if not camera_restored:
            issues.append("Last output differs from initial camera RT")
        chains.append({
            "unityFrame": unity_frame, "camera": camera,
            "liveFrame": steps[0].get("liveFrame"), "seconds": steps[0].get("seconds"),
            "steps": names, "scheduledPasses": expected,
            "urpPostProcessing": enqueue[0].get("urpPostProcessing") if len(enqueue) == 1 else None,
            "cameraOutputRestored": camera_restored,
            "handoffs": [{k: e.get(k) for k in ("renderStep", "renderPassEvent", "inputRT", "outputRT")}
                         for e in steps],
            "issues": issues,
        })
    return {
        "recordCount": sum(len(items) for items in groups.values()),
        "sampledCameraFrames": len(chains),
        "chains": chains,
        "scope": "Managed command-buffer submissions only; internal PostFilm/Shader draws and GPU pixels "
                 "are not traced. Adjacent Live frames are not exact reference captures. "
                 "No findings outside sampled frames are implied."
    }


def summarize(path):
    events = []
    with path.open(encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                events.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise ValueError(f"{path}:{line_number}: {exc}") from exc
    if not events or events[0].get("type") != "session_begin":
        raise ValueError("Missing session_begin: use a complete Live diagnostics session")

    begin = events[0]
    counts = next((e for e in events if e.get("type") == "preload_counts"), {})
    root_counts = next((e for e in events if e.get("type") == "resource_roots"), {})
    root_expansions = [e for e in events if e.get("type") == "preload_root"]
    complete_index = next((i for i, e in enumerate(events)
                           if e.get("type") == "preload_complete"), None)
    before = events[:complete_index] if complete_index is not None else events
    after = events[complete_index + 1:] if complete_index is not None else []
    preload_acquires = [e for e in before if e.get("type") == "bundle_acquire"]
    runtime_acquires = [e for e in after if e.get("type") == "bundle_acquire"]
    preload_names = [e.get("bundle", "") for e in preload_acquires]
    runtime_requests = [e for e in after if e.get("type") == "bundle_request"]
    instances = [e for e in events if e.get("type") == "instance_create"]
    asset_loads = [e for e in events if e.get("type") == "asset_load"]
    progress = [e for e in events if e.get("type") == "load_progress"]
    frames = [e for e in events if e.get("type") == "live_frame"]
    passes = [e for e in events if e.get("type") == "render_pass"]
    targets = [e for e in frames if e.get("liveFrame") in (308, 868, 3848)]
    bundle_inventories = [e for e in events if e.get("type") == "bundle_inventory"]
    scene_inventories = [e for e in events if e.get("type") == "scene_inventory"]
    indexed_bundles = [e for e in events if e.get("type") == "bundle_index"]
    inventory_keys = ("phase", "uniqueLoadedHandles", "loadedAssetBundles",
                      "loadedNonBundles", "assetIndexCandidates", "assetIndexErrors")
    scene_keys = ("phase", "loadedScenes", "sceneRoots", "totalTransforms",
                  "renderers", "lights", "particleSystems", "cameras", "videos", "outcome")
    def count_outcomes(items):
        return dict(sorted(collections.Counter(e.get("outcome") for e in items).items()))
    def frame_snapshot(e):
        keys = ("liveFrame", "seconds", "unityFrame", "camera", "width", "height",
                "graphicsFormat", "characters", "stageObjects", "stageMapObjects",
                "totalTransforms", "renderers", "lights", "particleSystems",
                "dof", "bloom", "diffusion", "radial", "colorCorrection", "mode")
        return {k: e[k] for k in keys if k in e}
    return {
        "session": begin.get("session"), "songId": begin.get("songId"),
        "stageId": begin.get("stageId"), "characterSlots": begin.get("characters"),
        "completePreload": complete_index is not None,
        "preload": {
            "roots": counts.get("roots"),
            "rootSources": {
                "director": root_counts.get("directorRoots"),
                "normalCharacter": root_counts.get("normalRoots"),
                "mobCharacter": root_counts.get("mobRoots"),
                "normalCharacters": root_counts.get("normalCharacters"),
                "mobCharacters": root_counts.get("mobCharacters"),
                "skippedCharacters": root_counts.get("skippedCharacters"),
                "uniqueBeforeDependencyExpansion": root_counts.get("roots"),
            },
            "uniqueExpandedDownloadEntries": counts.get("downloadEntriesUnique"),
            "requestedItemsWithDuplicates": counts.get("loadItemsWithDuplicates"),
            "perRootCoverage": {
                "recordedRoots": len(root_expansions),
                "requestsSummed": sum(e.get("requestCount", 0) for e in root_expansions),
                "repeatedRequestsSummed": sum(e.get("repeatedRequests", 0) for e in root_expansions),
                "agreesWithProgressTarget": (
                    sum(e.get("requestCount", 0) for e in root_expansions)
                    == counts["loadItemsWithDuplicates"]
                    if root_expansions and "loadItemsWithDuplicates" in counts else None),
                "largestRoots": [
                    {k: e.get(k) for k in ("rootIndex", "bundle", "requestCount", "repeatedRequests")}
                    for e in sorted(root_expansions,
                                    key=lambda e: (-e.get("requestCount", 0), e.get("rootIndex", -1)))[:20]
                ],
            },
            "observedAcquireAttempts": len(preload_acquires),
            "observedUniqueBundleNames": len(set(preload_names)),
            "observedRepeatedBundleRequests": len(preload_names) - len(set(preload_names)),
            "acquireOutcomes": count_outcomes(preload_acquires),
            "progressSamples": len(progress),
            "progressFinal": next((e.get("progressTarget") for e in reversed(progress)
                                   if e.get("progressTarget", -1) >= 0), None)
        },
        "runtime": {
            "bundleRequestCalls": len(runtime_requests),
            "uniqueRequestedBundleNames": len({e.get("bundle") for e in runtime_requests}),
            "acquireAttempts": len(runtime_acquires),
            "acquireOutcomes": count_outcomes(runtime_acquires),
            "failedBundleNames": sorted({e.get("bundle") for e in runtime_acquires
                                         if e.get("outcome") in ("file_missing", "load_failed", "exception")}),
            "assetLoadCallsObserved": len(asset_loads),
            "materializedAssetsInObservedCalls": sum(max(e.get("assetsLoaded", 0), 0) for e in asset_loads),
            "matchingAssetsInObservedCalls": sum(max(e.get("assetsMatching", 0), 0) for e in asset_loads),
            "assetLoadOperations": dict(sorted(collections.Counter(e.get("operation") for e in asset_loads).items())),
            "instantiationsObserved": len(instances),
            "instantiationSources": dict(sorted(collections.Counter(e.get("bundle") for e in instances).items())),
        },
        "phaseInventories": {
            "bundles": [{k: e[k] for k in inventory_keys if k in e}
                        for e in bundle_inventories],
            "sceneObjects": [{k: e[k] for k in scene_keys if k in e}
                             for e in scene_inventories],
            "perPhaseIndexedBundles": dict(sorted(collections.Counter(
                e.get("phase") for e in indexed_bundles).items())),
            "indexFailures": [{"phase": e.get("phase"), "bundle": e.get("bundle")}
                              for e in indexed_bundles if e.get("outcome") in ("index_error", "bundle_unavailable")],
        },
        "playback": {
            "frameSamples": len(frames), "renderPassSamples": len(passes),
            "cameraPassCounts": dict(sorted(collections.Counter(e.get("camera") for e in passes).items())),
            "referenceFrameSnapshots": [frame_snapshot(e) for e in targets],
            "execution": summarize_execution(events),
        },
        "coverage": "Asset calls cover UmaDatabaseEntry.Get/GetAll and selected stage, part, "
                    "motion, flare, flash, monitor and cyalume paths. Other direct AssetBundle "
                    "calls may still be uncounted. bundle.name may not be an official index name. "
                    "Materialized assets can repeat across calls. Bundle catalog candidates are "
                    "GetAllAssetNames entries, not actual loaded assets. Scene inventories count "
                    "loaded SceneManager scenes, not DontDestroyOnLoad or Editor objects. "
                    "No compiled-viewer count is implied."
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("session", type=Path, help="one complete Live JSONL file")
    args = parser.parse_args()
    print(json.dumps(summarize(args.session), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
