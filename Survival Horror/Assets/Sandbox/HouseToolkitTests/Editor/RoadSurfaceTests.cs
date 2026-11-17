using System;
using System.Collections.Generic;
using System.Linq;
using HouseToolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RoadSurfaceTests
{
    const string Root = "Assets/Game/Environment/Roads/TownStreet/";

    static T Asset<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.IsNotNull(asset, path);
        return asset;
    }

    static RoadFamily Family()
    {
        var family = Object.Instantiate(Asset<RoadFamily>(Root + "Town Street.asset"));
        family.leftSidewalk = family.rightSidewalk = false;
        family.forwardLanes = family.reverseLanes = 1;
        family.centerLine = RoadLines.None;
        return family;
    }

    [Test]
    public void FamiliesSharePlainAsphaltAndAllPaintUsesThePaintShader()
    {
        var asphalt = Asset<Material>(Root + "Road Asphalt.mat");
        Assert.AreEqual("House Toolkit/Road Surface", asphalt.shader.name);
        Assert.IsNotNull(asphalt.mainTexture);
        Assert.IsFalse(asphalt.HasProperty("_EdgeMask"));
        foreach (var name in new[] { "Town Street", "Side Street" })
        {
            var f = Asset<RoadFamily>(Root + name + ".asset");
            foreach (var material in new[] { f.road, f.bareAsphalt, f.tJunction, f.xJunction })
                Assert.AreSame(asphalt, material, name);
            Assert.IsTrue(f.edgeLines);
            foreach (var paint in new[] { f.paint, f.edgePaint, f.markingSet.atlas, Resources.Load<Material>("HouseToolkitRoadWhite"), Resources.Load<Material>("HouseToolkitRoadYellow") })
            {
                Assert.AreEqual("House Toolkit/Road Paint", paint.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(paint.shader), paint.name);
                Assert.That(paint.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(paint.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(paint.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            }
        }
    }

    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public void EdgeRibbonsFollowBendsAndJunctionsWithoutChangingCollision(int arms, bool skew)
    {
        var f = Family();
        var go = new GameObject("Road edge test");
        try
        {
            var road = go.AddComponent<RoadNetwork>(); road.family = f;
            var center = road.graph.AddNode(Vector3.zero);
            var mouths = new List<Vector3>();
            for (int i = 0; i < arms; i++)
            {
                float angle = arms == 2 ? i * 90 : i * 360f / arms;
                if (skew && i == 1) angle += 17;
                var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var end = road.graph.AddNode(direction * 20);
                road.graph.Connect(center.id, end.id, 0, f);
                mouths.Add(direction);
            }
            f.edgeLines = false; road.Rebuild();
            int collisionCount = road.GetComponent<MeshCollider>().sharedMesh.triangles.Length;
            f.edgeLines = true; road.Rebuild();
            var mesh = road.GetComponent<MeshFilter>().sharedMesh;
            var collider = road.GetComponent<MeshCollider>().sharedMesh;
            var materials = road.GetComponent<MeshRenderer>().sharedMaterials;
            int slot = Array.IndexOf(materials, f.edgePaint);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            var vertices = mesh.vertices;
            int[] paint = mesh.GetTriangles(slot);
            Assert.That(paint.Length, Is.GreaterThanOrEqualTo(arms * 12));
            Assert.AreEqual(collisionCount, collider.triangles.Length);
            Assert.That(collider.triangles.All(i => Mathf.Abs(collider.vertices[i].y) < .0001f));
            foreach (int index in paint)
                Assert.That(vertices[index].y, Is.EqualTo(.003f).Within(.0001f));
            for (int i = 0; i < paint.Length; i += 3)
            {
                var a = vertices[paint[i]]; var b = vertices[paint[i + 1]]; var c = vertices[paint[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f));
                // Paint may approach a mouth along its edges, but cannot span its centreline.
                if (arms >= 3)
                    foreach (var direction in mouths)
                        Assert.IsFalse(ContainsXZ(a, b, c, direction * 5), "Edge paint blocks a junction mouth");
            }
            // At an ordinary bend both edge strips meet at exactly the same mitered endpoints.
            if (arms == 2)
            {
                var joins = paint.Select(i => vertices[i]).Where(v => Mathf.Abs(v.x) < 4 && Mathf.Abs(v.z) < 4)
                    .GroupBy(v => v).ToArray();
                Assert.That(joins.Length, Is.EqualTo(4));
                Assert.That(joins.All(g => g.Count() >= 2));
            }
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(f); }
    }

    static bool ContainsXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        float Cross(Vector3 x, Vector3 y) => x.x * y.z - x.z * y.x;
        float x = Cross(b-a, p-a), y = Cross(c-b, p-b), z = Cross(a-c, p-c);
        return (x > .00001f && y > .00001f && z > .00001f) || (x < -.00001f && y < -.00001f && z < -.00001f);
    }

    [Test]
    public void WheelPathsFadeOnlyAtOpenEndsAndDoNotChangeMarkings()
    {
        var f = Family(); var go = new GameObject("Road wear test");
        try
        {
            var road = go.AddComponent<RoadNetwork>(); road.family = f;
            var a = road.graph.AddNode(Vector3.zero);
            var b = road.graph.AddNode(Vector3.forward * 10);
            var c = road.graph.AddNode(Vector3.forward * 20);
            road.graph.Connect(a.id, b.id, 0, f); road.graph.Connect(b.id, c.id, 0, f);
            road.Rebuild();
            var mesh = road.GetComponent<MeshFilter>().sharedMesh;
            var data = new List<Vector4>(); mesh.GetUVs(2, data);
            var uv = new List<Vector4>(); mesh.GetUVs(0, uv);
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (uv[i].z != -1) continue;
                float distance = Mathf.Min(data[i].z, data[i].w);
                if (Mathf.Abs(mesh.vertices[i].z - 10) < .001f)
                    Assert.That(distance, Is.GreaterThanOrEqualTo(f.wheelPathEndFade - .001f));
                else Assert.That(distance, Is.EqualTo(0).Within(.001f));
            }
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(f); }
    }

    [Test]
    public void AsphaltRendersTheSameWorldPatternAcrossTransformsUVsAndTextureAlpha()
    {
        var preview = new PreviewRenderUtility();
        var material = new Material(Asset<Material>(Root + "Road Asphalt.mat"));
        var go = new GameObject("Hand-authored asphalt");
        var filter = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        preview.AddSingleGO(go);
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var meshes = new List<Mesh>();
        Texture2D baseline = null, transformed = null, changedRepeat = null;
        bool async = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                texture.SetPixel(x, y, new Color(x / 15f, y / 15f, (x+y) % 3 / 2f, 0));
            texture.Apply(); material.mainTexture = texture; material.color = Color.white;
            material.SetFloat("_RepeatMetres", 3.7f);
            material.mainTextureScale = new Vector2(.7f, 1.3f); material.mainTextureOffset = new Vector2(.13f, -.21f);
            // Keep traffic enabled: ordinary mesh attributes must never produce wheel-path tint.
            material.SetFloat("_TrafficWear", 1);
            var camera = preview.camera;
            camera.cameraType = CameraType.Game; camera.orthographic = true; camera.orthographicSize = 4;
            camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta; camera.allowHDR = false;
            preview.ambientColor = Color.gray;
            preview.lights[0].intensity = 1; preview.lights[0].transform.rotation = Quaternion.Euler(90, 0, 0);
            preview.lights[1].intensity = 0;
            Texture2D Render(Matrix4x4 matrix, bool roadUV)
            {
                var inverse = matrix.inverse;
                var mesh = new Mesh(); meshes.Add(mesh);
                mesh.vertices = new[] { new Vector3(-4,0,-4), new Vector3(-4,0,4), new Vector3(4,0,4), new Vector3(4,0,-4) }.Select(inverse.MultiplyPoint3x4).ToArray();
                mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals();
                mesh.SetUVs(0, new List<Vector4> { Vector4.zero, new Vector4(0,5,4,0), new Vector4(7,5,4,0), new Vector4(7,0,4,0) });
                if (!roadUV) mesh.uv = new[] { Vector2.one, Vector2.zero, Vector2.one, Vector2.zero };
                mesh.colors = Enumerable.Repeat(Color.black, 4).ToArray();
                filter.sharedMesh = mesh;
                go.transform.SetPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
                go.transform.localScale = matrix.lossyScale;
                preview.BeginStaticPreview(new Rect(0, 0, 256, 256));
                preview.Render(true);
                return preview.EndStaticPreview();
            }
            baseline = Render(Matrix4x4.identity, true);
            transformed = Render(Matrix4x4.TRS(new Vector3(13,2,-9), Quaternion.Euler(0,37,0), new Vector3(1.8f,.6f,.7f)), false);
            string capture = Environment.GetEnvironmentVariable("ROAD_PROJECTION_PREVIEW");
            if (!string.IsNullOrEmpty(capture))
            {
                System.IO.Directory.CreateDirectory(capture);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(capture, "Asphalt_Reference.png"), baseline.EncodeToPNG());
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(capture, "Asphalt_Transformed.png"), transformed.EncodeToPNG());
            }
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            var original = baseline.GetPixels(); var moved = transformed.GetPixels();
            float difference = original.Zip(moved, (a,b) => Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b))).Average();
            Assert.That(difference, Is.LessThan(.005f), "Object transform or mesh UVs changed the asphalt phase");
            Assert.That(original.Max(c => c.grayscale) - original.Min(c => c.grayscale), Is.GreaterThan(.2f), "Diagnostic pattern did not render");
            material.SetFloat("_RepeatMetres", 2);
            changedRepeat = Render(Matrix4x4.identity, true);
            Assert.That(original.Zip(changedRepeat.GetPixels(), (a,b) => Mathf.Abs(a.r-b.r)).Average(), Is.GreaterThan(.05f), "Material repeat must control world texture scale");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = async; preview.Cleanup();
            foreach (var item in new Object[] { baseline, transformed, changedRepeat, material, texture }.Concat(meshes.Cast<Object>())) if (item) Object.DestroyImmediate(item);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void PaintRendersIndependentlyOnStraightsBendsAndJunctions(int arms)
    {
        var f = Family(); var go = new GameObject("Road paint preview");
        var preview = new PreviewRenderUtility();
        Texture2D fresh = null, bareEdges = null, worn = null;
        bool async = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            f.paintWear = 0; f.centerLine = RoadLines.Solid; f.wheelPathTint = Color.white;
            var road = go.AddComponent<RoadNetwork>(); road.family = f;
            var center = road.graph.AddNode(arms == 1 ? Vector3.back * 14 : Vector3.zero);
            for (int i = 0; i < arms; i++)
            {
                float angle = arms == 2 ? i * 90 : arms == 3 ? (i == 0 ? 0 : i == 1 ? 90 : 270) : i * 90;
                var end = road.graph.AddNode(Quaternion.Euler(0, angle, 0) * Vector3.forward * 14);
                var span = road.graph.Connect(center.id, end.id, 0, f);
                if (arms >= 3)
                {
                    span.start.crosswalk = span.start.stopLine = true;
                    span.start.crosswalkStyle = "Ladder"; span.start.symbol = "Straight arrow";
                }
            }
            road.Rebuild(); preview.AddSingleGO(go);
            var camera = preview.camera;
            camera.cameraType = CameraType.Game; camera.orthographic = true; camera.orthographicSize = 16;
            camera.nearClipPlane = .1f; camera.farClipPlane = 60;
            camera.transform.SetPositionAndRotation(new Vector3(0, 30, 0), Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.09f,.1f); camera.allowHDR = false;
            preview.ambientColor = Color.gray;
            preview.lights[0].intensity = 1; preview.lights[0].transform.rotation = Quaternion.Euler(65, -25, 0);
            preview.lights[1].intensity = 0;
            Texture2D Render()
            {
                preview.BeginStaticPreview(new Rect(0,0,1024,1024)); preview.Render(true);
                return preview.EndStaticPreview();
            }
            float Sample(Texture2D picture, float x, float z)
            {
                var p = camera.WorldToViewportPoint(new Vector3(x, 0, z));
                return picture.GetPixel((int)(p.x * picture.width), (int)(p.y * picture.height)).grayscale;
            }
            fresh = Render();
            string capture = Environment.GetEnvironmentVariable("ROAD_PROJECTION_PREVIEW");
            if (!string.IsNullOrEmpty(capture))
            {
                System.IO.Directory.CreateDirectory(capture);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(capture, "Road_" + arms + "Arms.png"), fresh.EncodeToPNG());
            }
            f.edgeLines = false; road.Rebuild(); bareEdges = Render();
            Assert.That(Sample(fresh, 2.64f, 11) - Sample(bareEdges, 2.64f, 11), Is.GreaterThan(.08f), "Independent edge ribbon must render");
            Assert.That(Mathf.Abs(Sample(fresh, 2, 11) - Sample(bareEdges, 2, 11)), Is.LessThan(.01f), "Removing edge paint must leave asphalt intact");
            f.edgeLines = true; f.paintWear = 1; road.Rebuild(); worn = Render();
            int Bright(Texture2D picture) => picture.GetPixels().Count(c => c.grayscale > .4f);
            Assert.That(Bright(fresh) - Bright(worn), Is.GreaterThan(50), "Paint scuffs must expose the asphalt");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = async; preview.Cleanup();
            foreach (var item in new Object[] { fresh, bareEdges, worn, go, f }) if (item) Object.DestroyImmediate(item);
        }
    }
}
