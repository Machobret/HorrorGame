using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    [InitializeOnLoad]
    public static class TabletMapValidation
    {
        const string Pending="TabletMap.Validation";
        static double deadline,next;
        static int stage;
        static UIMap ui;
        static MapData map;
        static TabletMapValidation(){if(SessionState.GetBool(Pending,false)){EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+120;}}
        public static void BuildAndRun()
        {
            if(!Application.isBatchMode)throw new Exception("Use isolated batch editor");
            TabletMapBuilder.Build();
            EditorSceneManager.OpenScene("Assets/Game/Levels/StorageFacility/Scenes/StorageFacility_Blockout.unity");
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);Debug.Log("TABLET PASS: "+message);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish("Timeout");return;}
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(stage==0)
                {
                    if(!GameManager.Exists||!GameManager.Instance.Player)return;
                    ui=UIManager.Get<UIMap>();map=Object.FindFirstObjectByType<MapController>().Data;
                    GameManager.Instance.Inventory.Maps.Add(map);
                    ui.Show(map);stage=10;next=EditorApplication.timeSinceStartup+.3;return;
                }
                if(stage==10)
                {
                    Check(ui.GetComponent<HorrorGame.UI.TabletStartupScreen>().Screen.gameObject.activeSelf,"first opening shows hero startup");
                    Capture("Tablet_Startup.png");stage=1;next=EditorApplication.timeSinceStartup+3;return;
                }
                if(stage==1)
                {
                    var state=new SerializedObject(ui);
                    Check(ui.CurrentMap==map,"native map selection retained");
                    Check(!Find("NS").gameObject.activeSelf,"north/south compass removed");
                    var directory=(UIMapList)state.FindProperty("m_MapList").objectReferenceValue;
                    Check(!directory.gameObject.activeSelf,"directory initially hidden");
                    float expanded=((RawImage)state.FindProperty("m_MapImage").objectReferenceValue).rectTransform.anchorMin.x;
                    var displayFilter=ui.GetComponent<HorrorGame.UI.TabletDisplayFilter>();
                    Canvas.ForceUpdateCanvases();
                    var buttonRect=(RectTransform)Find("Directory toggle");
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(displayFilter.DisplayCamera,buttonRect.TransformPoint(buttonRect.rect.center))};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Any(hit=>hit.gameObject==buttonRect.gameObject),"filtered display preserves pointer hit testing");
                    Find("Directory toggle").GetComponent<Button>().onClick.Invoke();
                    Check(directory.gameObject.activeSelf,"button opens map directory");
                    Check(((RawImage)state.FindProperty("m_MapImage").objectReferenceValue).rectTransform.anchorMin.x==expanded,"directory overlays without moving map");
                    Check(directory.transform.GetSiblingIndex()>((RawImage)state.FindProperty("m_MapImage").objectReferenceValue).transform.GetSiblingIndex(),"directory draws above map");
                    foreach(string name in new[]{"ArrowL","ArrowR","ArrowT","ArrowD"})Check(ui.GetComponentsInChildren<Image>(true).Single(i=>i.name==name).color.a<.5f,"translucent "+name);
                    var text=ui.GetComponentsInChildren<TMP_Text>(true);Check(text.All(t=>t.font!=null),"tablet fonts assigned");
                    Check(text.All(t=>t.font.name.Contains("3270")),"3270 used throughout tablet");
                    var filter=ui.GetComponent<HorrorGame.UI.TabletDisplayFilter>();
                    Check(filter.DisplayCamera.targetTexture!=null&&filter.Output.texture==filter.DisplayCamera.targetTexture,"whole interface filter is connected");
                    Check(filter.ScreenMaterial.shader.isSupported,"tablet screen shader is supported");
                    var raw=(RawImage)state.FindProperty("m_MapImage").objectReferenceValue;
                    Check(raw.texture!=null&&raw.gameObject.activeSelf,"native rendered map displayed");
                    float before=MapRenderer.Instance.GetComponentInChildren<Camera>().orthographicSize;
                    MapRenderer.Instance.Zoom(1);Check(MapRenderer.Instance.GetComponentInChildren<Camera>().orthographicSize<before,"native zoom retained");MapRenderer.Instance.Zoom(-1);
                    MapRenderer.Instance.GetComponentInChildren<Camera>().Render();Capture();
                    ui.ToggleMapDirectory();Check(!directory.gameObject.activeSelf,"directory toggles closed");
                    Check(raw.rectTransform.anchorMin.x==expanded,"map stays fixed when directory closes");
                    Capture("Tablet_MapExpanded.png");
                    typeof(UIMap).GetMethod("Hide",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,null);
                    Check(!PauseController.Instance.IsPaused,"closing map restores gameplay");
                    Check(filter.DisplayCamera.targetTexture==null,"closing releases screen target");
                    ui.Show(map);Check(!ui.GetComponent<HorrorGame.UI.TabletStartupScreen>().Screen.gameObject.activeSelf,"subsequent opening skips startup");
                    typeof(UIMap).GetMethod("Hide",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,null);
                    Finish(null);
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Capture(string name="Tablet_Map.png")
        {
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var data=new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            var features=Enumerable.Range(0,data.arraySize).SelectMany(i=>((ScriptableRendererData)data.GetArrayElementAtIndex(i).objectReferenceValue).rendererFeatures).Where(f=>f).Distinct().ToArray();
            var activeFeatures=features.Select(f=>f.isActive).ToArray();
            // A camera capture should match an overlay Canvas, which normally renders after world post effects.
            foreach(var feature in features)feature.SetActive(false);
            var filter=ui.GetComponent<HorrorGame.UI.TabletDisplayFilter>();
            var canvas=filter.Output.GetComponentInParent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            var cameraObject=new GameObject("Tablet review camera");var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(1600,900,24);camera.targetTexture=target;
            var previousSource=filter.DisplayCamera.targetTexture;
            var reviewSource=new RenderTexture(1600,900,24){filterMode=FilterMode.Point};
            filter.DisplayCamera.targetTexture=reviewSource;filter.Output.texture=reviewSource;
            var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
                Canvas.ForceUpdateCanvases();filter.DisplayCamera.Render();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory("Exports/Reviews/Tablet");File.WriteAllBytes("Exports/Reviews/Tablet/"+name,image.EncodeToPNG());
                Check(image.GetPixels32().Count(pixel=>pixel.r+pixel.g+pixel.b>100)>1000,"capture contains visible interface: "+name);
            }
            finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;RenderTexture.active=previous;filter.DisplayCamera.targetTexture=previousSource;filter.Output.texture=previousSource;reviewSource.Release();Object.DestroyImmediate(reviewSource);camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);for(int i=0;i<features.Length;i++)features[i].SetActive(activeFeatures[i]);}
        }
        static void Finish(string error)
        {SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;if(error!=null)Debug.LogError(error);EditorApplication.Exit(error==null?0:1);}
        static Transform Find(string name)=>ui.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    }
}
