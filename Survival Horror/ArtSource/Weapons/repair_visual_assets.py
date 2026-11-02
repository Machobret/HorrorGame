"""Apply material references without rebuilding or resetting tuned weapon prefabs."""

# Shared paths keep generated assets beside their owning weapon.
import sys as _sys
from pathlib import Path as _Path
_sys.path.insert(0, str(next(p for p in _Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir()) / "Tools" / "Art"))
from weapon_paths import weapon_asset
from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]/'Assets/Game/Weapons'
for model in ('HuntingRifle','FlareGun','FlareAmmo','RifleAmmo'):
    for mat in weapon_asset('Materials', model + '_Material_').parent.glob(model+'_Material_*.mat'):
        texture=('ScopeText.jpg' if '067' in mat.stem else 'HuntingRifleText.png') if model=='HuntingRifle' else ('FlareAmmo_Albedo.png' if model=='FlareAmmo' else 'Flaregun_Text_240.png' if model=='FlareGun' else 'RifleAmmoText_240.png')
        tex=weapon_asset('Models', str(model) + "/" + str(texture))
        assert tex.exists() and tex.with_suffix(tex.suffix+'.meta').exists(), tex
        guid=re.search(r'^guid: (\w+)',tex.with_suffix(tex.suffix+'.meta').read_text(),re.M)[1]
        text=mat.read_text()
        for slot in ('_BaseMap','_MainTex'):
            text,count=re.subn(r'(- '+slot+r':\s*\n\s*m_Texture:) \{[^}]+\}',r'\1 {fileID: 2800000, guid: '+guid+r', type: 3}',text)
            # The template's unlit shader only exposes _MainTex; _BaseMap can
            # disappear when Unity saves a material after the shader change.
            assert count==1 if slot=='_MainTex' else count<=1,(mat,slot,count)
        mat.write_text(text)
        print('TEXTURED',mat.name,texture)
for meta in root.rglob('*_Icon.png.meta'):
    text=meta.read_text();text=re.sub(r'filterMode: \d+', 'filterMode: 1',text);meta.write_text(text)
glow=weapon_asset('Materials', str('FlareGlow.mat'))
text=glow.read_text();text=re.sub(r'(_BaseColor:) \{[^}]+\}',r'\1 {r: 8, g: 0.025, b: 0.004, a: 1}',text);glow.write_text(text)
prefab=weapon_asset('Effects', str('SignalFlare.prefab'))
text=prefab.read_text();text=text.replace('m_Color: {r: 1, g: 0.18, b: 0.045, a: 1}','m_Color: {r: 1, g: 0.008, b: 0.005, a: 1}').replace('m_Intensity: 3.5','m_Intensity: 12').replace('m_Range: 9\n','m_Range: 12\n')
if '  GlowMaterial:' not in text:
    text=re.sub(r'(  FlareLight: [^\n]+)',r'\1\n  LightIntensity: 12\n  GlowMaterial: {fileID: 2100000, guid: cc2b0fb70d0e4c82b00f911f13ce8e02, type: 2}',text)
prefab.write_text(text)
