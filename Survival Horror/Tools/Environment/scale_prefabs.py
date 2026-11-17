"""Stage/publish only the reviewed scale decisions; all validation occurs in isolated Unity."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import sys

PROJECT=Path(__file__).resolve().parents[2]
SOURCE=PROJECT/'ArtSource/Environment/ScaleCalibration'
REVIEW=PROJECT/'Exports/Reviews/PrefabCollision/ScaleAudit'
STAGE=REVIEW.parent/'UnityProject'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def guid(p):return re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(encoding='utf-8-sig'),re.M).group(1)

def promote():
    receipt=read(STAGE/'PrefabScaleReceipt.json');plan=read(SOURCE/'ScalePlan.json');baseline=read(REVIEW/'Baseline.json')
    assert receipt['verified'] and receipt['reviewed']==len(plan['assets'])
    verified={a['path']:a for a in receipt['assets']};pending=[]
    for a in plan['assets']:
        rel=a['path'];target=PROJECT/rel;staged=STAGE/rel
        assert target.resolve().is_relative_to((PROJECT/'Assets/Game/Environment').resolve()),rel
        assert sha(target)==baseline[rel]['sha256'] and sha(Path(str(target)+'.meta'))==baseline[rel]['meta_sha256'],'Concurrent edit '+rel
        assert guid(Path(str(target)+'.meta'))==guid(Path(str(staged)+'.meta'))==a['guid'],rel
        assert verified[rel]['guid']==a['guid'] and max(abs(x-y) for x,y in zip(verified[rel]['size_m'],a['target_size_m']))<.0005,rel
        # Scaling changes transforms and native labels; mesh/material/prefab dependencies must remain identical.
        references=lambda p:set(re.findall(rb'guid: ([0-9a-f]{32})',p.read_bytes()))
        assert references(target)==references(staged),'Dependencies changed '+rel
        for source,destination in [(staged,target),(Path(str(staged)+'.meta'),Path(str(target)+'.meta'))]:
            if sha(source)!=sha(destination):pending.append((source,destination))
    for source,target in pending:shutil.copy2(source,target)
    for a in plan['assets']:
        p=PROJECT/a['path'];assert sha(p)==sha(STAGE/a['path']) and guid(Path(str(p)+'.meta'))==a['guid']
    write(SOURCE/'ScaleReceipt.json',receipt)
    write(REVIEW/'PromotionReceipt.json',dict(verified=True,reviewed=receipt['reviewed'],resized=receipt['resized'],changed_files=len(pending),guid_changes=0,physics_checks=receipt['physics_checks']))
    print('Promoted',receipt['resized'],'size corrections;',receipt['reviewed'],'reviewed prefab GUIDs preserved.')

if __name__=='__main__':{'promote':promote}[sys.argv[1]]()
