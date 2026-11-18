using System.Linq;
using HorrorEngine;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace HorrorGame.UI.Editor
{
    public static class TabletWorkspaceBuilder
    {
        static TMP_FontAsset font;
        static readonly Color Ink=new Color(.65f,.84f,.88f),PanelColor=new Color(.025f,.065f,.085f),Edge=new Color(.14f,.23f,.26f);
        public static void Apply(GameObject root)
        {
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TabletMapBuilder.Root+"Tablet_3270Font.asset");
            var surface=InventoryFolderBuilder.MenuSurface(root);var old=surface.Find("Tablet workspace");if(old)Object.DestroyImmediate(old.gameObject);
            var oldComponent=root.GetComponent<TabletWorkspace>();if(oldComponent)Object.DestroyImmediate(oldComponent);
            var owner=new GameObject("Tablet workspace",typeof(RectTransform));owner.transform.SetParent(surface,false);Rect((RectTransform)owner.transform,Vector2.zero,Vector2.one);
            var workspace=root.AddComponent<TabletWorkspace>();workspace.Map=root.GetComponent<UIMap>();
            var data=new SerializedObject(workspace.Map);data.FindProperty("m_MapViewportLeft").floatValue=.075f;data.ApplyModifiedPropertiesWithoutUndo();workspace.MapImage=(RawImage)data.FindProperty("m_MapImage").objectReferenceValue;
            workspace.MapFrame=((RectTransform)data.FindProperty("m_PanFrame").objectReferenceValue).gameObject;
            workspace.Floors=((Transform)data.FindProperty("m_FloorsParent").objectReferenceValue).gameObject;
            Rect(workspace.MapImage.rectTransform,new Vector2(.075f,.095f),new Vector2(.93f,.77f));Rect((RectTransform)workspace.MapFrame.transform,new Vector2(.075f,.095f),new Vector2(.93f,.77f));
            var directory=(UIMapList)data.FindProperty("m_MapList").objectReferenceValue;workspace.Directory=directory.gameObject;Rect((RectTransform)directory.transform,new Vector2(.075f,.095f),new Vector2(.35f,.77f));
            var mapName=(TMP_Text)data.FindProperty("m_MapName").objectReferenceValue;Rect(mapName.rectTransform,new Vector2(.075f,.885f),new Vector2(.93f,.925f));mapName.fontSize=21;
            var nameScale=mapName.GetComponent<MenuTextScale>();if(nameScale)nameScale.BaseSize=21;
            var header=surface.Find("Device identification");if(header){Rect((RectTransform)header,new Vector2(.075f,.935f),new Vector2(.93f,.972f));header.GetComponent<TMP_Text>().text="HERO HQ / FIELD TERMINAL";}
            foreach(var t in surface.GetComponentsInChildren<Transform>(true))if(t.name=="Map scale label" || t.name=="Directory toggle" || t.name=="Display frame")t.gameObject.SetActive(false);
            var controls=surface.Find("Map controls");if(controls){workspace.Footer=controls.GetComponent<TMP_Text>();Rect((RectTransform)controls,new Vector2(.075f,.045f),new Vector2(.77f,.085f));controls.GetComponent<TMP_Text>().text="PAN WASD / ZOOM WHEEL / FLOOR UP-DOWN / CLOSE ESC";controls.GetComponent<TMP_Text>().fontSize=13;}
            var background=surface.Find("Bg");if(background){Rect((RectTransform)background,new Vector2(.045f,.035f),new Vector2(.96f,.987f));background.GetComponent<Image>().color=new Color(.012f,.026f,.035f);}
            Casing(owner.transform);
            workspace.MapTools=Container("Map tools",owner.transform,Vector2.zero,Vector2.one);
            Button("Map directory","DIRECTORY [I]",workspace.MapTools.transform,new Vector2(.54f,.835f),new Vector2(.72f,.88f),workspace.Map.ToggleMapDirectory);
            Button("Legend toggle","LEGEND",workspace.MapTools.transform,new Vector2(.73f,.835f),new Vector2(.93f,.88f),workspace.ToggleLegend);
            var annotation=Container("Map symbols",owner.transform,new Vector2(.075f,.095f),new Vector2(.93f,.77f));workspace.Annotations=annotation.AddComponent<TabletMapAnnotations>();workspace.Annotations.Workspace=workspace;workspace.Annotations.color=Ink;
            workspace.Legend=Container("Map legend",owner.transform,new Vector2(.655f,.20f),new Vector2(.925f,.745f));Panel("Legend backing",workspace.Legend.transform,Vector2.zero,Vector2.one,new Color(.018f,.05f,.065f,.98f)).raycastTarget=true;
            Label("Legend heading","MAP LEGEND",workspace.Legend.transform,new Vector2(.07f,.88f),new Vector2(.80f,.98f),21);
            Button("Close legend","X",workspace.Legend.transform,new Vector2(.85f,.90f),new Vector2(.97f,.98f),workspace.ToggleLegend);
            Panel("Legend divider",workspace.Legend.transform,new Vector2(.07f,.865f),new Vector2(.93f,.868f),Edge);
            string[] legendTitles={"Your position","Locked door","Unlocked door","Unexplored area","Power point"};
            string[] legendDescriptions={"Filled direction arrow","Access is locked","Access is available","Known, not yet visited","Circuit diagram revealed"};
            for(int i=0;i<5;i++)
            {
                float y=.705f-i*.14f;
                var legendRow=Container("Legend row "+i,workspace.Legend.transform,new Vector2(.05f,y),new Vector2(.95f,y+.13f));
                var icon=Container("Legend symbol",legendRow.transform,new Vector2(.01f,.20f),new Vector2(.15f,.80f)).AddComponent<TabletMapSymbol>();icon.Kind=(TabletSymbolKind)i;icon.raycastTarget=false;
                Label("Legend name",legendTitles[i],legendRow.transform,new Vector2(.21f,.51f),new Vector2(.98f,.98f),18);
                var detail=Label("Legend description",legendDescriptions[i],legendRow.transform,new Vector2(.21f,.03f),new Vector2(.98f,.51f),13);detail.color=new Color(.44f,.62f,.66f);detail.enableWordWrapping=true;
            }
            var legendFooter=Label("Legend footer","Only discovered features are marked.",workspace.Legend.transform,new Vector2(.07f,.025f),new Vector2(.93f,.11f),13);legendFooter.enableWordWrapping=true;
            workspace.PowerPage=Container("Power page",owner.transform,new Vector2(.075f,.095f),new Vector2(.93f,.77f));Panel("Power background",workspace.PowerPage.transform,Vector2.zero,Vector2.one,PanelColor);
            workspace.NetworkStatus=Label("Network load","NO POWER NETWORK",workspace.PowerPage.transform,new Vector2(.04f,.85f),new Vector2(.96f,.97f),22);
            workspace.PowerHelp=Label("Power guidance","",workspace.PowerPage.transform,new Vector2(.04f,.015f),new Vector2(.96f,.14f),16);workspace.PowerHelp.enableWordWrapping=true;
            var scroll=Scroll("Circuit list",workspace.PowerPage.transform,new Vector2(.035f,.16f),new Vector2(.965f,.83f));workspace.PowerRows=scroll.content;
            var layout=scroll.content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=6;
            var row=Button("Circuit row template","CIRCUIT",workspace.PowerPage.transform,Vector2.zero,Vector2.one,null);row.onClick.RemoveAllListeners();row.gameObject.AddComponent<LayoutElement>().preferredHeight=58;row.GetComponentInChildren<TMP_Text>().fontSize=17;row.GetComponentInChildren<TMP_Text>().enableWordWrapping=true;workspace.PowerRowTemplate=row;row.gameObject.SetActive(false);
            workspace.LogPage=Container("Log page",owner.transform,new Vector2(.075f,.095f),new Vector2(.93f,.77f));Panel("Log background",workspace.LogPage.transform,Vector2.zero,Vector2.one,PanelColor);
            var logScroll=Scroll("Field record",workspace.LogPage.transform,new Vector2(.04f,.17f),new Vector2(.96f,.96f));workspace.LogText=Label("Service log","",logScroll.content,Vector2.zero,Vector2.one,18);workspace.LogText.alignment=TextAlignmentOptions.TopLeft;workspace.LogText.enableWordWrapping=true;workspace.LogText.richText=false;
            var logRect=workspace.LogText.rectTransform;logRect.anchorMin=new Vector2(0,1);logRect.anchorMax=Vector2.one;logRect.pivot=new Vector2(.5f,1);logRect.offsetMin=logRect.offsetMax=Vector2.zero;workspace.LogText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;logScroll.content=logRect;
            BuildMapActions(workspace, owner.transform, surface);
            directory.transform.SetAsLastSibling();
            var startup=root.GetComponent<TabletStartupScreen>();if(startup && startup.Screen)startup.Screen.transform.SetAsLastSibling();
            foreach(var t in owner.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            InventoryFolderBuilder.AddReadability(root);
            var screen=AssetDatabase.LoadAssetAtPath<Material>(TabletMapBuilder.Root+"Tablet_Screen.mat");if(screen){screen.SetVector("_VirtualResolution",new Vector4(1280,720,0,0));screen.SetFloat("_ColorDepth",64);screen.SetFloat("_DitherStrength",.1f);screen.SetFloat("_Scanlines",.035f);EditorUtility.SetDirty(screen);}
        }
        internal static void BuildMapActions(TabletWorkspace workspace, Transform owner, Transform surface)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TabletMapBuilder.Root + "Tablet_3270Font.asset");
            Button("Open power controls", "POWER CONTROLS", workspace.MapTools.transform,
                new Vector2(.075f, .835f), new Vector2(.275f, .88f), workspace.ShowPower);
            Button("Open service log", "SERVICE LOG", workspace.MapTools.transform,
                new Vector2(.285f, .835f), new Vector2(.485f, .88f), workspace.ShowLog);
            workspace.BackToMap = Button("Back to map", "BACK TO MAP", owner,
                new Vector2(.075f, .835f), new Vector2(.275f, .88f), workspace.ShowMap);
            workspace.BackToMap.gameObject.SetActive(false);
            foreach (var view in new[] { workspace.MapImage.gameObject, workspace.MapFrame, workspace.Directory,
                         workspace.Annotations.gameObject, workspace.PowerPage, workspace.LogPage })
            {
                var rect = (RectTransform)view.transform;
                rect.anchorMax = new Vector2(rect.anchorMax.x, .82f);
            }
            InventoryFolderBuilder.MarkTextSizes(surface);
        }

        static void Casing(Transform parent)
        {
            var metal=new Color(.10f,.105f,.105f);
            Panel("Left bezel",parent,new Vector2(.015f,.015f),new Vector2(.055f,.995f),metal);Panel("Right bezel",parent,new Vector2(.95f,.015f),new Vector2(.985f,.995f),metal);Panel("Upper bezel",parent,new Vector2(.015f,.975f),new Vector2(.985f,.995f),metal);Panel("Lower bezel",parent,new Vector2(.015f,.015f),new Vector2(.985f,.04f),metal);
            foreach(float x in new[]{.035f,.965f})foreach(float y in new[]{.027f,.985f}){Panel("Recessed screw",parent,new Vector2(x-.004f,y-.004f),new Vector2(x+.004f,y+.004f),new Color(.025f,.028f,.028f));Panel("Screw slot",parent,new Vector2(x-.0025f,y-.0006f),new Vector2(x+.0025f,y+.0006f),new Color(.23f,.24f,.23f));}
            for(int i=0;i<8;i++){float x=.06f+i*.115f;Panel("Bezel wear",parent,new Vector2(x,.983f),new Vector2(x+.011f,.984f),new Color(.25f,.255f,.24f,.32f));}
        }
        static ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var view=Panel(name,parent,min,max,new Color(0,0,0,0));view.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            var content=Container("Content",view.transform,new Vector2(0,1),Vector2.one);var rect=(RectTransform)content.transform;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=Vector2.zero;content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=rect;return scroll;
        }
        static GameObject Container(string name,Transform parent,Vector2 min,Vector2 max){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Rect((RectTransform)go.transform,min,max);return go;}
        static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color){var go=Container(name,parent,min,max);var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static TextMeshProUGUI Label(string name,string value,Transform parent,Vector2 min,Vector2 max,float size){var go=Container(name,parent,min,max);var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.text=value;label.fontSize=size;label.color=Ink;label.alignment=TextAlignmentOptions.MidlineLeft;label.enableWordWrapping=false;label.raycastTarget=false;return label;}
        static Button Button(string name,string value,Transform parent,Vector2 min,Vector2 max,UnityAction action){var panel=Panel(name,parent,min,max,Edge);panel.raycastTarget=true;var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel;if(action!=null)UnityEventTools.AddPersistentListener(button.onClick,action);var text=Label(name+" label",value,panel.transform,new Vector2(.03f,.02f),new Vector2(.97f,.98f),16);text.alignment=TextAlignmentOptions.Center;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,.95f,1);colors.selectedColor=colors.highlightedColor;button.colors=colors;return button;}
        static void Rect(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
    }
}
