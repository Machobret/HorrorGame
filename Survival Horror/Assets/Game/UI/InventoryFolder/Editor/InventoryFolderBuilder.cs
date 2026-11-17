using System;
using System.IO;
using System.Linq;
using HorrorEngine;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace HorrorGame.UI.Editor
{
    public static partial class InventoryFolderBuilder
    {
        public const string Root="Assets/Game/UI/InventoryFolder/";
        static readonly Color Ink=new Color(.16f,.155f,.145f);
        static readonly Color Cream=new Color(.79f,.78f,.74f);
        static TMP_FontAsset paperFont,terminalFont;
        static Material iconPalette;
        static InventoryIconProfile iconProfile;
        static Material Palette(string name,float saturation,Color tint)
        {
            string path=Root+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("HorrorGame/UI/Inventory Palette"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Saturation",saturation);material.SetColor("_Tint",tint);EditorUtility.SetDirty(material);return material;
        }
        [MenuItem("Horror Game/UI/Build Folder Inventory")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            paperFont=FontAsset();terminalFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/Tablet/Tablet_3270Font.asset");
            foreach(string path in new[]{"Assets/Game/Weapons/HuntingRifle/Data/HuntingRifle.asset","Assets/Game/Weapons/MP5/Data/MP5.asset","Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset"}){var item=AssetDatabase.LoadAssetAtPath<ItemData>(path);if(item && item.InventorySlots!=2){item.InventorySlots=2;EditorUtility.SetDirty(item);}}
            iconPalette=Palette("Inventory_ItemPalette",.45f,new Color(.94f,.93f,.90f));
            var paperPalette=Palette("Inventory_PaperPalette",0,new Color(.83f,.80f,.74f));
            paperPalette.SetFloat("_Contrast",.65f);iconProfile=BuildIconProfile();
            var folderSprite=Sprite("Inventory_ManilaFolder");var pageSprite=Sprite("Inventory_TornNotebook");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/UI/UIInventory.prefab"));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            try
            {
                root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);
                var ui=new SerializedObject(root.GetComponent<UIInventory>());
                SetMenuAudio(ui);
                var content=(GameObject)ui.FindProperty("m_Content").objectReferenceValue;
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))Style(text,paperFont,Ink,20);
                var previousBackground=content.transform.Find("Bg");if(previousBackground)previousBackground.gameObject.SetActive(false);
                var fogObject=new GameObject("Animated white fog",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));fogObject.layer=5;fogObject.transform.SetParent(root.transform,false);fogObject.transform.SetAsFirstSibling();
                var fog=fogObject.GetComponent<RawImage>();Rect(fog.rectTransform,Vector2.zero,Vector2.one);fog.raycastTarget=false;
                string fogPath=Root+"Inventory_Fog.mat";var fogMaterial=AssetDatabase.LoadAssetAtPath<Material>(fogPath);
                if(!fogMaterial){fogMaterial=new Material(Shader.Find("HorrorGame/UI/Inventory Fog"));AssetDatabase.CreateAsset(fogMaterial,fogPath);}fogMaterial.SetFloat("_Opacity",.32f);EditorUtility.SetDirty(fogMaterial);fog.material=fogMaterial;
                var folder=Panel("Manila folder",content.transform,new Vector2(.045f,.16f),new Vector2(.65f,.9f),Color.white,folderSprite);folder.material=paperPalette;folder.transform.SetAsFirstSibling();
                var notebook=Panel("Torn notebook page",content.transform,new Vector2(.665f,.12f),new Vector2(.965f,.91f),new Color(.99f,.99f,.98f),pageSprite);notebook.material=paperPalette;
                var presentation=root.AddComponent<InventoryFolderPresentation>();presentation.Fog=fog;presentation.FogMaterial=fogMaterial;presentation.Content=content.AddComponent<CanvasGroup>();presentation.Notebook=notebook.rectTransform;presentation.InventoryUI=root.GetComponent<UIInventory>();
                var grid=root.GetComponentsInChildren<GridLayoutGroup>(true).Single();grid.transform.SetParent(folder.transform,false);
                Rect((RectTransform)grid.transform,new Vector2(.075f,.35f),new Vector2(.925f,.78f));grid.cellSize=new Vector2(112,100);grid.spacing=new Vector2(12,14);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;grid.childAlignment=TextAnchor.MiddleCenter;grid.enabled=false;
                var slots=ui.FindProperty("m_ItemSlots");for(int i=0;i<slots.arraySize;i++)StyleSlot((UIInventoryItem)slots.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach(var fitter in grid.GetComponents<ContentSizeFitter>())fitter.enabled=false;
                MarkTextSizes(((UIInventoryItem)slots.GetArrayElementAtIndex(0).objectReferenceValue).transform);
                ((UIInventoryItem)slots.GetArrayElementAtIndex(0).objectReferenceValue).GetComponent<InventoryItemAnnotation>().InventoryUI=null;
                var templateSlot=((UIInventoryItem)slots.GetArrayElementAtIndex(0).objectReferenceValue).gameObject;if(!templateSlot.GetComponent<PooledGameObject>())templateSlot.AddComponent<PooledGameObject>();
                var slotTemplate=PrefabUtility.SaveAsPrefabAsset(((UIInventoryItem)slots.GetArrayElementAtIndex(0).objectReferenceValue).gameObject,Root+"Inventory_ItemSlot.prefab");
                ui.FindProperty("m_AlwaysRebuild").boolValue=true;ui.FindProperty("m_RebuildOnlyOnCapacityChange").boolValue=true;ui.FindProperty("m_RebuildItemParent").objectReferenceValue=grid.transform;ui.FindProperty("m_RebuildItemPrefab").objectReferenceValue=slotTemplate;ui.ApplyModifiedPropertiesWithoutUndo();
                var layoutHost=grid.gameObject;Object.DestroyImmediate(grid);layoutHost.AddComponent<InventoryFootprintLayout>();
                var info=(UIItemFiller)ui.FindProperty("m_ItemInfoFiller").objectReferenceValue;info.transform.SetParent(notebook.transform,false);Rect((RectTransform)info.transform,Vector2.zero,Vector2.one);
                foreach(var layout in info.GetComponents<LayoutGroup>())layout.enabled=false;
                foreach(var fitter in info.GetComponentsInChildren<ContentSizeFitter>(true))fitter.enabled=false;
                Rect(info.m_ItemName.rectTransform,new Vector2(.16f,.86f),new Vector2(.93f,.94f));Style(info.m_ItemName,paperFont,Ink,24);
                Rect(info.m_ItemDescription.rectTransform,new Vector2(.16f,.34f),new Vector2(.93f,.60f));Style(info.m_ItemDescription,paperFont,Ink,19);info.m_ItemDescription.enableWordWrapping=true;info.m_ItemDescription.alignment=TextAlignmentOptions.TopLeft;
                var viewport=Panel("Description viewport",info.transform,new Vector2(.16f,.34f),new Vector2(.93f,.60f),new Color(0,0,0,0));viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=18;
                info.m_ItemDescription.transform.SetParent(viewport.transform,false);var descriptionRect=info.m_ItemDescription.rectTransform;descriptionRect.anchorMin=new Vector2(0,1);descriptionRect.anchorMax=Vector2.one;descriptionRect.pivot=new Vector2(.5f,1);descriptionRect.offsetMin=descriptionRect.offsetMax=Vector2.zero;
                var descriptionFitter=info.m_ItemDescription.GetComponent<ContentSizeFitter>();if(!descriptionFitter)descriptionFitter=info.m_ItemDescription.gameObject.AddComponent<ContentSizeFitter>();descriptionFitter.enabled=true;descriptionFitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=descriptionRect;presentation.DescriptionScroll=scroll;viewport.raycastTarget=true;
                var preview=Panel("Selected item preview",info.transform,new Vector2(.18f,.66f),new Vector2(.93f,.84f),Color.white);preview.preserveAspect=true;preview.material=iconPalette;preview.gameObject.AddComponent<InventoryIconFit>().Profile=iconProfile;
                var count=Label("Selected item quantity","",info.transform,new Vector2(.16f,.61f),new Vector2(.93f,.65f),18,Ink,paperFont);count.alignment=TextAlignmentOptions.MidlineLeft;
                var details=info.gameObject.AddComponent<InventoryItemAnnotation>();details.InventoryUI=root.GetComponent<UIInventory>();details.Quantity=count;details.Profile=iconProfile;
                presentation.FeedbackText=Label("Action feedback","",notebook.transform,new Vector2(.16f,.305f),new Vector2(.93f,.338f),14,Ink,terminalFont);
                UnityEventTools.AddPersistentListener(root.GetComponent<UIInventory>().OnFeedback,presentation.ShowFeedback);
                var filler=new SerializedObject(info);filler.FindProperty("m_Icon").objectReferenceValue=preview;filler.FindProperty("m_ItemCount").objectReferenceValue=count;filler.FindProperty("m_NoItemName.IsLocalized").boolValue=false;filler.FindProperty("m_NoItemName.Unlocalized").stringValue="Select an item";filler.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(info.OnFilled,presentation.SelectPage);
                Panel("Notes divider",notebook.transform,new Vector2(.16f,.845f),new Vector2(.93f,.848f),new Color(.18f,.17f,.15f,.25f));
                var menu=(UIInventoryContextMenu)ui.FindProperty("m_ContextMenu").objectReferenceValue;menu.transform.SetParent(notebook.transform,false);Rect((RectTransform)menu.transform,new Vector2(.16f,.055f),new Vector2(.93f,.30f));
                foreach(var image in menu.GetComponentsInChildren<Image>(true)){image.sprite=null;image.color=image.name=="Selected"?new Color(.92f,.91f,.86f,.3f):new Color(.18f,.17f,.15f,.08f);}
                var menuLayout=menu.GetComponent<VerticalLayoutGroup>();if(menuLayout){menuLayout.childControlHeight=true;menuLayout.childForceExpandHeight=false;menuLayout.spacing=2;}
                foreach(var button in menu.GetComponentsInChildren<Button>(true)){ButtonStyle(button);var layout=button.GetComponent<LayoutElement>();if(!layout)layout=button.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=26;foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))Style(text,paperFont,Ink,19);}
                var fit=menu.GetComponent<ContentSizeFitter>();if(fit)fit.enabled=false;
                var primary=(UIInventoryItem)ui.FindProperty("m_Equipped").objectReferenceValue;primary.transform.SetParent(folder.transform,false);Rect((RectTransform)primary.transform,new Vector2(.09f,.13f),new Vector2(.25f,.31f));StyleSlot(primary);EmptyEquipment(primary,"NO WEAPON");
                var secondary=(UIInventoryItem)ui.FindProperty("m_EquippedSecondary").objectReferenceValue;secondary.transform.SetParent(folder.transform,false);Rect((RectTransform)secondary.transform,new Vector2(.3f,.13f),new Vector2(.46f,.31f));StyleSlot(secondary);EmptyEquipment(secondary,"NO TOOL");
                var oldEquipped=content.transform.Find("UIEquipped");if(oldEquipped)oldEquipped.gameObject.SetActive(false);

                Label("Equipment label","EQUIPPED",folder.transform,new Vector2(.09f,.045f),new Vector2(.46f,.105f),16,Ink,terminalFont);
                foreach(var text in secondary.GetComponentsInChildren<TMP_Text>(true))if(text.name=="ToolText")text.gameObject.SetActive(false);
                Label("Primary label","WEAPON",folder.transform,new Vector2(.09f,.31f),new Vector2(.25f,.34f),13,Ink,terminalFont);
                Label("Secondary label","TOOL",folder.transform,new Vector2(.3f,.31f),new Vector2(.46f,.34f),13,Ink,terminalFont);
                var categories=Find(root,"UIInventoryCategories");categories.SetParent(folder.transform,false);Rect((RectTransform)categories,new Vector2(.07f,.8f),new Vector2(.93f,.885f));
                foreach(var button in categories.GetComponentsInChildren<Button>(true)){ButtonStyle(button);foreach(var image in button.GetComponentsInChildren<Image>(true)){image.sprite=null;image.color=new Color(.18f,.17f,.15f,.12f);}foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))Style(text,paperFont,Ink,21);}
                categories.gameObject.SetActive(false);
                BuildTabs(root.transform,0,()=>{},root.GetComponent<UIInventory>().OnFilesCategory,root.GetComponent<UIInventory>().OnMapCategory);
                presentation.CapacityText=Label("Capacity","",folder.transform,new Vector2(.55f,.79f),new Vector2(.93f,.84f),15,Ink,terminalFont);presentation.CapacityText.alignment=TextAlignmentOptions.MidlineRight;
                var status=Find(root,"UIStatus");status.gameObject.SetActive(false);
                string fluidPath=Root+"Inventory_ConditionFluid.mat";
                var fluidMaterial=AssetDatabase.LoadAssetAtPath<Material>(fluidPath);
                if(!fluidMaterial){fluidMaterial=new Material(Shader.Find("HorrorGame/UI/Condition Fluid"));AssetDatabase.CreateAsset(fluidMaterial,fluidPath);}
                var fluidWindow=Panel("Condition fluid window",folder.transform,new Vector2(.54f,.145f),new Vector2(.94f,.315f),Color.white);fluidWindow.material=fluidMaterial;fluidWindow.enabled=false;
                var condition=Label("Condition state","FINE",folder.transform,new Vector2(.54f,.13f),new Vector2(.94f,.235f),24,Ink,terminalFont);condition.alignment=TextAlignmentOptions.Center;
                var fluid=fluidWindow.gameObject.AddComponent<InventoryConditionFluid>();fluid.Window=fluidWindow;fluid.FluidMaterial=fluidMaterial;fluid.Label=condition;fluid.ShowFluid=false;
                Label("Condition heading","CONDITION",folder.transform,new Vector2(.54f,.24f),new Vector2(.94f,.29f),14,Ink,terminalFont).alignment=TextAlignmentOptions.Center;
                var legend=content.transform.Find("ButtonLegend");if(legend)legend.gameObject.SetActive(false);
                Label("Inventory controls","CLICK SELECT   /   DOUBLE-CLICK OR E ACTIONS   /   ESC BACK",content.transform,new Vector2(.08f,.035f),new Vector2(.76f,.075f),15,Cream,terminalFont);
                foreach(var itemFiller in root.GetComponentsInChildren<UIItemFiller>(true)){var data=new SerializedObject(itemFiller);data.FindProperty("m_NormalAmountColor").colorValue=Ink;data.FindProperty("m_EmptyAmountColor").colorValue=new Color(.6f,.12f,.08f);data.FindProperty("m_MaxStackAmountColor").colorValue=Ink;data.ApplyModifiedPropertiesWithoutUndo();}
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
                foreach(var button in root.GetComponentsInChildren<Button>(true))if(button.targetGraphic)button.targetGraphic.raycastTarget=true;
                AddReadability(root,new Vector2(.78f,.035f),new Vector2(.96f,.075f));
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Inventory_Folder.prefab");
                var core=new SerializedObject(AssetDatabase.LoadAssetAtPath<HECorePrefabs>("Assets/AssetStore/HorrorEngine/Prefabs/HECorePrefabs.asset"));core.FindProperty("m_Inventory").objectReferenceValue=prefab;core.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();Debug.Log("FOLDER_INVENTORY_BUILD_OK");
            }
            finally{Object.DestroyImmediate(root);}
            BuildDocuments();
            BuildMapTabs();
        }
        public static void BuildBatch(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;}
        static void Style(TMP_Text t,TMP_FontAsset font,Color color,float size){t.font=font;t.color=color;t.fontSize=size;t.fontStyle=FontStyles.Normal;t.enableWordWrapping=false;t.raycastTarget=false;}
        static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color,Sprite sprite=null){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();Rect(image.rectTransform,min,max);image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;}
        static TextMeshProUGUI Label(string name,string value,Transform parent,Vector2 min,Vector2 max,float size,Color color,TMP_FontAsset font){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var t=go.AddComponent<TextMeshProUGUI>();Rect(t.rectTransform,min,max);Style(t,font,color,size);t.text=value;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
        static void ButtonStyle(Button button){var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.94f,.93f,.88f);c.selectedColor=c.highlightedColor;button.colors=c;}
        static void StyleSlot(UIInventoryItem slot)
        {
            var annotation=slot.gameObject.AddComponent<InventoryItemAnnotation>();annotation.Slot=slot;annotation.Profile=iconProfile;annotation.Equipped=Label("Equipped stamp","EQUIPPED",slot.transform,new Vector2(.25f,.80f),new Vector2(.96f,.98f),10,Ink,terminalFont);annotation.Equipped.alignment=TextAlignmentOptions.MidlineRight;annotation.CombineMark=Label("Combine source","COMBINE",slot.transform,new Vector2(.04f,.80f),new Vector2(.52f,.98f),10,Ink,terminalFont);
            var fillerData=new SerializedObject(slot.GetComponent<UIItemFiller>());fillerData.FindProperty("m_NormalAmountColor").colorValue=Ink;fillerData.FindProperty("m_EmptyAmountColor").colorValue=new Color(.6f,.12f,.08f);fillerData.FindProperty("m_MaxStackAmountColor").colorValue=Ink;fillerData.ApplyModifiedPropertiesWithoutUndo();
            var icon=(Image)fillerData.FindProperty("m_Icon").objectReferenceValue;
            foreach(var image in slot.GetComponentsInChildren<Image>(true))
            {
                if(image==icon){if(icon.transform!=slot.transform)Rect(icon.rectTransform,new Vector2(.08f,.13f),new Vector2(.92f,.88f));image.color=Color.white;image.material=iconPalette;image.preserveAspect=true;image.gameObject.AddComponent<InventoryIconFit>().Profile=iconProfile;continue;}
                if(image.name=="StatusFill")continue;
                image.sprite=null;
                if(image.name=="Selected" || image.name=="SelectionLocked")
                {
                    image.color=Color.clear;image.raycastTarget=false;
                    var frameObject=new GameObject("Ivory selection frame",typeof(RectTransform),typeof(CanvasRenderer));frameObject.layer=5;frameObject.transform.SetParent(image.transform,false);Rect((RectTransform)frameObject.transform,Vector2.zero,Vector2.one);
                    var frame=frameObject.AddComponent<InventorySelectionFrame>();frame.color=new Color(.96f,.94f,.84f);frame.raycastTarget=false;
                }
                else image.color=new Color(.18f,.17f,.15f,.06f);
            }
            var border=slot.GetComponent<Outline>();if(!border)border=slot.gameObject.AddComponent<Outline>();border.effectColor=new Color(.83f,.82f,.77f,.5f);border.effectDistance=new Vector2(1,-1);
            foreach(var text in slot.GetComponentsInChildren<TMP_Text>(true))Style(text,terminalFont,Ink,17);
            var button=slot.GetComponent<Button>();if(button)ButtonStyle(button);
        }
        static void EmptyEquipment(UIInventoryItem slot,string label)
        {
            var annotation=slot.GetComponent<InventoryItemAnnotation>();annotation.Empty=Label("Empty equipment",label,slot.transform,new Vector2(.06f,.25f),new Vector2(.94f,.75f),12,new Color(.24f,.23f,.21f,.6f),terminalFont);annotation.Empty.alignment=TextAlignmentOptions.Center;
        }
        static void SetMenuAudio(SerializedObject ui)
        {
            var paper=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AssetStore/HorrorEngine/ExampleContent/UI/Paper.wav");var click=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AssetStore/HorrorEngine/ExampleContent/UI/Select.wav");
            foreach(string field in new[]{"m_ShowClip","m_CloseClip"}){var prop=ui.FindProperty(field);if(prop!=null)prop.objectReferenceValue=paper;}
            foreach(string field in new[]{"m_UseClip","m_NavigateClip"}){var prop=ui.FindProperty(field);if(prop!=null)prop.objectReferenceValue=click;}
            ui.FindProperty("m_MenuVolume").floatValue=.22f;ui.ApplyModifiedPropertiesWithoutUndo();
        }
        static Sprite Sprite(string name)
        {
            string path=Root+"Textures/"+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static TMP_FontAsset FontAsset()
        {
            string path=Root+"Inventory_CourierFont.asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font)return font;
            font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/CourierPrime-Regular.ttf"));font.name="Inventory Courier Prime";AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:-_.,?<>[]()+%=\\");EditorUtility.SetDirty(font);return font;
        }
    }
}
