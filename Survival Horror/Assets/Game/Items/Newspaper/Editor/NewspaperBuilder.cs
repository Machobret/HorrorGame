using System;
using System.Collections.Generic;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;

namespace HorrorGame.Items.Editor
{
    public static class NewspaperBuilder
    {
        public const string Folder="Assets/Game/Items/Newspaper/";
        public const string DocumentPath=Folder+"Newspaper_CityHerald.asset";
        public const string PrefabPath=Folder+"Newspaper_Pickup.prefab";

        static LocalizableText Text(string value)=>new LocalizableText {IsLocalized=false,Unlocalized=value};
        static LocalizableSprite Image(Sprite sprite)=>new LocalizableSprite {IsLocalized=false,Unlocalized=sprite};

        [MenuItem("Horror Game/Items/Build and Place Newspaper")]
        public static void BuildAndPlace()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before building.");
            Build();
            var document=AssetDatabase.LoadAssetAtPath<DocumentData>(DocumentPath);
            var existing=Object.FindObjectsByType<PickupDocument>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .FirstOrDefault(p=>new SerializedObject(p).FindProperty("m_Data").objectReferenceValue==document);
            if(existing){UseEmptyPlaceholder(existing.gameObject);Selection.activeGameObject=existing.gameObject;return;}
            var counter=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t=>t.name.StartsWith("CheckoutCounter",StringComparison.Ordinal))
                .OrderBy(t=>Vector3.Distance(t.position,new Vector3(-31.6f,0,-22))).FirstOrDefault();
            if(!counter)throw new InvalidOperationException("No checkout counter found in the current scene; the prefab was built for manual placement.");
            var renderers=counter.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Undo.RegisterCreatedObjectUndo(root,"Place newspaper clue");
            root.transform.position=new Vector3(bounds.center.x,bounds.max.y+.007f,bounds.center.z);
            root.transform.rotation=Quaternion.Euler(0,12,0);
            var id=root.GetComponent<ObjectUniqueId>();id.RegenerateId();PrefabUtility.RecordPrefabInstancePropertyModifications(id);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            UseEmptyPlaceholder(root);
            EditorSceneManager.MarkSceneDirty(root.scene);Selection.activeGameObject=root;
            Debug.Log($"NEWSPAPER_PLACED_OK: {root.transform.position}, casualty clue 1998, native PickupDocument.");
        }

        static void UseEmptyPlaceholder(GameObject newspaper)
        {
            var placeholder=Object.FindObjectsByType<PickupDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(p=>p.name=="PickupDocument"&&p.gameObject!=newspaper&&
                    Vector3.Distance(p.transform.position,newspaper.transform.position)<2&&
                    !new SerializedObject(p).FindProperty("m_Data").objectReferenceValue);
            if(!placeholder)return;
            Undo.RecordObject(newspaper.transform,"Use newspaper placeholder position");
            newspaper.transform.position=placeholder.transform.position+Vector3.up*.007f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(newspaper.transform);
            Undo.RecordObject(placeholder.gameObject,"Disable empty document placeholder");placeholder.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
            EditorSceneManager.MarkSceneDirty(newspaper.scene);
            Debug.Log("NEWSPAPER_PLACEHOLDER_REPLACED: newspaper uses the empty document's countertop position; unconfigured placeholder retained inactive.");
        }

        public static void Build()
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"Newspaper_FrontPage.png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var image=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"Newspaper_FrontPage.png");
            var document=AssetDatabase.LoadAssetAtPath<DocumentData>(DocumentPath);
            if(!document){document=ScriptableObject.CreateInstance<DocumentData>();AssetDatabase.CreateAsset(document,DocumentPath);}
            document.GenerateId();document.Name=Text("The City Herald — Emergency Edition");document.Image=Image(image);
            document.ShowImageOnRead=true;document.ShowPageCount=true;
            // The template reader uses a large fixed font; paginate instead of shrinking all documents.
            string[] pages={
                "<b>THE CITY HERALD</b>\nEMERGENCY EDITION\n\n<b>1,998 DEAD</b>\nHero battle leaves district in ruins\n\nAuthorities have confirmed 1,998 deaths following the battle in the city centre. Rescue crews are still searching the apartment blocks. The official toll is expected to rise.",
                "<b>OFFICIALS PRAISE HERO RESPONSE</b>\n\nA spokesman credited the heroes with preventing further destruction. Residents interviewed by the Herald described buildings collapsing before any evacuation warning was given.\n\nFamilies seeking missing relatives are directed to the perimeter checkpoint.",
                "<b>DISTRICT REMAINS SEALED</b>\n\nPharmacies and local shops have been ordered to retain emergency supplies.\n\n<i>Written in the margin:</i>\n\"Storage lock changed. Use the toll on the front page. Digits only. — M.\""
            };
            document.Pages=pages.Select(page=>new DocumentPage{Text=Text("<margin-left=16%><margin-right=2%>"+page),Image=Image(null),Caption=Text("")}).ToList();
            // Keep the template's document page/open/close sounds.
            var sample=AssetDatabase.LoadAssetAtPath<DocumentData>("Assets/AssetStore/HorrorEngine/ExampleContent/Documents/DemoDocument.asset");
            document.ShowClip=sample.ShowClip;document.PageClip=sample.PageClip;document.CloseClip=sample.CloseClip;
            EditorUtility.SetDirty(document);
            var database=AssetDatabase.LoadAssetAtPath<DocumentDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/DocumentDB.asset");
            if(!database.Registers.Contains(document))database.Registers.Add(document);EditorUtility.SetDirty(database);

            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/PickupDocument.prefab"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                root.name="Newspaper_Pickup";root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var data=new SerializedObject(root.GetComponent<PickupDocument>());
                data.FindProperty("m_Data").objectReferenceValue=document;data.FindProperty("m_ReadOnPickup").boolValue=true;
                data.FindProperty("m_DocObtainedDialog.m_Lines").arraySize=0;data.ApplyModifiedPropertiesWithoutUndo();
                var interaction=root.GetComponentInChildren<Interactive>(true);
                var prompt=AssetDatabase.LoadAssetAtPath<InteractionData>(Folder+"Newspaper_Interaction.asset");
                if(!prompt){prompt=Object.Instantiate(interaction.Data);AssetDatabase.CreateAsset(prompt,Folder+"Newspaper_Interaction.asset");}
                prompt.Prompt=Text("Read newspaper");prompt.AnimState=null;prompt.InteractionDuration=0;prompt.InteractionDelay=0;prompt.RotateToInteractive=false;
                EditorUtility.SetDirty(prompt);interaction.Data=prompt;
                // Rewire the native pickup event explicitly (the vendor prefab contains an old type name).
                interaction.OnInteract=new OnInteractionEvent();UnityEventTools.AddVoidPersistentListener(interaction.OnInteract,root.GetComponent<PickupDocument>().Take);
                var trigger=interaction.GetComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,.12f,0);trigger.size=new Vector3(.7f,.35f,.7f);
                var identity=root.GetComponent<ObjectUniqueId>();var previous=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if(previous)identity.SetId(previous.GetComponent<ObjectUniqueId>().Id);else identity.RegenerateId();identity.IsUniqueInstance=false;

                var paper=GameObject.CreatePrimitive(PrimitiveType.Cube);paper.name="Paper_Stack";paper.transform.SetParent(root.transform,false);
                paper.transform.localScale=new Vector3(.32f,.003f,.43f);
                paper.GetComponent<Renderer>().sharedMaterial=Material("Newspaper_Paper",new Color(.57f,.52f,.4f),null);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"Newspaper_CreasedMesh.asset");
                if(!mesh){mesh=new Mesh {name="Newspaper_CreasedMesh"};AssetDatabase.CreateAsset(mesh,Folder+"Newspaper_CreasedMesh.asset");}
                mesh.Clear();var vertices=new List<Vector3>();var uv=new List<Vector2>();
                for(int x=0;x<3;x++)for(int z=0;z<2;z++)
                {vertices.Add(new Vector3((x-1)*.16f,x==1?.005f:.002f,(z-.5f)*.43f));uv.Add(new Vector2(x*.5f,z));}
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.triangles=new[]{0,1,2,2,1,3,2,3,4,4,3,5};mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var face=new GameObject("Newspaper_FrontPage",typeof(MeshFilter),typeof(MeshRenderer));face.transform.SetParent(root.transform,false);
                face.GetComponent<MeshFilter>().sharedMesh=mesh;face.GetComponent<MeshRenderer>().sharedMaterial=Material("Newspaper_Ink",Color.white,image.texture);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Debug.Log("NEWSPAPER_BUILD_OK: front-page art, native readable document, registered document ID, 1998 clue.");
        }
        static Material Material(string name,Color color,Texture texture)
        {
            var result=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(!result){result=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(result,Folder+name+".mat");}
            result.SetColor("_BaseColor",color);result.SetTexture("_BaseMap",texture);result.SetFloat("_Smoothness",0);
            EditorUtility.SetDirty(result);return result;
        }
    }
}
