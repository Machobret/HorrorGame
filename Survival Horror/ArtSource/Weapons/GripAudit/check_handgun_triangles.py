from pathlib import Path
import numpy as np
ROOT=Path('C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/HandgunClipping')
def load(path):
 v=[];groups={};group=''
 for l in path.read_text().splitlines():
  a=l.split()
  if not a:continue
  if a[0]=='o':group=a[1];groups.setdefault(group,[])
  elif a[0]=='v':v.append([float(x) for x in a[1:]])
  elif a[0]=='f':groups[group].append([int(x)-1 for x in a[1:]])
 v=np.array(v);return {k:v[np.array(t)] for k,t in groups.items() if t}
def hits(a,b):
 if not len(a) or not len(b):return 0
 count=0
 for tri in a:
  mask=np.all(b.max(axis=1)>=tri.min(axis=0)-1e-6,axis=1)&np.all(b.min(axis=1)<=tri.max(axis=0)+1e-6,axis=1)
  targets=b[mask]
  if not len(targets):continue
  e1=targets[:,1]-targets[:,0];e2=targets[:,2]-targets[:,0]
  for j in range(3):
   origin=tri[j];direction=tri[(j+1)%3]-origin
   h=np.cross(np.broadcast_to(direction,e2.shape),e2);det=np.sum(e1*h,axis=1);valid=np.abs(det)>1e-10
   inv=np.where(valid,1/np.where(valid,det,1),0);s=origin-targets[:,0];u=inv*np.sum(s*h,axis=1);q=np.cross(s,e1);vv=inv*np.sum(direction*q,axis=1);t=inv*np.sum(e2*q,axis=1)
   count+=int(np.count_nonzero(valid&(u>1e-5)&(vv>1e-5)&(u+vv<1-1e-5)&(t>1e-4)&(t<1-1e-4)))
 return count
def collisions(groups):
 weapon=np.concatenate([t for k,t in groups.items() if k.startswith('Weapon_')])
 return {hand:hits(t,weapon)+hits(weapon,t) for hand,t in groups.items() if hand in ['RightHand','LeftHand']}
if __name__=='__main__':
 for n in ['Gun','Revolver','FlareGun']:
  for pose in ['Idle','Aim']:
   g=load(ROOT/f'{n}_{pose}.obj');print(n,pose,{k:len(t) for k,t in g.items()},collisions(g))
