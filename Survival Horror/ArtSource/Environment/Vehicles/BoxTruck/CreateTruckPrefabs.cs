using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
public static class CreateTruckPrefabs
{
    public static void Build()
    {
        const string root = "Assets/Truck/";
        var importer = (ModelImporter)AssetImporter.GetAtPath(root + "BoxTruck_Model.fbx");
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.importLights = false;
        importer.importCameras = false;
        importer.SaveAndReimport();
        var filters = AssetDatabase.LoadAssetAtPath<GameObject>(root + "BoxTruck_Model.fbx").GetComponentsInChildren<MeshFilter>();
        if (filters.Length != 1 || filters[0].sharedMesh.subMeshCount != 2) throw new Exception("Expected only the truck, with two material slots.");
        var mesh = Object.Instantiate(filters[0].sharedMesh);
        mesh.name = "BoxTruck_Mesh";
        var matrix = filters[0].transform.localToWorldMatrix;
        mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        mesh.normals = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(root + "BoxTruck_Mesh.asset");
        if (existing)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else AssetDatabase.CreateAsset(mesh, root + "BoxTruck_Mesh.asset");
        var size = mesh.bounds.size;
        if (size.x < 2 || size.x > 3 || size.y < 2 || size.y > 3 || size.z < 5 || size.z > 6) throw new Exception("Truck scale/orientation incorrect: " + size);
        var cab = AssetDatabase.LoadAssetAtPath<Material>(root + "Vehicles_Opaque_A.mat");
        var cargo = AssetDatabase.LoadAssetAtPath<Material>(root + "BoxTruck_Cargo.mat");
        if (!cab || !cargo) throw new Exception("Material missing");
        var truck = new GameObject("BoxTruck");
        try
        {
            truck.AddComponent<MeshFilter>().sharedMesh = mesh;
            truck.AddComponent<MeshRenderer>().sharedMaterials = new[] { cab, cargo };
            truck.AddComponent<HorrorGame.Environment.TruckCargoAtlas>();
            PrefabUtility.SaveAsPrefabAsset(truck, root + "BoxTruck.prefab");
        }
        finally { Object.DestroyImmediate(truck); }
        AssetDatabase.SaveAssets();
        Debug.Log("TRUCK_PREFAB_READY: single randomized truck prefab. Bounds " + size + "; triangles " + mesh.triangles.Length/3);
    }
}
