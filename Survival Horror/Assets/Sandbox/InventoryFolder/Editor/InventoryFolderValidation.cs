using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.UI;
using HorrorGame.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    [InitializeOnLoad]
    public static class InventoryFolderValidation
    {
        const string Pending="InventoryFolder.Validation";
        static double deadline,next;
        static int stage;
        static UIInventory ui;
        static Inventory inventory;
        static UIInventoryItem[] slots;
        static UIItemFiller info;
        static UIInventoryContextMenu menu;
        static float fogClock;
        static Health previewHealth;
        static float originalHealth;
        static ItemData[] examinationItems;
        static int examinationIndex;
        static UIDocs docs;
        static DocumentData sampleDocument;
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static InventoryFolderValidation(){if(SessionState.GetBool(Pending,false)){EditorApplication.update+=Tick;next=EditorApplication.timeSinceStartup+3;deadline=next+120;}}
        public static void BuildAndRun()
        {
            if(!Application.isBatchMode)throw new Exception("Use isolated validation editor");
            InventoryFolderBuilder.Build(useReporterMenu:false);EditorSceneManager.OpenScene("Assets/Game/Levels/StorageFacility/Scenes/StorageFacility_Blockout.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);Debug.Log("INVENTORY PASS: "+message);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish("Timeout");return;}
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(stage==0)
                {
                    if(!GameManager.Exists||!GameManager.Instance.Player)return;
                    inventory=GameManager.Instance.Inventory;inventory.Unequip(EquipmentSlot.Primary);inventory.Unequip(EquipmentSlot.Secondary);inventory.Clear();inventory.ResetCapacity(12);
                    string[] paths={"Assets/Game/Weapons/FlareGun/Data/FlareGun.asset","Assets/Game/Weapons/HuntingRifle/Data/HuntingRifle.asset","Assets/Game/Weapons/MP5/Data/MP5.asset","Assets/Game/Weapons/Revolver/Data/Revolver.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset","Assets/Game/Weapons/Ammunition/HuntingRifleAmmo/Data/HuntingRifleAmmo.asset","Assets/Game/Weapons/Ammunition/FlareAmmo/Data/FlareAmmo.asset"};
                    foreach(string path in paths){var item=AssetDatabase.LoadAssetAtPath<ItemData>(path);Check(item,"preview item exists: "+Path.GetFileNameWithoutExtension(path));inventory.Add(item,item is ReloadableWeaponData?1:3,item is ReloadableWeaponData?1:0);}
                    previewHealth=GameManager.Instance.Player.GetComponent<Health>();originalHealth=previewHealth.Value;previewHealth.Value=previewHealth.Max;
                    ui=UIManager.Get<UIInventory>();ui.Show();var data=new SerializedObject(ui);var array=data.FindProperty("m_ItemSlots");slots=Enumerable.Range(0,array.arraySize).Select(i=>(UIInventoryItem)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();info=(UIItemFiller)data.FindProperty("m_ItemInfoFiller").objectReferenceValue;menu=(UIInventoryContextMenu)data.FindProperty("m_ContextMenu").objectReferenceValue;
                    EventSystem.current.SetSelectedGameObject(slots[0].gameObject);stage=1;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==1)
                {
                    Check(PauseController.Instance.IsPaused,"native inventory pauses gameplay");
                    Check(slots.Count(s=>s.gameObject.activeSelf)==inventory.Items.Count,"native capacity and slot visibility preserved");
                    Check(inventory.UsedSlots==11 && inventory.FreeSlots==1,"large weapons consume two actual inventory places");
                    Check(ui.GetComponentInChildren<InventoryFootprintLayout>(),"span layout component installed");Check(((RectTransform)slots[1].transform).rect.width>((RectTransform)slots[0].transform).rect.width*1.8f,"two-slot weapons visibly span two places");Check(slots.Where(s=>s.InventoryEntry?.Item).All(s=>s.GetComponent<CanvasGroup>().alpha>.9f && ((RectTransform)s.transform).rect.height>25),"all occupied footprints remain visible");Check(!inventory.CanAdd(slots[2].InventoryEntry.Item,1),"two-slot pickup blocked with only one free place");
                    var selection=slots[0].GetComponentsInChildren<InventorySelectionFrame>().FirstOrDefault();Check(selection && !selection.raycastTarget,"selected item has hollow ivory frame");Check(selection.GetComponentInParent<Image>().color.a==0,"selection wash is transparent");
                    Check(info.m_ItemName.text==(string)slots[0].InventoryEntry.Item.Name,"selection fills notebook title");
                    Check(info.m_ItemDescription.text==(string)slots[0].InventoryEntry.Item.Description,"selection fills notebook description");
                    Check(info.m_ItemCount.text=="Loaded: 1 / 1","weapon quantity has a loaded-round label");
                    var presentation=ui.GetComponent<InventoryFolderPresentation>();fogClock=presentation.Fog.material.GetFloat("_FogTime");
                    var fluid=ui.GetComponentInChildren<InventoryConditionFluid>();Check(fluid.Label.text=="FINE","full health shows fine condition");Check(!fluid.Window.enabled,"condition is text only");
                    Capture("Inventory_Folder.png");previewHealth.Value=previewHealth.Max*.5f;stage=10;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==10)
                {
                    var fluid=ui.GetComponentInChildren<InventoryConditionFluid>();Check(fluid.Label.text=="INJURED","injured health shows injured text");Check(!fluid.Window.enabled,"condition remains text only after damage");Capture("Inventory_Condition_Injured.png");previewHealth.Value=previewHealth.Max*.15f;stage=11;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==11)
                {
                    var fluid=ui.GetComponentInChildren<InventoryConditionFluid>();Check(fluid.Label.text=="CRITICAL","critical health shows critical text");Check(!fluid.ShowFluid,"fluid presentation stays disabled");Capture("Inventory_Condition_Critical.png");previewHealth.Value=originalHealth;stage=2;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(stage==2)
                {
                    Check(ui.GetComponent<InventoryFolderPresentation>().Fog.material.GetFloat("_FogTime")>fogClock,"fog animates while gameplay is paused");
                    Submit();Check(menu.gameObject.activeSelf,"native item actions open");Check(menu.EquipButton.gameObject.activeSelf,"equip action available");
                    Capture("Inventory_Actions.png");menu.EquipButton.onClick.Invoke();Check(inventory.GetEquipped(EquipmentSlot.Primary)?.Item==slots[0].InventoryEntry.Item,"native equip action works");Check(ui.GetComponent<InventoryFolderPresentation>().FeedbackText.text=="Equipped","equip confirmation uses actual result");
                    ui.Hide();Check(!PauseController.Instance.IsPaused,"closing restores gameplay");ui.Show();EventSystem.current.SetSelectedGameObject(slots[7].gameObject);stage=3;next=EditorApplication.timeSinceStartup+.4;return;
                }
                if(stage==3)
                {
                    Submit();Check(menu.CombineButton.gameObject.activeSelf,"combine action retained for ammo");menu.CombineButton.onClick.Invoke();Check(typeof(UIInventory).GetField("m_CombiningSlot",Private).GetValue(ui)!=null,"native combine selection works");ui.OnCancel();Check(typeof(UIInventory).GetField("m_CombiningSlot",Private).GetValue(ui)==null,"cancel clears combine selection");
                    Submit();menu.CombineButton.onClick.Invoke();EventSystem.current.SetSelectedGameObject(slots[6].gameObject);Submit();Check(ui.GetComponent<InventoryFolderPresentation>().FeedbackText.text=="Cannot combine these items","invalid combination has clear feedback");
                    Submit();int before=inventory.Items.Count(e=>e.Item);menu.DropButton.onClick.Invoke();Check(inventory.Items.Count(e=>e.Item)==before-1,"native drop removes selected item");Check(!PauseController.Instance.IsPaused,"drop returns to gameplay");
                    Check(GameManager.Instance.Player.GetComponent<PlayerMovement>().enabled,"player movement remains enabled");
                    sampleDocument=AssetDatabase.FindAssets("t:DocumentData").Select(g=>AssetDatabase.LoadAssetAtPath<DocumentData>(AssetDatabase.GUIDToAssetPath(g))).First(d=>d.Pages.Count>0 && (Sprite)d.Image);
                    inventory.Documents.Clear();inventory.ReadDocuments.Clear();inventory.Documents.Add(sampleDocument);docs=UIManager.Get<UIDocs>();docs.Show();stage=4;next=EditorApplication.timeSinceStartup+.6;return;
                }
                if(stage==4)
                {
                    var entries=(List<UIDocumentEntry>)typeof(UIDocs).GetField("m_Entries",Private).GetValue(docs);var entry=entries.First(e=>e.Data==sampleDocument);EventSystem.current.SetSelectedGameObject(entry.gameObject);
                    Check(PauseController.Instance.IsPaused,"files menu pauses gameplay");Check(entry.GetComponent<InventoryFileAnnotation>().Unread.activeSelf,"unread file has folded corner");Check(entry.GetComponent<InventoryFileAnnotation>().Title.text==sampleDocument.Name,"file thumbnail has title");
                    var title=(TMP_Text)new SerializedObject(docs).FindProperty("m_DocumentName").objectReferenceValue;Check(title.text==sampleDocument.Name,"file selection displays document title");
                    var entryRect=(RectTransform)entry.transform;Check(entry.gameObject.activeInHierarchy && entryRect.rect.width>90,"file entry is visible and sized");Debug.Log("FILE POSITION "+entryRect.position);Capture("Inventory_Files.png",docs.GetComponent<Canvas>());docs.OnSubmit();Check(UIManager.Get<UIDocument>().gameObject.activeSelf,"native file opens reader");stage=5;next=EditorApplication.timeSinceStartup+.4;return;
                }
                if(stage==5)
                {
                    var reader=UIManager.Get<UIDocument>();var text=(TMP_Text)new SerializedObject(reader).FindProperty("m_Text").objectReferenceValue;Check(text.text==sampleDocument.Pages[0].Text,"native document contents preserved");Check(inventory.ReadDocuments.Contains(sampleDocument),"opening file marks it read");Check(inventory.GetSavableData().ReadDocuments.Contains(sampleDocument.UniqueId),"read state included in save data");
                    var save=inventory.GetSavableData();save.Items=new List<InventoryEntrySaveData>();save.EquippedItems=new List<InventoryEquippedEntrySaveData>();save.Maps=new List<string>();
                    var restored=new Inventory();restored.SetFromSavedData(JsonUtility.FromJson<InventorySaveData>(JsonUtility.ToJson(save)));Check(restored.ReadDocuments.Contains(sampleDocument),"read state survives save/load");
                    save.ReadDocuments=null;restored.SetFromSavedData(save);Check(restored.ReadDocuments.Count==0,"older saves without read state load safely");
                    typeof(UIDocument).GetMethod("Hide",Private).Invoke(reader,null);Check(!PauseController.Instance.IsPaused,"closing file restores gameplay");
                    examinationItems=AssetDatabase.FindAssets("t:ItemData").Select(g=>AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).Where(i=>i && i.Flags.HasFlag(ItemFlags.Examinable)).ToArray();
                    Check(examinationItems.All(i=>i.ExamineModel),"examinable items have models: "+string.Join(",",examinationItems.Where(i=>!i.ExamineModel).Select(i=>i.name)));stage=20;next=EditorApplication.timeSinceStartup+.2;return;
                }
                if(stage==20)
                {
                    if(examinationIndex>=examinationItems.Length){Check(!PauseController.Instance.IsPaused,"all inspections close cleanly");TabletWorkspaceChecks.Begin();stage=30;next=EditorApplication.timeSinceStartup+3.5;return;}
                    UIManager.Get<UIExamineItem>().Show(examinationItems[examinationIndex],false);stage=21;next=EditorApplication.timeSinceStartup+.15;return;
                }
                if(stage==21)
                {
                    var renderer=Object.FindFirstObjectByType<UIExamineItemRenderer>();var model=renderer.PreviewObject.GetChild(renderer.PreviewObject.childCount-1);var camera=renderer.Camera;
                    var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();Check(renderers.Length>0,"inspection geometry exists: "+examinationItems[examinationIndex].name);
                    foreach(var r in renderers)Check((camera.cullingMask & (1<<r.gameObject.layer))!=0,"inspection layer visible: "+r.name);
                    var rotation=renderer.PreviewObject.localRotation;
                    foreach(var angle in new[]{Vector3.zero,new Vector3(90,0,0),new Vector3(0,90,0),new Vector3(45,135,45)})
                    {
                        renderer.PreviewObject.localRotation=Quaternion.Euler(angle);Physics.SyncTransforms();
                        foreach(var r in renderers){var b=r.localBounds;for(int k=0;k<8;k++){var point=r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));var v=camera.WorldToViewportPoint(point);if(v.x<0 || v.x>1 || v.y<0 || v.y>1 || v.z<camera.nearClipPlane || v.z>camera.farClipPlane)throw new Exception("Inspection clips "+examinationItems[examinationIndex].name+" at "+angle+": "+v+" clips "+camera.nearClipPlane+".."+camera.farClipPlane);}}
                    }
                    renderer.PreviewObject.localRotation=rotation;Debug.Log("INSPECT PASS: fitted and rotation-safe "+examinationItems[examinationIndex].name);UIManager.Get<UIExamineItem>().Hide();examinationIndex++;stage=20;next=EditorApplication.timeSinceStartup+.1;return;
                }
                if(stage==30){if(TabletWorkspaceChecks.Tick()){Finish(null);return;}next=EditorApplication.timeSinceStartup+.5;return;}
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Submit()=>typeof(UIInventory).GetMethod("OnSubmit",Private).Invoke(ui,null);
        static void Capture(string name,Canvas targetCanvas=null)
        {
            var pipeline=new SerializedObject(GraphicsSettings.currentRenderPipeline).FindProperty("m_RendererDataList");var features=Enumerable.Range(0,pipeline.arraySize).SelectMany(i=>((ScriptableRendererData)pipeline.GetArrayElementAtIndex(i).objectReferenceValue).rendererFeatures).Where(f=>f).Distinct().ToArray();var active=features.Select(f=>f.isActive).ToArray();foreach(var f in features)f.SetActive(false);
            var canvas=targetCanvas?targetCanvas:ui.GetComponent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var cameraObject=new GameObject("Inventory review camera");var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(1600,900,24);camera.targetTexture=target;var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);Vector3? pagePosition=null;Transform activePage=null;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();
                if(targetCanvas){var data=new SerializedObject(docs);var pages=(Transform[])typeof(UIDocs).GetField("m_Pages",Private).GetValue(docs);activePage=pages.First(p=>p.gameObject.activeSelf);pagePosition=activePage.localPosition;activePage.position=((Transform)data.FindProperty("m_PageCenterHook").objectReferenceValue).position;Canvas.ForceUpdateCanvases();}
                camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory("Exports/Reviews/InventoryFolder");File.WriteAllBytes("Exports/Reviews/InventoryFolder/"+name,image.EncodeToPNG());Check(image.GetPixels32().Count(p=>p.r+p.g+p.b>100)>10000,"inventory renders visible paper and items");
                if(!targetCanvas){var button=slots[0].GetComponent<Button>();var rect=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Any(h=>h.gameObject.transform.IsChildOf(button.transform)),"slot pointer hit testing retained");}
            }
            finally{if(activePage && pagePosition.HasValue)activePage.localPosition=pagePosition.Value;canvas.renderMode=mode;canvas.worldCamera=oldCamera;RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);for(int i=0;i<features.Length;i++)features[i].SetActive(active[i]);}
        }
        static void Finish(string error){TabletWorkspaceChecks.RestorePreferences();SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;if(error!=null)Debug.LogError(error);EditorApplication.Exit(error==null?0:1);}
    }
}
