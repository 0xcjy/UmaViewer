"""Run the project's original PMX exporter in an isolated Unity 2022.3 batch Editor.

Usage: python tools/export_exclusive_models.py [--batch 24] [--once] [--retry-failures]
Checkpoint/report is exports/exclusive-model-report.json; re-launch resumes and validates each PMX.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

PROJECT = Path(__file__).resolve().parents[1]
SITE = PROJECT.parent / 'UmaAudioSite'
OUTPUT = SITE / 'data' / 'model-viewer'
REPORT = PROJECT / 'exports' / 'exclusive-model-report.json'
ISOLATED = PROJECT / 'tmp' / 'exclusive-model-batch'
UNITY = Path('C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe')


def prepare():
    if not UNITY.is_file():
        raise FileNotFoundError(UNITY)
    if not (ISOLATED / 'Assets' / 'Scenes' / 'Version2.unity').is_file():
        ISOLATED.mkdir(parents=True, exist_ok=True)
        for name in ('Assets', 'Packages', 'ProjectSettings'):
            shutil.copytree(PROJECT / name, ISOLATED / name, dirs_exist_ok=True)
        shutil.copy2(PROJECT / 'Config.json', ISOLATED / 'Config.json')
    shutil.copy2(PROJECT / 'tools' / 'ExclusiveModelBatch.cs', ISOLATED / 'Assets' / 'Editor' / 'ExclusiveModelBatch.cs')
    shutil.copy2(PROJECT / 'Assets' / 'Scripts' / 'Exporters' / 'ModelExporter.cs',
                 ISOLATED / 'Assets' / 'Scripts' / 'Exporters' / 'ModelExporter.cs')
    OUTPUT.mkdir(parents=True, exist_ok=True)


def status():
    report = REPORT
    if not report.is_file():
        return 0, 0, 0, []
    outfits = json.loads(report.read_text(encoding='utf-8'))['outfits']
    valid = [o for o in outfits if o['bytes'] > 0 and not o.get('error')]
    return len(outfits), len(valid), sum(o['bytes'] for o in valid), outfits


def catalog(outfits):
    grouped = {}
    for o in outfits:
        directory = OUTPUT / 'models' / str(o['id']) / o['costume']
        if not o.get('bytes') or o.get('error'):
            if directory.exists():
                shutil.rmtree(directory)
            continue
        pmx = directory / 'model.pmx'
        if not pmx.is_file() or pmx.stat().st_size != o['bytes']:
            raise ValueError(f'Catalog candidate changed since Unity readback: {pmx}')
        chara = grouped.setdefault(o['id'], {'id': o['id'], 'name': o['name'], 'costumes': []})
        chara['costumes'].append({'id': o['costume'], 'model': f"/data/model-viewer/models/{o['id']}/{o['costume']}/model.pmx"})
    path = OUTPUT / 'catalog.json'
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps({'characters': list(grouped.values())}, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(path)
    return len(grouped)


def normalized_environment(batch, retry_ids, only=None):
    # Unity/UPM on Windows chokes on duplicate case-insensitive keys from mixed runtimes.
    env = {}
    for key, value in os.environ.items():
        env[key.upper()] = value
    env['ALLUSERSPROFILE'] = os.environ.get('ALLUSERSPROFILE') or r'C:\ProgramData'
    env['UMA_EXCLUSIVE_OUTPUT'] = str(OUTPUT)
    env['UMA_EXCLUSIVE_BATCH'] = str(batch)
    env['UMA_EXCLUSIVE_REPORT'] = str(REPORT)
    env['UMA_EXCLUSIVE_RETRY_IDS'] = ','.join(retry_ids)
    env['UMA_EXCLUSIVE_ONLY'] = only or ''
    return env


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--batch', type=int, default=24)
    parser.add_argument('--once', action='store_true')
    parser.add_argument('--retry-failures', action='store_true', help='Reattempt reported failures after triage')
    parser.add_argument('--only', help='Comma-separated character_costume IDs for targeted triage')
    args = parser.parse_args()
    prepare()
    before = status()
    pending = sum(not o.get('bytes') and not o.get('error') for o in before[3])
    if args.retry_failures and pending and not args.only:
        raise RuntimeError(f'{pending} outfits have not been attempted; complete ordinary export before retrying failures')
    retry_ids = args.only.split(',') if args.only else [f"{o['id']}_{o['costume']}" for o in before[3] if o.get('error')]
    before_remaining = pending
    cycle = 0
    while True:
        cycle += 1
        log = ISOLATED / f'exclusive-batch-{time.strftime("%Y%m%d-%H%M%S")}-{cycle:03d}.log'
        print(f'Launching isolated Unity pass {cycle}, valid {before[1]}/{before[0]}, log={log}', flush=True)
        with log.open('wb') as stream:
            result = subprocess.run([str(UNITY), '-batchmode', '-projectPath', str(ISOLATED),
                                     '-executeMethod', 'ExclusiveModelBatch.Run', '-logFile', str(log)],
                                    env=normalized_environment(args.batch, retry_ids[:args.batch] if args.retry_failures or args.only else [], args.only), timeout=7200)
        after = status()
        chara_count = catalog(after[3]) if after[0] else 0
        print(f'Unity exit={result.returncode}; available={after[0]} verified={after[1]} '
              f'pmxBytes={after[2]} characters={chara_count}; log={log}', flush=True)
        if result.returncode or not after[0]:
            raise RuntimeError(f'Unity batch did not finish discovery; inspect {log}')
        if args.retry_failures or args.only:
            retry_ids = retry_ids[args.batch:]
            if args.once or not retry_ids:
                break
        else:
            remaining = sum(not o.get('bytes') and not o.get('error') for o in after[3])
            if args.once or after[1] == after[0] or not remaining:
                break
            if remaining >= before_remaining:
                raise RuntimeError(f'No checkpoint progress in isolated Unity batch; inspect {log}')
            before_remaining = remaining
        before = after
    failures = [o for o in after[3] if o.get('error')]
    print(f'FINAL {after[1]}/{after[0]} verified, {len(failures)} failures; '
          f'report={REPORT}', flush=True)
    for o in failures:
        print(f'FAIL {o["id"]}_{o["costume"]}: {o["error"].splitlines()[0]}', flush=True)
    return 0 if not failures else 2


if __name__ == '__main__':
    sys.exit(main())
