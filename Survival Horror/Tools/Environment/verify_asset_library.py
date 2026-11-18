"""Overlay the browser and current catalogue prefabs onto the existing isolated collision snapshot."""
from pathlib import Path
import hashlib
import json
import shutil
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
STAGE = ROOT / 'Exports/Reviews/PrefabCollision/UnityProject'
REVIEW = ROOT / 'Exports/Reviews/AssetLibrary'
LIBRARY = Path('Assets/Game/Environment/Editor/AssetLibrary')


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def stage():
    if not (STAGE/'ProjectSettings/ProjectVersion.txt').exists():
        raise SystemExit('First create the isolated snapshot: python Tools/Environment/prefab_collision.py stage')
    REVIEW.mkdir(exist_ok=True, parents=True)
    catalog = json.loads((ROOT/LIBRARY/'Catalog.json').read_text(encoding='utf-8'))
    (STAGE/'Library/AssetLibraryReview/Verification.json').unlink(missing_ok=True)
    # Seed stable GUIDs for new editor files if Unity has not imported them yet.
    for path in [ROOT/LIBRARY, *(ROOT/LIBRARY).iterdir()]:
        if path.suffix == '.meta':
            continue
        meta = Path(str(path)+'.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if path.is_dir() else ''))
    existing = list((ROOT/'Assets/Game').rglob('*.prefab'))
    baseline = {p.relative_to(ROOT).as_posix():digest(p) for path in existing for p in [path,Path(str(path)+'.meta')]}
    (REVIEW/'RuntimeBaseline.json').write_text(json.dumps(baseline))
    for i, entry in enumerate(catalog['entries']):
        for suffix in ['', '.meta']:
            source = ROOT/(entry['path']+suffix)
            dest = STAGE/(entry['path']+suffix)
            if not dest.exists():
                raise SystemExit('Snapshot missing target/dependencies; recreate it: '+str(dest))
            if digest(source) != digest(dest):
                shutil.copy2(source, dest)
        if i % 100 == 0:
            print('Synchronized',i,'catalogue targets',flush=True)
    for path in [Path(str(ROOT/LIBRARY)+'.meta'), *(ROOT/LIBRARY).rglob('*')]:
        if path.is_file():
            dest = STAGE/path.relative_to(ROOT)
            dest.parent.mkdir(exist_ok=True, parents=True)
            shutil.copy2(path,dest)
    shutil.copy2(ROOT/'Tools/Environment/AssetLibraryVerification.cs',STAGE/'Assets/Editor/AssetLibraryVerification.cs')
    print('Staged',len(catalog['entries']),'entries. Run AssetLibraryVerification.RunBatch in the snapshot.',flush=True)


def verify():
    baseline = json.loads((REVIEW/'RuntimeBaseline.json').read_text())
    changed = [p for p,sha in baseline.items() if not (ROOT/p).exists() or digest(ROOT/p) != sha]
    if changed:
        raise SystemExit('Runtime prefab/metadata changed during work: '+str(changed))
    receipt = json.loads((STAGE/'Library/AssetLibraryReview/Verification.json').read_text())
    assert receipt['verified']
    assert (STAGE/LIBRARY/'Catalog.json').read_bytes() == (ROOT/LIBRARY/'Catalog.json').read_bytes()
    for path in (ROOT/LIBRARY).glob('*.cs'):
        assert path.read_bytes() == (STAGE/LIBRARY/path.name).read_bytes(), 'Untested source edit: '+str(path)
    receipt['runtime_prefab_and_meta_changes'] = 0
    (REVIEW/'Verification.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps(receipt,indent=2))


if __name__ == '__main__':
    {'stage':stage,'verify':verify}[sys.argv[1]]()
