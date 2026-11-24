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
        static readonly Vector2 MapMin = new(.025f, .087f), MapMax = new(.975f, .87f);
        public static void Apply(GameObject root)
        {
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TabletMapBuilder.Root+"Tablet_3270Font.asset");
            var display = InventoryFolderBuilder.MenuSurface(root);
            var surface = display.Find("Tablet window content") as RectTransform;
            if (!surface)
            {
                var children = display.Cast<Transform>().ToArray();
                surface = (RectTransform)Container("Tablet window content", display, Vector2.zero, Vector2.one).transform;
                foreach (var child in children) child.SetParent(surface, false);
            }
            var old=surface.Find("Tablet workspace");if(old)Object.DestroyImmediate(old.gameObject);
            var oldComponent=root.GetComponent<TabletWorkspace>();if(oldComponent)Object.DestroyImmediate(oldComponent);
            var owner=new GameObject("Tablet workspace",typeof(RectTransform));owner.transform.SetParent(surface,false);Rect((RectTransform)owner.transform,Vector2.zero,Vector2.one);
            var workspace=root.AddComponent<TabletWorkspace>();workspace.Map=root.GetComponent<UIMap>();workspace.WindowContent=surface;
            var data=new SerializedObject(workspace.Map);data.FindProperty("m_MapViewportLeft").floatValue=MapMin.x;data.ApplyModifiedPropertiesWithoutUndo();workspace.MapImage=(RawImage)data.FindProperty("m_MapImage").objectReferenceValue;
            workspace.MapFrame=((RectTransform)data.FindProperty("m_PanFrame").objectReferenceValue).gameObject;
            workspace.Floors=((Transform)data.FindProperty("m_FloorsParent").objectReferenceValue).gameObject;
            var floors = (RectTransform)workspace.Floors.transform;
            Rect(floors, new Vector2(.955f, .85f), new Vector2(.955f, .85f));
            floors.pivot = Vector2.one;
            Rect(workspace.MapImage.rectTransform, MapMin, MapMax);Rect((RectTransform)workspace.MapFrame.transform, MapMin, MapMax);
            var directory=(UIMapList)data.FindProperty("m_MapList").objectReferenceValue;workspace.Directory=directory.gameObject;Rect((RectTransform)directory.transform, MapMin, new Vector2(.35f, MapMax.y));
            var mapName=(TMP_Text)data.FindProperty("m_MapName").objectReferenceValue;mapName.gameObject.SetActive(false);
            var controls=surface.Find("Map controls");if(controls){workspace.Footer=controls.GetComponent<TMP_Text>();Rect((RectTransform)controls,new Vector2(.025f,.022f),new Vector2(.975f,.072f));workspace.Footer.fontSize=15;}
            var background=surface.Find("Bg");if(background){Rect((RectTransform)background,Vector2.zero,Vector2.one);background.GetComponent<Image>().color=new Color(.055f,.075f,.085f);}
            if (surface.Find("Map backdrop") is RectTransform backing) Rect(backing, MapMin, MapMax);
            var header = Container("Tablet commands", owner.transform, new Vector2(.025f,.891f), new Vector2(.975f,.982f));
            workspace.InventoryButton = Button("Return to inventory", "INVENTORY [Q]", header.transform,
                Vector2.zero, new Vector2(.217f, 1), workspace.Map.OpenInventory);
            workspace.InventoryButton.navigation = new Navigation { mode = Navigation.Mode.None };
            workspace.InventoryButton.targetGraphic.color = TabletWorkspace.CommandSurface;
            var annotation=Container("Map symbols",owner.transform,MapMin,MapMax);workspace.Annotations=annotation.AddComponent<TabletMapAnnotations>();workspace.Annotations.Workspace=workspace;workspace.Annotations.color=Ink;
            workspace.Legend=Container("Map legend",owner.transform,new Vector2(.655f,.20f),new Vector2(.925f,.745f));Panel("Legend backing",workspace.Legend.transform,Vector2.zero,Vector2.one,new Color(.018f,.05f,.065f,.98f)).raycastTarget=true;
            Label("Legend heading","MAP LEGEND",workspace.Legend.transform,new Vector2(.07f,.88f),new Vector2(.80f,.98f),21);
            Button("Close legend","X",workspace.Legend.transform,new Vector2(.85f,.90f),new Vector2(.97f,.98f),workspace.ToggleLegend);
            Panel("Legend divider",workspace.Legend.transform,new Vector2(.07f,.865f),new Vector2(.93f,.868f),Edge);
            string[] legendTitles={"Your position","Locked door","Unlocked door","Unexplored area","Circuit"};
            string[] legendDescriptions={"Filled direction arrow","Access is locked","Access is available","Known, not yet visited","Revealed by a room visit or diagram"};
            for(int i=0;i<5;i++)
            {
                float y=.705f-i*.14f;
                var legendRow=Container("Legend row "+i,workspace.Legend.transform,new Vector2(.05f,y),new Vector2(.95f,y+.13f));
                var icon=Container("Legend symbol",legendRow.transform,new Vector2(.01f,.20f),new Vector2(.15f,.80f)).AddComponent<TabletMapSymbol>();icon.Kind=(TabletSymbolKind)i;icon.raycastTarget=false;
                Label("Legend name",legendTitles[i],legendRow.transform,new Vector2(.21f,.51f),new Vector2(.98f,.98f),18);
                var detail=Label("Legend description",legendDescriptions[i],legendRow.transform,new Vector2(.21f,.03f),new Vector2(.98f,.51f),13);detail.color=new Color(.44f,.62f,.66f);detail.textWrappingMode=TextWrappingModes.Normal;
            }
            var legendFooter=Label("Legend footer","Only discovered features are marked.",workspace.Legend.transform,new Vector2(.07f,.025f),new Vector2(.93f,.11f),13);legendFooter.textWrappingMode=TextWrappingModes.Normal;
            workspace.PowerPage=Container("Power page",owner.transform,MapMin,new Vector2(MapMax.x,.81f));Panel("Power background",workspace.PowerPage.transform,Vector2.zero,Vector2.one,PanelColor);
            workspace.NetworkStatus=Label("Network load","NO POWER NETWORK",workspace.PowerPage.transform,new Vector2(.04f,.85f),new Vector2(.96f,.97f),22);
            workspace.PowerHelp=Label("Power guidance","",workspace.PowerPage.transform,new Vector2(.04f,.015f),new Vector2(.96f,.14f),16);workspace.PowerHelp.textWrappingMode=TextWrappingModes.Normal;
            var scroll=Scroll("Circuit list",workspace.PowerPage.transform,new Vector2(.035f,.16f),new Vector2(.965f,.83f));workspace.PowerRows=scroll.content;
            var layout=scroll.content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=6;
            var row=Button("Circuit row template","CIRCUIT",workspace.PowerPage.transform,Vector2.zero,Vector2.one,null);row.onClick.RemoveAllListeners();row.gameObject.AddComponent<LayoutElement>().preferredHeight=58;row.GetComponentInChildren<TMP_Text>().fontSize=17;row.GetComponentInChildren<TMP_Text>().textWrappingMode=TextWrappingModes.Normal;workspace.PowerRowTemplate=row;row.gameObject.SetActive(false);
            workspace.LogPage=Container("Log page",owner.transform,MapMin,new Vector2(MapMax.x,.81f));Panel("Log background",workspace.LogPage.transform,Vector2.zero,Vector2.one,PanelColor);
            var logScroll=Scroll("Field record",workspace.LogPage.transform,new Vector2(.04f,.17f),new Vector2(.96f,.96f));workspace.LogText=Label("Service log","",logScroll.content,Vector2.zero,Vector2.one,18);workspace.LogText.alignment=TextAlignmentOptions.TopLeft;workspace.LogText.textWrappingMode=TextWrappingModes.Normal;workspace.LogText.richText=false;
            var logRect=workspace.LogText.rectTransform;logRect.anchorMin=new Vector2(0,1);logRect.anchorMax=Vector2.one;logRect.pivot=new Vector2(.5f,1);logRect.offsetMin=logRect.offsetMax=Vector2.zero;workspace.LogText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;logScroll.content=logRect;
            BuildMapActions(workspace, owner.transform, surface);
            directory.transform.SetAsLastSibling();
            var startup=root.GetComponent<TabletStartupScreen>();if(startup && startup.Screen)startup.Screen.transform.SetAsLastSibling();
            foreach(var t in owner.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            InventoryFolderBuilder.AddReadability(root);
            var screen = AssetDatabase.LoadAssetAtPath<Material>(TabletMapBuilder.Root + "Tablet_Screen.mat");
            if (screen)
            {
                screen.SetFloat("_Curvature", .017f);
                screen.SetFloat("_DitherStrength", .12f);
                screen.SetFloat("_Scanlines", .045f);
                screen.SetFloat("_Vignette", .08f);
                EditorUtility.SetDirty(screen);
            }
        }
        internal static void BuildMapActions(TabletWorkspace workspace, Transform owner, Transform surface)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TabletMapBuilder.Root + "Tablet_3270Font.asset");
            // Power controls and the service log remain built as pages for later
            // use, but their navigation buttons are intentionally unavailable.
            workspace.BackToMap = Button("Back to map", "BACK TO MAP", owner,
                new Vector2(.025f, .82f), new Vector2(.23f, .87f), workspace.ShowMap);
            workspace.BackToMap.gameObject.SetActive(false);
            InventoryFolderBuilder.MarkTextSizes(surface);
        }

        static ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var view=Panel(name,parent,min,max,new Color(0,0,0,0));view.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            var content=Container("Content",view.transform,new Vector2(0,1),Vector2.one);var rect=(RectTransform)content.transform;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=Vector2.zero;content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=rect;return scroll;
        }
        static GameObject Container(string name,Transform parent,Vector2 min,Vector2 max){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Rect((RectTransform)go.transform,min,max);return go;}
        static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color){var go=Container(name,parent,min,max);var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static TextMeshProUGUI Label(string name,string value,Transform parent,Vector2 min,Vector2 max,float size){var go=Container(name,parent,min,max);var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.text=value;label.fontSize=size;label.color=Ink;label.alignment=TextAlignmentOptions.MidlineLeft;label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;return label;}
        static Button Button(string name,string value,Transform parent,Vector2 min,Vector2 max,UnityAction action){var panel=Panel(name,parent,min,max,Edge);panel.raycastTarget=true;var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel;if(action!=null)UnityEventTools.AddPersistentListener(button.onClick,action);var text=Label(name+" label",value,panel.transform,new Vector2(.03f,.02f),new Vector2(.97f,.98f),16);text.alignment=TextAlignmentOptions.Center;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,.95f,1);colors.selectedColor=colors.highlightedColor;button.colors=colors;return button;}
        static void Rect(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
    }
}
