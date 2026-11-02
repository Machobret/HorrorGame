"""Update only audio references and icon import limits; preserve asset GUIDs and pickup IDs."""

# Shared paths keep generated assets beside their owning weapon.
import sys as _sys
from pathlib import Path as _Path
_sys.path.insert(0, str(next(p for p in _Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir()) / "Tools" / "Art"))
from weapon_paths import weapon_asset
from pathlib import Path
import re, uuid
ROOT=Path(__file__).resolve().parents[2];WEAPONS=ROOT/'Assets/Game/Weapons'
template=(ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/GunFire.wav.meta').read_text()
for clip in WEAPONS.glob('*/Audio/*.wav'):
    meta=Path(str(clip)+'.meta')
    text=meta.read_text() if meta.exists() else re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template,count=1)
    text=re.sub(r'compressionFormat: \d+','compressionFormat: 0',text)
    text=re.sub(r'loadType: \d+','loadType: 0',text)
    text=re.sub(r'forceToMono: \d+','forceToMono: 1',text)
    text=re.sub(r'preloadAudioData: \d+','preloadAudioData: 1',text)
    text=re.sub(r'normalize: \d+','normalize: 0',text)
    text=text.split('AssetOrigin:')[0]
    meta.write_text(text)
for name in ('Revolver','HuntingRifle','FlareGun'):
    data=weapon_asset('Data', str(name+'.asset'));text=data.read_text()
    for kind,property in [('Shot','ShotSound'),('Reload','ReloadSound')]:
        meta=weapon_asset('Audio', str(name+'_'+kind+'.wav.meta'))
        if not meta.exists():continue
        guid=re.search(r'guid: ([0-9a-f]+)',meta.read_text())[1]
        text=re.sub(r'  '+property+r': \{[^}]+\}',f'  {property}: {{fileID: 8300000, guid: {guid}, type: 3}}',text)
    data.write_text(text)
    meta=weapon_asset('Models', str(name) + "/" + str(name+'_Icon.png.meta'))
    text=meta.read_text();text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',text);meta.write_text(text)
print('PRESENTATION_REFERENCES_APPLIED')
clips={re.search(r'guid: ([0-9a-f]+)',p.read_text())[1] for p in WEAPONS.glob('*/Audio/*.wav.meta')}
for name in ('Revolver','HuntingRifle','FlareGun'):
    shot=re.search(r'ShotSound: .*guid: ([0-9a-f]+)',(weapon_asset('Data', str(name+'.asset'))).read_text())[1]
    assert shot in clips, 'Missing shot reference '+name
    print('SHOT_REFERENCE_OK',name)
