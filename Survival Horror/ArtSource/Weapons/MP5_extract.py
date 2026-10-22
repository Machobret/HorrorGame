from pathlib import Path
from PIL import Image
import json
src=Path(r'C:/Users/Mangis/Downloads/Assets/Guns/PSXMP5[FIXED]/PSXMP5')
root=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror');out=root/'Assets/Game/Weapons/Models/MP5';art=root/'ArtSource/Weapons/MP5';out.mkdir(parents=True,exist_ok=True);art.mkdir(parents=True,exist_ok=True)
verts=[];uvs=[];normals=[];faces={};obj=''
for line in (src/'MP5.obj').read_text().splitlines():
 a=line.split()
 if not a:continue
 if a[0]=='v':verts.append(tuple(map(float,a[1:4])))
 elif a[0]=='vt':uvs.append(tuple(map(float,a[1:3])))
 elif a[0]=='vn':normals.append(tuple(map(float,a[1:4])))
 elif a[0]=='o':obj=a[1];faces[obj]=[]
 elif a[0]=='f':faces[obj].append([tuple(int(x)-1 if x else None for x in c.split('/')) for c in a[1:]])
names=['MP5BodyFull','MP5FullStock','MP5Magazene','Trigger.002'];origin=(-.95,8.,0);scale=.68/(2.969927+4.000673)
def pt(v):return (-v[2]*scale,(v[1]-origin[1])*scale,(v[0]-origin[0])*scale)
def norm(v):return (-v[2],v[1],v[0])
lines=['# Standard fixed-stock MP5 only. Baked metres, firing grip origin, +Z muzzle.','mtllib MP5.mtl'];vi=ti=ni=0;record={}
for name in names:
 fs=faces[name];vmap={i:vi+j+1 for j,i in enumerate(sorted({f[0] for face in fs for f in face}))};tmap={i:ti+j+1 for j,i in enumerate(sorted({f[1] for face in fs for f in face if len(f)>1 and f[1] is not None}))};nmap={i:ni+j+1 for j,i in enumerate(sorted({f[2] for face in fs for f in face if len(f)>2 and f[2] is not None}))}
 lines.extend(['o '+name,'g '+name])
 for i in vmap:lines.append('v '+' '.join(f'{x:.7f}' for x in pt(verts[i])))
 for i in tmap:lines.append('vt '+' '.join(f'{x:.7f}' for x in uvs[i]))
 for i in nmap:lines.append('vn '+' '.join(f'{x:.7f}' for x in norm(normals[i])))
 lines.extend(['usemtl MP5Material','s off'])
 for face in fs:lines.append('f '+' '.join(str(vmap[f[0]])+'/'+(str(tmap[f[1]]) if len(f)>1 and f[1] is not None else '')+'/'+(str(nmap[f[2]]) if len(f)>2 and f[2] is not None else '') for f in face))
 points=[pt(verts[i]) for i in vmap];record[name]={'vertices':len(points),'faces':len(fs),'min':[min(p[a] for p in points) for a in range(3)],'max':[max(p[a] for p in points) for a in range(3)]};vi+=len(vmap);ti+=len(tmap);ni+=len(nmap)
(out/'MP5.obj').write_text('\n'.join(lines)+'\n');(out/'MP5.mtl').write_text('newmtl MP5Material\nKd 1.0 1.0 1.0\nmap_Kd MP5_Albedo.png\n');Image.open(src/'Textures/MP5Texture.jpg').save(out/'MP5_Albedo.png');Image.open(src/'Textures/MP5Texture.jpg').save(art/'MP5_Original_Texture.png')
body=[pt(verts[i]) for face in faces['MP5BodyFull'] for f in face for i in [f[0]] if verts[i][0]>2.94];record['Muzzle']=[sum(p[a] for p in body)/len(body) for a in range(3)];record['source']=str(src);record['bakedScale']=scale;record['gripSource']=origin;(art/'Extraction.json').write_text(json.dumps(record,indent=2));print(json.dumps(record,indent=2))