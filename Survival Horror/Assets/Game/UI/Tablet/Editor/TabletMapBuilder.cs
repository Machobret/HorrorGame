using System;
using System.IO;
using System.Linq;
using HorrorEngine;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.UI.Editor
{
    public static class TabletMapBuilder
    {
        public const string Root = "Assets/Game/UI/Tablet/";
        const string Vendor = "Assets/AssetStore/HorrorEngine/Prefabs/";
        static readonly Color Ink = new Color(.57f,.78f,.83f);
        static readonly Color Edge = new Color(.17f,.31f,.37f);
        static TMP_FontAsset font;

        [MenuItem("Horror Game/UI/Build Utilitarian Tablet Map")]
        public static void Build()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            font = FontAsset();
            var map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"UI/UIMap.prefab"));
            PrefabUtility.UnpackPrefabInstance(map,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            GameObject mapPrefab;
            try
            {
                var ui = new SerializedObject(map.GetComponent<UIMap>());
                ui.FindProperty("m_SidebarDirectory").boolValue = true;
                ui.FindProperty("m_CrosshairColor").colorValue = new Color(.48f,.73f,.8f,.2f);
                ui.FindProperty("m_CurrentFloorColor").colorValue = Ink;
                ui.FindProperty("m_FloorsPrefab").objectReferenceValue=ListEntry((GameObject)ui.FindProperty("m_FloorsPrefab").objectReferenceValue,"Tablet_FloorEntry");
                ui.ApplyModifiedPropertiesWithoutUndo();
                var scaler=map.GetComponent<CanvasScaler>();scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
                map.transform.Find("NS").gameObject.SetActive(false);
                foreach(var text in map.GetComponentsInChildren<TMP_Text>(true)) Style(text);
                foreach(string path in new[]{"ButtonLegendL","ButtonLegendR"}) map.transform.Find(path).gameObject.SetActive(false);
                var floors=(RectTransform)map.transform.Find("Floors");floors.anchorMin=floors.anchorMax=new Vector2(.92f,.92f);floors.anchoredPosition=Vector2.zero;
                var oldBackground = map.transform.Find("Bg").GetComponent<Image>();
                oldBackground.sprite = null; oldBackground.color = new Color(.025f,.035f,.045f);
                oldBackground.raycastTarget = false;
                var oldFrame = map.transform.Find("Panel").GetComponent<Image>();
                if(oldFrame) oldFrame.enabled = false;
                var raw = (RawImage)ui.FindProperty("m_MapImage").objectReferenceValue;
                Rect(raw.rectTransform,new Vector2(.065f,.14f),new Vector2(.935f,.82f));
                var panFrame=(RectTransform)map.transform.Find("Panel");Rect(panFrame,new Vector2(.065f,.14f),new Vector2(.935f,.82f));
                ui.FindProperty("m_PanFrame").objectReferenceValue=panFrame;ui.ApplyModifiedPropertiesWithoutUndo();
                raw.raycastTarget = false;
                raw.color = Color.white;
                foreach(string name in new[]{"ArrowL","ArrowR","ArrowT","ArrowD"})
                {
                    var arrow = map.transform.Find("Panel/"+name).GetComponent<Image>();
                    arrow.color = new Color(1,.79f,.23f,.42f); arrow.raycastTarget = false;
                    var r=arrow.rectTransform;
                    Vector2 anchor = name=="ArrowL" ? new Vector2(0,.5f) : name=="ArrowR" ? new Vector2(1,.5f) : name=="ArrowT" ? new Vector2(.5f,1) : new Vector2(.5f,0);
                    Rect(r,anchor,anchor);r.anchoredPosition=name=="ArrowL"?new Vector2(16,0):name=="ArrowR"?new Vector2(-16,0):name=="ArrowT"?new Vector2(0,-16):new Vector2(0,16);
                    r.sizeDelta=new Vector2(30,18);r.localScale=Vector3.one;
                }
                var list = (UIMapList)ui.FindProperty("m_MapList").objectReferenceValue;
                Rect((RectTransform)list.transform,new Vector2(.065f,.14f),new Vector2(.25f,.82f));
                list.GetComponent<Image>().color = new Color(.045f,.09f,.115f);
                var listTitle=list.transform.Find("Text (TMP)").GetComponent<TMP_Text>();listTitle.text="MAP DIRECTORY";listTitle.fontSize=21;
                var content=(RectTransform)list.transform.Find("ContainerScrollingList/Content");
                content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
                var layout=content.GetComponent<VerticalLayoutGroup>();layout.childForceExpandHeight=false;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.spacing=6;
                var fitter=content.GetComponent<ContentSizeFitter>();if(!fitter)fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                foreach(var graphic in listTitle.GetComponentsInChildren<Image>(true))graphic.color=Edge;
                foreach(var graphic in list.transform.Find("ContainerScrollingList/Scrollbar").GetComponentsInChildren<Image>(true))graphic.color=new Color(.24f,.43f,.5f,.65f);
                var scrollBar=(RectTransform)list.transform.Find("ContainerScrollingList/Scrollbar");scrollBar.sizeDelta=new Vector2(7,0);
                var listData=new SerializedObject(list);
                listData.FindProperty("m_MapListItemPrefab").objectReferenceValue=ListEntry((GameObject)listData.FindProperty("m_MapListItemPrefab").objectReferenceValue);
                listData.ApplyModifiedPropertiesWithoutUndo();
                var mapName=(TMP_Text)ui.FindProperty("m_MapName").objectReferenceValue;
                Rect(mapName.rectTransform,new Vector2(.275f,.83f),new Vector2(.93f,.875f));mapName.alignment=TextAlignmentOptions.MidlineLeft;mapName.fontSize=26;
                var trim=Panel("Tablet housing",map.transform,new Vector2(.035f,.045f),new Vector2(.965f,.955f),new Color(.07f,.085f,.095f));trim.transform.SetSiblingIndex(1);
                var screen=Panel("Blue display",map.transform,new Vector2(.055f,.12f),new Vector2(.945f,.89f),new Color(.018f,.055f,.08f));screen.transform.SetSiblingIndex(2);
                // Thin screen-printed grid; geometry is kept behind the map and its controls.
                for(int i=1;i<24;i++)
                {
                    float x=.055f+.89f*i/24;
                    var line=Panel("Schematic grid",map.transform,new Vector2(x,.12f),new Vector2(x,.89f),new Color(.23f,.4f,.48f,.08f));
                    ((RectTransform)line.transform).sizeDelta=new Vector2(1,0);line.transform.SetSiblingIndex(3);
                }
                for(int i=1;i<16;i++)
                {
                    float y=.12f+.77f*i/16;
                    var line=Panel("Schematic grid",map.transform,new Vector2(.055f,y),new Vector2(.945f,y),new Color(.23f,.4f,.48f,.08f));
                    ((RectTransform)line.transform).sizeDelta=new Vector2(0,1);line.transform.SetSiblingIndex(3);
                }
                Border(map.transform,.055f,.12f,.945f,.89f);
                Label("Device identification","HERO HQ  /  FIELD SERVICE TERMINAL",map.transform,new Vector2(.065f,.9f),new Vector2(.85f,.94f),22);
                Label("Map controls","WASD PAN  /  WHEEL ZOOM  /  UP-DOWN FLOOR  /  M-ESC CLOSE",map.transform,new Vector2(.28f,.065f),new Vector2(.935f,.105f),14);
                var directoryButton=Panel("Directory toggle",map.transform,new Vector2(.065f,.065f),new Vector2(.25f,.105f),new Color(.12f,.22f,.27f));directoryButton.raycastTarget=true;
                var toggle=directoryButton.gameObject.AddComponent<Button>();toggle.targetGraphic=directoryButton;UnityEditor.Events.UnityEventTools.AddPersistentListener(toggle.onClick,map.GetComponent<UIMap>().ToggleMapDirectory);
                Label("Directory toggle label","[ I ]  MAP DIRECTORY",directoryButton.transform,Vector2.zero,Vector2.one,15);
                Label("Map scale label","LOCAL PLAN  /  NOT TO SCALE",map.transform,new Vector2(.28f,.13f),new Vector2(.8f,.16f),14);
                foreach(var pos in new[]{new Vector2(.045f,.06f),new Vector2(.955f,.06f),new Vector2(.045f,.94f),new Vector2(.955f,.94f)})
                {
                    var screw=Panel("Housing fastener",map.transform,pos,pos,new Color(.22f,.25f,.26f));((RectTransform)screw.transform).sizeDelta=new Vector2(7,7);
                }
                list.transform.SetAsLastSibling();
                Startup(map);
                foreach(var text in map.GetComponentsInChildren<TMP_Text>(true))text.fontSize*=1.25f;
                Filter(map);
                foreach(var component in map.GetComponentsInChildren<Component>(true)) if(component) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                TabletWorkspaceBuilder.Apply(map);
                mapPrefab=PrefabUtility.SaveAsPrefabAsset(map,Root+"Tablet_Map.prefab");
            }
            finally { Object.DestroyImmediate(map); }
            var renderer=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"Mapping/MapRenderer.prefab"));
            GameObject rendererPrefab;
            try
            {
                var data=renderer.GetComponent<MapRenderer>();var serialized=new SerializedObject(data);
                serialized.FindProperty("m_GridColor").colorValue=new Color(.18f,.36f,.44f,.13f);
                serialized.FindProperty("m_ContourColor").colorValue=new Color(.42f,.7f,.79f);
                serialized.FindProperty("m_PlayerColor").colorValue=new Color(.85f,.96f,1);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                data.RoomNotVisitedSP=Process(data.RoomNotVisitedSP,"RoomsUnvisited",new Color(.04f,.10f,.14f));
                data.RoomIncompletedSP=Process(data.RoomIncompletedSP,"RoomsVisited",new Color(.08f,.22f,.29f));
                data.RoomCompletedSP=Process(data.RoomCompletedSP,"RoomsComplete",new Color(.13f,.31f,.37f));
                renderer.GetComponentInChildren<Camera>(true).backgroundColor=new Color(.018f,.055f,.08f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.GetComponentInChildren<Camera>(true));
                rendererPrefab=PrefabUtility.SaveAsPrefabAsset(renderer,Root+"Tablet_MapRenderer.prefab");
            }
            finally { Object.DestroyImmediate(renderer); }
            var core=new SerializedObject(AssetDatabase.LoadAssetAtPath<HECorePrefabs>(Vendor+"HECorePrefabs.asset"));
            core.FindProperty("m_Map").objectReferenceValue=mapPrefab;core.FindProperty("m_MapRenderer").objectReferenceValue=rendererPrefab;
            core.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            Debug.Log("TABLET_MAP_BUILD_OK");
        }
        public static void BuildBatch(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Filter(GameObject map)
        {
            var children=map.transform.Cast<Transform>().ToArray();
            Object.DestroyImmediate(map.GetComponent<GraphicRaycaster>());
            Object.DestroyImmediate(map.GetComponent<CanvasScaler>());
            Object.DestroyImmediate(map.GetComponent<Canvas>());
            // The source overlay Canvas stores a zero scale; it is no longer a Canvas root.
            map.transform.localScale=Vector3.one;map.transform.localPosition=Vector3.zero;
            var display=new GameObject("Tablet display",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));display.layer=map.layer;display.transform.SetParent(map.transform,false);
            Rect((RectTransform)display.transform,Vector2.zero,Vector2.one);
            foreach(var child in children)child.SetParent(display.transform,false);
            var cameraObject=new GameObject("Tablet screen camera",typeof(Camera));cameraObject.transform.SetParent(map.transform,false);
            var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<map.layer;camera.depth=50;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.allowHDR=false;camera.allowMSAA=false;
            cameraObject.transform.position=new Vector3(0,0,-50);
            var renderData=AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/AssetStore/HorrorEngine/Rendering/HEUniversalPipelineAsset_MapRenderer.asset");
            var pipeline=new SerializedObject(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
            var rendererList=pipeline.FindProperty("m_RendererDataList");
            int rendererIndex=Enumerable.Range(0,rendererList.arraySize).First(i=>rendererList.GetArrayElementAtIndex(i).objectReferenceValue==renderData);
            camera.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var canvas=display.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var scaler=display.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var outputCanvas=new GameObject("Tablet output",typeof(RectTransform),typeof(Canvas));outputCanvas.layer=map.layer;outputCanvas.transform.SetParent(map.transform,false);outputCanvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;outputCanvas.GetComponent<Canvas>().sortingOrder=1;
            var outputObject=new GameObject("Filtered tablet screen",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));outputObject.layer=map.layer;outputObject.transform.SetParent(outputCanvas.transform,false);
            var output=outputObject.GetComponent<RawImage>();Rect(output.rectTransform,Vector2.zero,Vector2.one);output.raycastTarget=false;
            string path=Root+"Tablet_Screen.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("HorrorGame/UI/Tablet Screen"));AssetDatabase.CreateAsset(material,path);}
            var filter=map.AddComponent<TabletDisplayFilter>();filter.DisplayCamera=camera;filter.Output=output;filter.ScreenMaterial=material;
        }
        static void Startup(GameObject map)
        {
            var cover=Panel("Hero agency startup",map.transform,new Vector2(.055f,.12f),new Vector2(.945f,.89f),new Color(.018f,.055f,.08f));cover.raycastTarget=true;
            var screen=cover.gameObject.AddComponent<CanvasGroup>();
            var badge=new GameObject("Hero response badge",typeof(RectTransform));badge.transform.SetParent(cover.transform,false);
            badge.layer=map.layer;
            var graphic=badge.AddComponent<TabletHeroBadge>();graphic.color=Ink;graphic.raycastTarget=false;
            Rect(graphic.rectTransform,new Vector2(.43f,.49f),new Vector2(.57f,.82f));
            Label("Agency","HERO RESPONSE AUTHORITY",cover.transform,new Vector2(.2f,.37f),new Vector2(.8f,.46f),27);
            Label("Unit","CIVIL DEFENCE  /  FIELD SERVICE SYSTEM",cover.transform,new Vector2(.2f,.30f),new Vector2(.8f,.36f),16);
            var progressBack=Panel("Startup progress track",cover.transform,new Vector2(.2f,.21f),new Vector2(.8f,.225f),Edge);
            var progress=Panel("Startup progress",progressBack.transform,Vector2.zero,Vector2.one,Ink);
            Label("Startup status","CHECKING FIELD HARDWARE...",cover.transform,new Vector2(.2f,.14f),new Vector2(.8f,.20f),15);
            var status=cover.transform.Find("Startup status").GetComponent<TMP_Text>();
            Label("Firmware","HRA / SERVICE ROM 1.04     OFFLINE ARCHIVE",cover.transform,new Vector2(.2f,.06f),new Vector2(.8f,.12f),12);
            foreach(var text in cover.GetComponentsInChildren<TMP_Text>())text.alignment=TextAlignmentOptions.Center;
            var startup=map.AddComponent<TabletStartupScreen>();startup.Screen=screen;startup.Progress=progress;startup.Status=status;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(map.GetComponent<UIMap>().OnShown,startup.Present);
            cover.gameObject.SetActive(false);
        }
        static TMP_FontAsset FontAsset()
        {
            var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"Tablet_3270Font.asset");if(asset)return asset;
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/3270Condensed-Regular.ttf");
            asset=TMP_FontAsset.CreateFontAsset(source);asset.name="3270 condensed terminal";
            AssetDatabase.CreateAsset(asset,Root+"Tablet_3270Font.asset");
            AssetDatabase.AddObjectToAsset(asset.material,asset);foreach(var texture in asset.atlasTextures)AssetDatabase.AddObjectToAsset(texture,asset);
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:-_.,?<>[]()+%=\\");
            EditorUtility.SetDirty(asset);return asset;
        }
        static void Style(TMP_Text text){text.font=font;text.color=Ink;text.enableWordWrapping=false;text.fontStyle=FontStyles.Normal;}
        static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
        static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();Rect(image.rectTransform,min,max);image.color=color;image.raycastTarget=false;return image;}
        static void Label(string name,string value,Transform parent,Vector2 min,Vector2 max,float size)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);var text=go.AddComponent<TextMeshProUGUI>();Rect(text.rectTransform,min,max);Style(text);text.text=value;text.fontSize=size;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;}
        static void Border(Transform parent,float left,float bottom,float right,float top)
        {
            foreach(float x in new[]{left,right}){var line=Panel("Display frame",parent,new Vector2(x,bottom),new Vector2(x,top),Edge);line.rectTransform.sizeDelta=new Vector2(2,0);}
            foreach(float y in new[]{bottom,top}){var line=Panel("Display frame",parent,new Vector2(left,y),new Vector2(right,y),Edge);line.rectTransform.sizeDelta=new Vector2(0,2);}
        }
        static GameObject ListEntry(GameObject original,string name="Tablet_MapListEntry")
        {
            var entry=(GameObject)PrefabUtility.InstantiatePrefab(original);
            try{foreach(var text in entry.GetComponentsInChildren<TMP_Text>(true)){Style(text);text.fontSize=22.5f;text.overflowMode=TextOverflowModes.Ellipsis;}
                var row=entry.GetComponent<LayoutElement>();if(!row)row=entry.AddComponent<LayoutElement>();row.preferredHeight=42;
                foreach(var button in entry.GetComponentsInChildren<Button>(true)){var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,.95f,1);colors.selectedColor=new Color(.8f,.95f,1);colors.disabledColor=new Color(.35f,.45f,.5f);button.colors=colors;}
                foreach(var component in entry.GetComponentsInChildren<Component>(true))if(component)PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                return PrefabUtility.SaveAsPrefabAsset(entry,Root+name+".prefab");}
            finally{Object.DestroyImmediate(entry);}
        }
        static ShapeCreationProcess Process(ShapeCreationProcess original,string name,Color color)
        {
            string path=Root+"Tablet_"+name+".asset";
            var process=AssetDatabase.LoadAssetAtPath<ShapeCreationProcess>(path);
            if(!process){process=Object.Instantiate(original);AssetDatabase.CreateAsset(process,path);}
            string materialPath=Root+"Tablet_"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}material.color=color;EditorUtility.SetDirty(material);
            var data=new SerializedObject(process);data.FindProperty("Material").objectReferenceValue=material;data.ApplyModifiedPropertiesWithoutUndo();return process;
        }
    }
}
