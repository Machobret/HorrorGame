using System.Collections.Generic;
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
    public static partial class InventoryFolderBuilder
    {
        static InventoryIconProfile BuildIconProfile()
        {
            string path=Root+"Inventory_IconProfile.asset";
            var profile=AssetDatabase.LoadAssetAtPath<InventoryIconProfile>(path);
            if(!profile){profile=ScriptableObject.CreateInstance<InventoryIconProfile>();AssetDatabase.CreateAsset(profile,path);}
            var items=AssetDatabase.FindAssets("t:ItemData").Select(g=>AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).Where(i=>i && i.Image).ToArray();var sprites=items.Select(i=>i.Image).Distinct();
            var entries=new List<InventoryIconProfile.Entry>();
            foreach(var sprite in sprites)
            {
                var texture=sprite.texture;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
                var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
                try
                {
                    Graphics.Blit(texture,target);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();
                    var data=pixels.GetPixels32();var rect=sprite.rect;int minX=(int)rect.xMax,minY=(int)rect.yMax,maxX=-1,maxY=-1;double sx=0,sy=0,sxx=0,syy=0,sxy=0,n=0;
                    for(int y=(int)rect.yMin;y<(int)rect.yMax;y++)for(int x=(int)rect.xMin;x<(int)rect.xMax;x++)if(data[y*texture.width+x].a>12){sx+=x;sy+=y;sxx+=(double)x*x;syy+=(double)y*y;sxy+=(double)x*y;n++;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                    var visible=maxX>=minX ? new Rect((minX-rect.x)/rect.width,(minY-rect.y)/rect.height,(maxX-minX+1)/rect.width,(maxY-minY+1)/rect.height) : new Rect(0,0,1,1);
                    bool wide=items.Any(i=>i.Image==sprite && i.InventorySlots>1);float rotation=0;Rect wideBounds=default;
                    if(wide && n>0){rotation=-(float)(.5*System.Math.Atan2(2*(sxy-sx*sy/n),(sxx-sx*sx/n)-(syy-sy*sy/n))*Mathf.Rad2Deg);var matrix=Quaternion.Euler(0,0,rotation);Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=new Vector2(float.MinValue,float.MinValue);
                        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)if(data[y*texture.width+x].a>12){Vector2 point=matrix*new Vector3(x-rect.center.x,y-rect.center.y,0);lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);}wideBounds=new Rect(lo,hi-lo+Vector2.one);}
                    entries.Add(new InventoryIconProfile.Entry{Sprite=sprite,Visible=visible,Wide=wide,Rotation=rotation,WideBounds=wideBounds});
                }
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixels);}
            }
            profile.AmmoItems=AssetDatabase.FindAssets("t:ReloadableWeaponData").Select(g=>AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(AssetDatabase.GUIDToAssetPath(g))).Where(w=>w && w.AmmoItem).Select(w=>w.AmmoItem).Distinct().ToArray();
            profile.Entries=entries.ToArray();EditorUtility.SetDirty(profile);return profile;
        }

        static void BuildTabs(Transform parent,Transform content,int active,UnityAction inventory,UnityAction files,UnityAction map)
        {
            var old=parent.Find("Menu tabs");if(old)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Menu tabs",typeof(RectTransform)).transform;root.SetParent(parent,false);Rect((RectTransform)root,new Vector2(.05f,.835f),new Vector2(.65f,.88f));
            string[] names={"INVENTORY","FILES","MAP"};UnityAction[] callbacks={inventory,files,map};
            var tabs = new InventoryTabButton[names.Length];
            for(int i=0;i<3;i++)
            {
                var go=new GameObject(names[i]+" tab",typeof(RectTransform),typeof(CanvasRenderer),typeof(InventoryPaperTab));go.layer=5;go.transform.SetParent(root,false);
                var panel=go.GetComponent<InventoryPaperTab>();Rect((RectTransform)panel.transform,new Vector2(i/3f,0),new Vector2((i+1)/3f-.008f,1));panel.PaperTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/Inventory_ManilaFolder.png");panel.material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Inventory_PaperPalette.mat");panel.color=i==active?Color.white:new Color(.78f,.78f,.78f);
                panel.raycastTarget=true;var border=go.AddComponent<Outline>();border.effectColor=new Color(.14f,.13f,.11f,.35f);border.effectDistance=new Vector2(1,-1);
                var button = panel.gameObject.AddComponent<InventoryTabButton>();
                tabs[i] = button;
                button.Content = content;
                button.targetGraphic = panel;
                ButtonStyle(button);
                var colors = button.colors;
                colors.selectedColor = new Color(.72f, .70f, .64f);
                button.colors = colors;
                if(i!=active)UnityEventTools.AddPersistentListener(button.onClick,callbacks[i]);
                Label(names[i]+" label",names[i],panel.transform,new Vector2(0,.15f),new Vector2(1,.95f),18,Ink,terminalFont).alignment=TextAlignmentOptions.Center;
                if(i==active)Panel("Active underline",panel.transform,new Vector2(.15f,.05f),new Vector2(.85f,.085f),Ink);
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? tabs[i - 1] : null,
                    selectOnRight = i < tabs.Length - 1 ? tabs[i + 1] : null
                };
            }
        }

        public static Transform MenuSurface(GameObject root)=>root.GetComponent<Canvas>()?root.transform:root.transform.Find("Tablet display");

        public static void AddReadability(GameObject root)
        {
            if (!root.GetComponent<MenuReadability>())
                root.AddComponent<MenuReadability>();

            var oldToggle = MenuSurface(root).Find("Text size toggle");
            if (oldToggle)
                Object.DestroyImmediate(oldToggle.gameObject);
            MarkTextSizes(root.transform);
        }

        public static void MarkTextSizes(Transform root)
        {
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(!text.GetComponent<MenuTextScale>()){float size=text.fontSize,min=text.fontSizeMin,max=text.fontSizeMax;var marker=text.gameObject.AddComponent<MenuTextScale>();marker.BaseSize=size;marker.BaseMinimum=min;marker.BaseMaximum=max;}
        }

    }
}
