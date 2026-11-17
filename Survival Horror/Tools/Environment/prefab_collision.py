"""Stage the full project offline and promote only verified additive collider edits."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import sys

PROJECT=Path(__file__).resolve().parents[2]
REVIEW=PROJECT/'Exports/Reviews/PrefabCollision'
STAGE=REVIEW/'UnityProject'

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,value): p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def copy(a,b):
    b.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(a,b)

def stage():
    REVIEW.mkdir(parents=True,exist_ok=True)
    for name in ['BatchReceipt.json','MergedVerification.json','PromotionPlan.json','PromotionReceipt.json']:
        (REVIEW/name).unlink(missing_ok=True)
    baseline={}
    for folder in ['Assets','ProjectSettings','Packages/com.manta.house-toolkit']:
        for p in (PROJECT/folder).rglob('*'):
            if p.is_file():
                rel=p.relative_to(PROJECT).as_posix();copy(p,STAGE/rel)
                if p.suffix=='.prefab' or p.name.endswith('.prefab.meta'):
                    baseline[rel]=sha(p)
                    copy(p,REVIEW/'OriginalPrefabs'/rel)
    # Resolve the same package dependency closure from the installed local cache.
    cache=PROJECT/'Library/PackageCache'
    pending=list(read(PROJECT/'Packages/manifest.json')['dependencies']);deps={}
    while pending:
        name=pending.pop()
        if name in deps: continue
        if name.startswith('com.unity.modules.'):
            deps[name]='1.0.0';continue
        embedded=STAGE/'Packages'/name
        if embedded.is_dir(): continue
        matches=list(cache.glob(name+'@*/package.json'))
        if len(matches)!=1: raise RuntimeError('Cannot resolve installed package '+name)
        package=matches[0];deps[name]='file:'+package.parent.as_posix();pending.extend(read(package).get('dependencies',{}))
    write(STAGE/'Packages/manifest.json',{'dependencies':deps})
    copy(PROJECT/'Tools/Environment/PrefabCollisionBatch.cs',STAGE/'Assets/Editor/PrefabCollisionBatch.cs')
    write(REVIEW/'Baseline.json',baseline)
    print('Staged full project and',len(baseline)//2,'prefab GUID snapshots:',STAGE,flush=True)

HEADER=re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\r?\n',re.M)
def blocks(text):
    matches=list(HEADER.finditer(text))
    return {m.group(2):(int(m.group(1)),bool(m.group(3)),text[m.start():matches[i+1].start() if i+1<len(matches) else len(text)]) for i,m in enumerate(matches)}

def section(block,name):
    match=re.search(r'^  '+name+r':[^\n]*(?:\n(?!  [A-Za-z_]|---)[^\n]*)*\n?',block,re.M)
    return match

def merge(original,generated):
    """Keep original serialized gameplay data verbatim; transplant only native collision additions."""
    before,after=blocks(original),blocks(generated)
    new={k:v for k,v in after.items() if k not in before}
    for ident,(kind,stripped,block) in new.items():
        if kind not in {64,65,135,136,154} and not (kind in {1,4} and stripped):
            raise RuntimeError('Unexpected new object '+ident+' type '+str(kind))
    output=original
    for ident,(kind,stripped,block) in before.items():
        if ident not in after:
            if stripped and kind in {1,4}: continue  # Keep original prefab aliases even if Unity canonicalizes them on save.
            raise RuntimeError('Original object removed '+ident)
        updated=after[ident][2];replacement=block
        if kind==1 and not stripped:
            old=section(block,'m_Component');new_section=section(updated,'m_Component')
            if old and new_section:
                old_ids=set(re.findall(r'component: \{fileID: (-?\d+)\}',old.group()))
                new_ids=set(re.findall(r'component: \{fileID: (-?\d+)\}',new_section.group()))
                if not old_ids<=new_ids: raise RuntimeError('Existing component removed '+ident)
                added=new_ids-old_ids
                if any(i not in new or new[i][0] not in {64,65,135,136,154} for i in added): raise RuntimeError('Unexpected component change')
                if added:
                    addition=''.join('  - component: {fileID: '+i+'}\n' for i in sorted(added))
                    replacement=block[:old.end()]+addition+block[old.end():]
        elif kind==1001:
            # Unity creates added-component overrides for meshes inherited from another prefab/FBX.
            pattern=r'^    m_AddedComponents:.*?(?=^  m_SourcePrefab:|^    [A-Za-z_]+:|\Z)'
            old=re.search(pattern,block,re.M|re.S);new_section=re.search(pattern,updated,re.M|re.S)
            if old and new_section and old.group()!=new_section.group():
                for added in re.findall(r'addedObject: \{fileID: (-?\d+)\}',new_section.group()):
                    if added not in new or new[added][0] not in {64,65,135,136,154}:
                        # Existing added components are preserved and may already be in the original file.
                        if added not in before: raise RuntimeError('Unexpected prefab override')
                replacement=block[:old.start()]+new_section.group()+block[old.end():]
        if replacement!=block: output=output.replace(block,replacement,1)
    if not output.endswith('\n'):output+='\n'
    output+=''.join(v[2] for v in new.values() if not v[1])
    aliases={k:v for k,v in new.items() if v[1]}
    while aliases:
        refs=set(re.findall(r'\{fileID: (-?\d+)\}',output))
        needed={k:v for k,v in aliases.items() if k in refs}
        if not needed: break
        output+=''.join(v[2] for v in needed.values())
        for k in needed:del aliases[k]
    return output

def prepare_promotion():
    receipt=read(REVIEW/'BatchReceipt.json');assert all(a['missing_parts']==0 for a in receipt['assets'])
    (REVIEW/'MergedVerification.json').unlink(missing_ok=True)
    baseline=read(REVIEW/'Baseline.json');pending=[]
    for a in receipt['assets']:
        if not a['added']: continue
        rel=a['path'];target=PROJECT/rel
        if sha(target)!=baseline[rel] or sha(Path(str(target)+'.meta'))!=baseline[rel+'.meta']: raise RuntimeError('Concurrent edit '+rel)
        try:
            merged=merge((REVIEW/'OriginalPrefabs'/rel).read_text(encoding='utf-8-sig'),(STAGE/rel).read_text(encoding='utf-8-sig'))
        except RuntimeError as error:
            raise RuntimeError(rel+': '+str(error)) from error
        out=REVIEW/'Promotion'/rel;out.parent.mkdir(parents=True,exist_ok=True);out.write_text(merged,encoding='utf-8')
        # Verify the merged form again in Unity; no unrelated serialization changes leave the isolated project.
        copy(out,STAGE/rel);pending.append(rel)
    write(REVIEW/'PromotionPlan.json',{'prefabs':pending})
    print('Prepared collider-only edits:',len(pending),flush=True)

def promote():
    assert read(REVIEW/'MergedVerification.json')['verified']
    baseline=read(REVIEW/'Baseline.json');plan=read(REVIEW/'PromotionPlan.json')
    for rel in plan['prefabs']:
        if sha(PROJECT/rel)!=baseline[rel] or sha(PROJECT/(rel+'.meta'))!=baseline[rel+'.meta']:raise RuntimeError('Concurrent edit '+rel)
        if sha(STAGE/rel)!=sha(REVIEW/'Promotion'/rel):raise RuntimeError('Staged prefab changed '+rel)
    for rel in plan['prefabs']:copy(REVIEW/'Promotion'/rel,PROJECT/rel)
    assert all(sha(PROJECT/(rel+'.meta'))==baseline[rel+'.meta'] for rel in plan['prefabs'])
    write(REVIEW/'PromotionReceipt.json',{'verified':True,'changed_prefabs':len(plan['prefabs']),'guid_changes':0,'scene_changes':0})
    print('Promoted',len(plan['prefabs']),'prefabs; GUIDs, scenes and existing component data preserved.',flush=True)

if __name__=='__main__': {'stage':stage,'prepare-promotion':prepare_promotion,'promote':promote}[sys.argv[1]]()
