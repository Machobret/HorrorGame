"""Run on Superman_PlayerRig.blend to export unlit inspection views."""
import bpy
import pathlib
import shutil
import sys
import uuid

SOURCE=pathlib.Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Exports/Reviews/Superman'
OUTPUT.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(SOURCE))
import blender_review
blender_review.intersections(OUTPUT)
blender_review.render(OUTPUT)
bpy.context.preferences.filepaths.save_version=0
staged=OUTPUT/('Superman_'+uuid.uuid4().hex+'.blend')
bpy.ops.wm.save_as_mainfile(filepath=str(staged))
shutil.copyfile(staged,SOURCE/'Superman_PlayerRig.blend')
staged.unlink()
