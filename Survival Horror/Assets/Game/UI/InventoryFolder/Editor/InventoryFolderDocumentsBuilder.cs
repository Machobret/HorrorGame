using System.Linq;
using HorrorEngine;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace HorrorGame.UI.Editor
{
    public static partial class InventoryFolderBuilder
    {
        static GameObject DocumentRoot(string source)
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/UI/"+source+".prefab"));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))Style(text,paperFont,Ink,20);
            return root;
        }
        static void DocumentAtmosphere(GameObject root,Transform content,RectTransform notebook)
        {
            var fogObject=new GameObject("Animated white fog",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            fogObject.transform.SetParent(root.transform,false);fogObject.transform.SetAsFirstSibling();
            var fog=fogObject.GetComponent<RawImage>();Rect(fog.rectTransform,Vector2.zero,Vector2.one);fog.raycastTarget=false;
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Inventory_Fog.mat");fog.material=material;
            var presentation=root.AddComponent<InventoryFolderPresentation>();presentation.Fog=fog;presentation.FogMaterial=material;presentation.Content=content.gameObject.AddComponent<CanvasGroup>();presentation.Notebook=notebook;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            foreach(var button in root.GetComponentsInChildren<Button>(true))if(button.targetGraphic)button.targetGraphic.raycastTarget=true;
        }
        static void BuildDocuments()
        {
            var paper=AssetDatabase.LoadAssetAtPath<Material>(Root+"Inventory_PaperPalette.mat");
            var root=DocumentRoot("UIDocs");
            try
            {
                var data=new SerializedObject(root.GetComponent<UIDocs>());SetMenuAudio(data);
                var content=new GameObject("Files content",typeof(RectTransform)).transform;content.SetParent(root.transform,false);Rect((RectTransform)content,Vector2.zero,Vector2.one);
                var bg=Find(root,"Bg");bg.gameObject.SetActive(false);
                var folder=Panel("Files folder",content,new Vector2(.045f,.16f),new Vector2(.65f,.9f),Color.white,Sprite("Inventory_ManilaFolder"));folder.material=paper;folder.transform.SetAsFirstSibling();
                var note=Panel("File details paper",content,new Vector2(.665f,.12f),new Vector2(.965f,.91f),Color.white,Sprite("Inventory_TornNotebook"));note.material=paper;

                var title=(TMP_Text)data.FindProperty("m_DocumentName").objectReferenceValue;
                title.transform.SetParent(note.transform,false);Rect(title.rectTransform,new Vector2(.16f,.62f),new Vector2(.92f,.88f));Style(title,paperFont,Ink,25);title.enableWordWrapping=true;title.alignment=TextAlignmentOptions.TopLeft;
                Label("Read instructions","Select a collected file.\n\nOpen it to read the contents.",note.transform,new Vector2(.16f,.24f),new Vector2(.92f,.56f),20,Ink,paperFont).enableWordWrapping=true;
                var page=(GameObject)data.FindProperty("m_DocumentsPage").objectReferenceValue;
                var parent=(Transform)data.FindProperty("m_PageParent").objectReferenceValue;
                parent.SetParent(folder.transform,false);var panelImage=parent.GetComponent<Image>();if(panelImage)panelImage.enabled=false;Rect((RectTransform)parent,new Vector2(.08f,.21f),new Vector2(.92f,.77f));
                var pageRect=(RectTransform)page.transform;Rect(pageRect,new Vector2(.5f,.5f),new Vector2(.5f,.5f));pageRect.sizeDelta=new Vector2(620,330);
                var grid=page.GetComponent<GridLayoutGroup>();if(!grid)grid=page.GetComponentInChildren<GridLayoutGroup>();
                grid.cellSize=new Vector2(100,100);grid.spacing=new Vector2(12,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;grid.childAlignment=TextAnchor.MiddleCenter;
                foreach(var fitter in page.GetComponentsInChildren<ContentSizeFitter>(true))fitter.enabled=false;
                foreach(string name in new[]{"m_PageLeftHook","m_PageCenterHook","m_PageRightHook"})
                {
                    var hook=(RectTransform)data.FindProperty(name).objectReferenceValue;hook.SetParent(parent,false);Rect(hook,new Vector2(.5f,.5f),new Vector2(.5f,.5f));hook.anchoredPosition=new Vector2(name=="m_PageLeftHook"?-1600:name=="m_PageRightHook"?1600:0,0);
                }
                foreach(var entry in page.GetComponentsInChildren<UIDocumentEntry>(true))
                {
                    var icon=(Image)new SerializedObject(entry).FindProperty("m_Icon").objectReferenceValue;Rect(icon.rectTransform,new Vector2(.14f,.30f),new Vector2(.86f,.92f));
                    foreach(var image in entry.GetComponentsInChildren<Image>(true)){if(image==icon){image.material=iconPalette;image.color=Color.white;image.preserveAspect=true;}else{image.sprite=null;image.color=image.name=="Selected"?new Color(.95f,.94f,.89f,.32f):new Color(.18f,.17f,.15f,.13f);}}
                    var backing=Panel("File slot",entry.transform,Vector2.zero,Vector2.one,new Color(.18f,.17f,.15f,.13f));backing.transform.SetAsFirstSibling();
                    var border=entry.gameObject.AddComponent<Outline>();border.effectColor=new Color(.83f,.82f,.77f,.5f);border.effectDistance=new Vector2(1,-1);
                    var annotation=entry.gameObject.AddComponent<InventoryFileAnnotation>();annotation.Entry=entry;
                    annotation.Title=Label("File title","",entry.transform,new Vector2(.04f,.02f),new Vector2(.96f,.28f),11,Ink,paperFont);annotation.Title.enableWordWrapping=true;annotation.Title.alignment=TextAlignmentOptions.Center;annotation.Title.enableAutoSizing=true;annotation.Title.fontSizeMin=9;annotation.Title.fontSizeMax=11;
                    var corner=new GameObject("Unread corner",typeof(RectTransform),typeof(CanvasRenderer),typeof(InventoryPaperTab));corner.transform.SetParent(entry.transform,false);Rect((RectTransform)corner.transform,new Vector2(.77f,.77f),new Vector2(.98f,.98f));corner.GetComponent<InventoryPaperTab>().Corner=true;corner.GetComponent<InventoryPaperTab>().color=Ink;corner.GetComponent<InventoryPaperTab>().raycastTarget=false;annotation.Unread=corner;
                    ButtonStyle(entry.GetComponent<Button>());
                }
                foreach(string key in new[]{"m_LeftArrow","m_RightArrow"})
                {
                    var arrow=(UISelectableCallbacks)data.FindProperty(key).objectReferenceValue;arrow.transform.SetParent(folder.transform,false);float x=key=="m_LeftArrow"?.09f:.84f;Rect((RectTransform)arrow.transform,new Vector2(x,.1f),new Vector2(x+.06f,.17f));foreach(var image in arrow.GetComponentsInChildren<Image>(true))image.color=Ink;
                }
                var categories=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="UIInventoryCategories");
                if(categories){categories.SetParent(folder.transform,false);Rect((RectTransform)categories,new Vector2(.07f,.8f),new Vector2(.93f,.885f));foreach(var button in categories.GetComponentsInChildren<Button>(true)){ButtonStyle(button);foreach(var image in button.GetComponentsInChildren<Image>(true)){image.sprite=null;image.color=new Color(.18f,.17f,.15f,.12f);}}}
                if(categories)categories.gameObject.SetActive(false);
                BuildTabs(root.transform,parent,1,root.GetComponent<UIDocs>().OnItemsCategory,()=>{},root.GetComponent<UIDocs>().OnMapCategory);
                Find(root,"ButtonLegend").gameObject.SetActive(false);
                var oldInfo=Find(root,"DocumentInfo");foreach(var image in oldInfo.GetComponentsInChildren<Image>(true))image.enabled=false;
                Label("Files controls","SELECT FILE   /   E OR CLICK READ   /   ESC BACK",content,new Vector2(.08f,.035f),new Vector2(.76f,.075f),15,Cream,terminalFont);
                data.FindProperty("m_AnimationTime").floatValue=.18f;data.ApplyModifiedPropertiesWithoutUndo();
                DocumentAtmosphere(root,content,note.rectTransform);
                AddReadability(root);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Inventory_Files.prefab");
                var core=new SerializedObject(AssetDatabase.LoadAssetAtPath<HECorePrefabs>("Assets/AssetStore/HorrorEngine/Prefabs/HECorePrefabs.asset"));core.FindProperty("m_DocumentList").objectReferenceValue=prefab;core.ApplyModifiedPropertiesWithoutUndo();
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
        }
    }
}
