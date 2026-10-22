using System;
using System.Collections.Generic;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HorrorGame.Weapons.Editor
{
 public static class AmmoBoxSideTextureBuilder
 {
  const string Folder="Assets/Game/Weapons/Models/AmmoSides/";
  static readonly string[] Names={"PistolAmmo","ShotgunAmmo","RevolverAmmo","HuntingRifleAmmo"};
  static readonly string[] Bases={"Assets/AssetStore/HorrorEngine/ExampleContent/Items/GunAmmo/GunAmmo.prefab","Assets/AssetStore/HorrorEngine/ExampleContent/Items/ShotgunAmmo/ShotgunAmmo.prefab",null,null};
  static readonly string[] Exams={"Assets/AssetStore/HorrorEngine/ExampleContent/Items/GunAmmo/GunAmmoExamination.prefab","Assets/AssetStore/HorrorEngine/ExampleContent/Items/ShotgunAmmo/ShotgunAmmoExamination.prefab","Assets/Game/Weapons/Prefabs/RevolverAmmo_Examination.prefab","Assets/Game/Weapons/Prefabs/HuntingRifleAmmo_Examination.prefab"};
  static readonly float[] RowStart={0,347,680,957,1254};
  [MenuItem("Horror Game/Weapons/Apply Ammo Box Side Textures")]
  public static void Apply()
  {
   // Preserve the complete packaging revision when this older menu is used.
   if(AssetDatabase.LoadAssetAtPath<GameObject>(AmmoPackagingBuilder.Folder+"PistolAmmo_Model.prefab")){AmmoPackagingBuilder.Apply();return;}
   AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"Ammo_Side_Atlas.png");importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaSource=TextureImporterAlphaSource.None;importer.mipmapEnabled=true;importer.SaveAndReimport();
   var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Ammo_Side_Atlas.png");
   for(int i=0;i<Names.Length;++i){
    string materialPath=Folder+Names[i]+"_Sides.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
    mat.SetTexture("_BaseMap",atlas);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.05f);mat.SetFloat("_Metallic",0);mat.SetTextureScale("_BaseMap",new Vector2(1,(RowStart[i+1]-RowStart[i])/1254f));mat.SetTextureOffset("_BaseMap",new Vector2(0,1-RowStart[i+1]/1254f));EditorUtility.SetDirty(mat);
    string pickup="Assets/Game/Weapons/Prefabs/"+Names[i]+"_Pickup.prefab";
    // Start from the pickup to preserve the actual imported model and its dimensions.
    UpdatePrefab(pickup,Names[i],mat);
    if(Bases[i]!=null)UpdatePrefab(Bases[i],Names[i],mat);
    UpdatePrefab(Exams[i],Names[i],mat);
   }
   AssetDatabase.SaveAssets();Debug.Log("AMMO_SIDE_TEXTURES_OK four cartons, pickups and examinations; original item IDs preserved");
  }
  public static void ApplyToModel(GameObject model,string name)
  {
   if(AmmoPackagingBuilder.ApplyToModel(model,name))return;
   string key=name=="RifleAmmo"?"HuntingRifleAmmo":name=="RevolverAmmo"?name:null;if(key==null)return;
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+key+"_SideUV.asset");var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+key+"_Sides.mat");if(!mesh||!mat)return;
   foreach(var f in model.GetComponentsInChildren<MeshFilter>()){f.sharedMesh=mesh;var r=f.GetComponent<MeshRenderer>();var materials=r.sharedMaterials;Array.Resize(ref materials,mesh.subMeshCount);materials[materials.Length-1]=mat;r.sharedMaterials=materials;}
  }
  static void UpdatePrefab(string path,string name,Material sideMaterial)
  {
   var root=PrefabUtility.LoadPrefabContents(path);
   try{
    foreach(var f in root.GetComponentsInChildren<MeshFilter>()){
     var source=f.sharedMesh;var renderer=f.GetComponent<MeshRenderer>();if(!source||!renderer)continue;
     string meshPath=Folder+name+"_SideUV.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
     if(!mesh||(path.EndsWith(name+"_Pickup.prefab")&&!AssetDatabase.GetAssetPath(source).StartsWith(Folder))){
      var replacement=BuildMesh(root.transform,f,name);
      if(mesh){EditorUtility.CopySerialized(replacement,mesh);Object.DestroyImmediate(replacement);EditorUtility.SetDirty(mesh);}else{mesh=replacement;AssetDatabase.CreateAsset(mesh,meshPath);}
     }
     if(path.EndsWith(name+"_Pickup.prefab")) RefreshSideUV(root.transform,f,mesh);
     f.sharedMesh=mesh;var materials=renderer.sharedMaterials;Array.Resize(ref materials,mesh.subMeshCount);materials[materials.Length-1]=sideMaterial;renderer.sharedMaterials=materials;
    }
    PrefabUtility.SaveAsPrefabAsset(root,path);
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  static Mesh BuildMesh(Transform root,MeshFilter filter,string name)
  {
   var source=filter.sharedMesh;var vertices=source.vertices;var normals=source.normals;var uv=source.uv;
   var points=new Vector3[vertices.Length];for(int i=0;i<points.Length;++i)points[i]=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
   var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
   var outV=new List<Vector3>();var outN=new List<Vector3>();var outUV=new List<Vector2>();var triangles=new List<int>[source.subMeshCount+1];for(int s=0;s<triangles.Length;++s)triangles[s]=new List<int>();int sideFaces=0;
   for(int s=0;s<source.subMeshCount;++s){var faces=source.GetTriangles(s);for(int t=0;t<faces.Length;t+=3){
    int a=faces[t],b=faces[t+1],c=faces[t+2];Vector3 n=Vector3.Cross(points[b]-points[a],points[c]-points[a]).normalized;
    bool side=name=="RevolverAmmo"?n.y<.65f:name=="HuntingRifleAmmo"?s==0&&Mathf.Abs(n.z)<.85f:n.y<.85f;
    int slot=side?source.subMeshCount:s;if(side)++sideFaces;
    for(int k=0;k<3;++k){int v=faces[t+k];triangles[slot].Add(outV.Count);outV.Add(vertices[v]);outN.Add(normals.Length==vertices.Length?normals[v]:Vector3.up);outUV.Add(side?SideUV(points[v],n,bounds):uv[v]);}
   }}
   if(sideFaces<4)throw new Exception("No carton sides selected for "+name);
   var mesh=new Mesh{name=name+"_SideUV"};mesh.SetVertices(outV);mesh.SetNormals(outN);mesh.SetUVs(0,outUV);mesh.subMeshCount=triangles.Length;for(int s=0;s<triangles.Length;++s)mesh.SetTriangles(triangles[s],s);mesh.RecalculateBounds();mesh.RecalculateTangents();
   Debug.Log("AMMO_SIDE_UV "+name+" side triangles="+sideFaces+" original="+vertices.Length+" replacement="+outV.Count);return mesh;
  }
  static void RefreshSideUV(Transform root,MeshFilter filter,Mesh mesh)
  {
   var vertices=mesh.vertices;var points=new Vector3[vertices.Length];for(int i=0;i<points.Length;++i)points[i]=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
   var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
   var uv=mesh.uv;var triangles=mesh.GetTriangles(mesh.subMeshCount-1);
   for(int i=0;i<triangles.Length;i+=3){var normal=Vector3.Cross(points[triangles[i+1]]-points[triangles[i]],points[triangles[i+2]]-points[triangles[i]]).normalized;for(int k=0;k<3;++k)uv[triangles[i+k]]=SideUV(points[triangles[i+k]],normal,bounds);}
   mesh.uv=uv;mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
  }
  static Vector2 SideUV(Vector3 p,Vector3 n,Bounds b)
  {
   float u,v;var size=b.size;
   if(Mathf.Abs(n.y)>.85f){u=(p.x-b.min.x)/size.x;v=(p.z-b.min.z)/size.z;}
   else{v=(p.y-b.min.y)/size.y;if(Mathf.Abs(n.x)>Mathf.Abs(n.z)){u=(p.z-b.min.z)/size.z;if(n.x<0)u=1-u;}else{u=(p.x-b.min.x)/size.x;if(n.z>0)u=1-u;}}
   if(Mathf.Abs(n.y)<.85f){float width=Mathf.Abs(n.x)>Mathf.Abs(n.z)?size.z:size.x;if(size.y>width*1.5f){float oldU=u;u=v;v=1-oldU;}}
   return new Vector2(Mathf.Lerp(.01f,.99f,Mathf.Clamp01(u)),Mathf.Lerp(.04f,.96f,Mathf.Clamp01(v)));
  }
  public static void RunBatch(){try{Apply();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
