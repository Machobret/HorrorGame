"""Promote only verified Unity-authored prefab labels, preserving GUIDs and prefab data."""
from pathlib import Path
import hashlib
import json
import re
import shutil

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
REVIEW = PROJECT / "Exports/Reviews/DownloadedEnvironmentPacks/QualityReview"
STAGE = REVIEW.parent / "ImportProject"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    return re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8-sig"), re.MULTILINE).group(1)


def main():
    assert (REVIEW / "LabelsVerified.txt").is_file(), "Run DownloadedPrefabReview.LabelBatch first"
    catalog = json.loads((SOURCE / "QualityCatalog.json").read_text(encoding="utf-8-sig"))
    baseline = json.loads((REVIEW / "LabelBaseline.json").read_text(encoding="utf-8-sig"))
    changes = []
    for entry in catalog["assets"]:
        if entry['status'] == 'Removed':
            continue
        target = PROJECT / (entry["prefab"] + ".meta")
        source = STAGE / (entry["prefab"] + ".meta")
        assert target.resolve().is_relative_to((PROJECT / "Assets/Game/Environment").resolve()), target
        assert sha(target) == baseline[entry["prefab"]]["sha256"], f"Project metadata changed during review: {target}"
        assert guid(source) == guid(target) == entry["guid"], entry["prefab"]
        assert sha(PROJECT / entry["prefab"]) == sha(STAGE / entry["prefab"]), f"Prefab changed during review: {entry['prefab']}"
        text = source.read_text(encoding="utf-8-sig")
        for label in entry["labels"]:
            assert re.search(r"^- " + re.escape(label) + r"$", text, re.MULTILINE), (entry["prefab"], label)
        changes.append((source, target, entry))
    for source, target, entry in changes:
        shutil.copy2(source, target)
        assert guid(target) == entry["guid"]
    receipt = {"verified": True, "prefabs_labelled": len(changes), "guid_changes": 0, "prefab_data_changes": 0,
               "counts": catalog["counts"], "originals_preserved": True,
               "unity_queries": ["t:Prefab l:DownloadedKeep", "t:Prefab l:DownloadedMissingTextures", "t:Prefab l:DownloadedFallbackMaterial",
                                 "t:Prefab l:DownloadedReview", "t:Prefab l:DownloadedBackgroundOnly", "t:Prefab l:DownloadedExclude"]}
    (SOURCE / "QualityLabelReceipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    main()
