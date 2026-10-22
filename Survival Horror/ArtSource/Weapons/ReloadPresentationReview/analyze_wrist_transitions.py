from pathlib import Path
import csv,math,json,shutil
root=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror/ArtSource/Weapons/ReloadPresentationReview')
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation')
out=root/'WristTransitions';out.mkdir(exist_ok=True)
def angle(a,b,p):
 q=[float(a[p+c]) for c in 'xyzw'];v=[float(b[p+c]) for c in 'xyzw'];dot=abs(sum(x*y for x,y in zip(q,v)))/math.sqrt(sum(x*x for x in q)*sum(y*y for y in v));return math.degrees(2*math.acos(min(1,dot)))
def analyze(folder):
 rows=[r for r in csv.DictReader((folder/'WristPoses.csv').open()) if r.get('rlqw')];peaks={};transitions={}
 for a,b in zip(rows,rows[1:]):
  if a['weapon']!=b['weapon']:continue
  key=b['weapon']+'/'+b['phase'];p=peaks.setdefault(key,{'worldWristDegreesPerFrame':0,'localWristDegreesPerFrame':0})
  p['worldWristDegreesPerFrame']=max(p['worldWristDegreesPerFrame'],angle(a,b,'lq'),angle(a,b,'rq'))
  p['localWristDegreesPerFrame']=max(p['localWristDegreesPerFrame'],angle(a,b,'llq'),angle(a,b,'rlq'))
  if a['state']!=b['state']:transitions[key]=transitions.get(key,0)+1
 return peaks,transitions
base,_=analyze(src/'WristTransitionsBaseline');final,transitions=analyze(src/'WristTransitionsArmFinal')
report={k:{'baseline':base.get(k),'final':v} for k,v in final.items()};(out/'WristChangeReport.json').write_text(json.dumps(report,indent=2));(out/'TransitionCounts.json').write_text(json.dumps(transitions,indent=2))
for k,v in report.items():
 b=v['baseline'];f=v['final']
 if b and (b['localWristDegreesPerFrame']>20 or f['localWristDegreesPerFrame']>20):print(k,'local',round(b['localWristDegreesPerFrame'],1),'->',round(f['localWristDegreesPerFrame'],1),'world',round(b['worldWristDegreesPerFrame'],1),'->',round(f['worldWristDegreesPerFrame'],1))
for folder,name in [('WristTransitionsBaseline','Before'),('WristTransitionsArmFinal','After')]:shutil.copy2(src/folder/'WristPoses.csv',out/(name+'_WristPoses_60fps.csv'))