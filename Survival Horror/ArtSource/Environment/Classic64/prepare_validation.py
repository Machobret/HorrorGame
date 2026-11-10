"""Create a small isolated Unity project using the game's actual rendering assets.

Copies pilot content and serialized render dependencies, preserving existing GUIDs.
No hard links are used. The live project's open scene is never changed.
"""
from pathlib import Path
import json
import re
import shutil

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
STAGE = PROJECT / 'Exports/Reviews/Classic64/UnityValidation'


def copy(relative):
    source = PROJECT / relative
    target = STAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    meta = Path(str(source) + '.meta')
    if meta.is_file():
        shutil.copy2(meta, Path(str(target) + '.meta'))


def main():
    STAGE.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((HERE / 'PilotManifest.json').read_text(encoding='utf-8'))
    for entry in manifest['assets']:
        copy(entry['model_path'])
    for mat in manifest['materials']:
        copy(mat['texture']['path'])
    library_receipt = HERE / 'LibraryImportReceipt.json'
    if library_receipt.exists():
        for file in json.loads(library_receipt.read_text(encoding='utf-8'))['files']:
            if (PROJECT / file['path']).is_file():
                copy(file['path'])
        for relative in ('Assets/Game/Environment/Editor/Classic64FullLibraryBuilder.cs',
                         'ArtSource/Environment/Classic64/PilotImportReceipt.json',
                         'ArtSource/Environment/Classic64/LibraryImportReceipt.json',
                         'ArtSource/Environment/Classic64/LibraryManifest.json'):
            copy(relative)
    copy('Assets/Game/Environment/Editor/Classic64LibraryBuilder.cs')
    copy('ArtSource/Environment/Classic64/PilotManifest.json')
    for name in ('ProjectVersion.txt', 'GraphicsSettings.asset', 'QualitySettings.asset', 'ProjectSettings.asset'):
        copy('ProjectSettings/' + name)
    dependencies = json.loads((PROJECT / 'Packages/manifest.json').read_text())['dependencies']
    dependencies = {k: v for k, v in dependencies.items() if k.startswith('com.unity.modules.') or k in (
        'com.unity.render-pipelines.universal', 'com.unity.inputsystem')}
    (STAGE / 'Packages').mkdir(exist_ok=True)
    # Reuse installed versions as copied embedded packages. This avoids a fresh
    # registry resolve/download and keeps the validation environment reproducible.
    cache = {}
    for folder in (PROJECT / 'Library/PackageCache').iterdir():
        package_json = folder / 'package.json'
        if package_json.is_file():
            package = json.loads(package_json.read_text(encoding='utf-8'))
            cache[package['name']] = (folder, package)
    pending_packages = list(dependencies)
    installed = set()
    while pending_packages:
        name = pending_packages.pop()
        if name in installed or name.startswith('com.unity.modules.'):
            continue
        folder, package = cache[name]
        target = STAGE / 'Packages' / name
        if not target.exists():
            shutil.copytree(folder, target)
        installed.add(name)
        pending_packages.extend(package.get('dependencies', {}))
    for name in installed:
        dependencies.pop(name, None)
    (STAGE / 'Packages/manifest.json').write_text(json.dumps(dict(dependencies=dependencies), indent=2))
    guid_paths = {}
    for meta in (PROJECT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
        asset = Path(str(meta)[:-5])
        if match and asset.is_file():
            guid_paths[match[1]] = asset.relative_to(PROJECT).as_posix()
    pending = [PROJECT / 'ProjectSettings/GraphicsSettings.asset', PROJECT / 'ProjectSettings/QualitySettings.asset']
    copied = set()
    while pending:
        path = pending.pop()
        data = path.read_text(encoding='utf-8', errors='replace')
        for guid in re.findall(r'guid: ([a-f0-9]{32})', data):
            relative = guid_paths.get(guid)
            if not relative or relative in copied:
                continue
            copied.add(relative)
            copy(relative)
            asset = PROJECT / relative
            if asset.suffix.lower() in ('.asset', '.mat', '.shader', '.shadergraph', '.cs', '.shadersubgraph'):
                pending.append(asset)
    (PROJECT / 'Exports/Reviews/Classic64/RenderingDependencies.json').write_text(json.dumps(sorted(copied), indent=2))
    print(json.dumps(dict(validation_project=str(STAGE), rendering_dependencies=len(copied), embedded_packages=len(installed))))


if __name__ == '__main__':
    main()
