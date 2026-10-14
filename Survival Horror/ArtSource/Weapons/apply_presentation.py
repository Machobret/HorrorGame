"""Update only audio references and icon import limits; preserve asset GUIDs and pickup IDs."""
from pathlib import Path
import re, uuid
ROOT=Path(__file__).resolve().parents[2];WEAPONS=ROOT/'Assets/Game/Weapons'
template=(ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/GunFire.wav.meta').read_text()
for clip in (WEAPONS/'Audio').glob('*.wav'):
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
    data=WEAPONS/'Data'/(name+'.asset');text=data.read_text()
    for kind,property in [('Shot','ShotSound'),('Reload','ReloadSound')]:
        meta=WEAPONS/'Audio'/(name+'_'+kind+'.wav.meta')
        if not meta.exists():continue
        guid=re.search(r'guid: ([0-9a-f]+)',meta.read_text())[1]
        text=re.sub(r'  '+property+r': \{[^}]+\}',f'  {property}: {{fileID: 8300000, guid: {guid}, type: 3}}',text)
    data.write_text(text)
    meta=WEAPONS/'Models'/name/(name+'_Icon.png.meta')
    text=meta.read_text();text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',text);meta.write_text(text)
print('PRESENTATION_REFERENCES_APPLIED')
for path in (WEAPONS/'Audio', WEAPONS/'Editor/WeaponPresentationBuilder.cs'):
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        importer='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if path.is_dir() else 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+importer)
clips={re.search(r'guid: ([0-9a-f]+)',p.read_text())[1] for p in (WEAPONS/'Audio').glob('*.wav.meta')}
for name in ('Revolver','HuntingRifle','FlareGun'):
    shot=re.search(r'ShotSound: .*guid: ([0-9a-f]+)',(WEAPONS/'Data'/(name+'.asset')).read_text())[1]
    assert shot in clips, 'Missing shot reference '+name
    print('SHOT_REFERENCE_OK',name)
