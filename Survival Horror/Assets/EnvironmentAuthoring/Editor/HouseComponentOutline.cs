using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Private mask: scene walls cannot obscure it, and modular seams share one silhouette.
    internal sealed class HouseComponentOutline : IDisposable
    {
        private Material material;
        private RenderTexture mask;
        private CommandBuffer commands;
        private readonly Dictionary<Color, List<(Mesh Mesh, Matrix4x4 Matrix)>> batches = new Dictionary<Color, List<(Mesh, Matrix4x4)>>();
        private readonly List<Color> batchOrder = new List<Color>();
        private bool batching;
        private Camera batchCamera;
        private RenderTexture batchTarget;
        private static readonly Unity.Profiling.ProfilerMarker drawSample = new Unity.Profiling.ProfilerMarker("House.ComponentOutlinePass");

        internal void BeginBatch()
        {
            batching = true;
            batchOrder.Clear();
            batchCamera = null; batchTarget = null;
            foreach (var meshes in batches.Values) meshes.Clear();
        }

        internal void EndBatch()
        {
            batching = false;
            try
            {
                if (batchCamera == null || batchTarget == null) return;
                foreach (var color in batchOrder)
                    Draw(batches[color], color, batchCamera, batchTarget);
            }
            finally
            {
                foreach (var meshes in batches.Values) meshes.Clear();
                batchOrder.Clear();
                batchCamera = null; batchTarget = null;
            }
        }

        internal void Draw(IEnumerable<MeshFilter> meshes, Color color)
        {
            var camera = Camera.current;
            var destination = RenderTexture.active;
            if (camera == null || destination == null) return;
            Draw(meshes, color, camera, destination);
        }

        // Explicit camera/target also lets graphics tests exercise the same rendering path.
        internal void Draw(IEnumerable<MeshFilter> meshes, Color color, Camera camera, RenderTexture destination)
            => Draw(Instances(meshes), color, camera, destination);

        private static IEnumerable<(Mesh Mesh, Matrix4x4 Matrix)> Instances(IEnumerable<MeshFilter> meshes)
        {
            foreach (var filter in meshes)
            {
                if (filter == null || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.enabled && !renderer.forceRenderingOff)
                    yield return (filter.sharedMesh, filter.transform.localToWorldMatrix);
            }
        }

        internal void Draw(IEnumerable<(Mesh Mesh, Matrix4x4 Matrix)> meshes, Color color)
        {
            if (Camera.current != null && RenderTexture.active != null)
                Draw(meshes, color, Camera.current, RenderTexture.active);
        }

        private void Draw(IEnumerable<(Mesh Mesh, Matrix4x4 Matrix)> meshes, Color color, Camera camera, RenderTexture destination)
        {
            if (batching)
            {
                // One target mask per colour, not one full-screen pass per wall.
                // Selection and hover retain separate colours and silhouettes.
                if (!batches.TryGetValue(color, out var batch))
                    batches.Add(color, batch = new List<(Mesh, Matrix4x4)>());
                int before = batch.Count;
                batch.AddRange(meshes);
                if (before == 0 && batch.Count > 0) batchOrder.Add(color);
                batchCamera = camera; batchTarget = destination;
                return;
            }
            using var sample = drawSample.Auto();
            if (commands == null) commands = new CommandBuffer { name = "House component outline" };
            commands.Clear();
            if (material == null)
            {
                var shader = Shader.Find("Hidden/EnvironmentAuthoring/ComponentOutline");
                if (shader == null || !shader.isSupported) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (mask == null || mask.width != destination.width || mask.height != destination.height)
            {
                ReleaseMask();
                mask = new RenderTexture(destination.width, destination.height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                {
                    name = "House outline mask", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                mask.Create();
            }
            material.SetMatrix("_ViewProjection", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true)
                * camera.worldToCameraMatrix);
            material.SetColor("_OutlineColor", color);
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(true, true, Color.clear);
            int count = 0;
            foreach (var instance in meshes)
            {
                if (instance.Mesh == null) continue;
                for (int submesh = 0; submesh < instance.Mesh.subMeshCount; submesh++)
                    commands.DrawMesh(instance.Mesh, instance.Matrix, material, submesh, 0);
                count++;
            }
            if (count == 0) { commands.Clear(); return; }
            // Blend only the silhouette border into the existing handle target; never clear it.
            material.SetTexture("_Mask", mask);
            material.SetVector("_MaskPixel", new Vector4(3f / mask.width, 3f / mask.height, 0, 0));
            commands.SetRenderTarget(destination);
            commands.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3);
            var previous = RenderTexture.active;
            try { Graphics.ExecuteCommandBuffer(commands); }
            finally { RenderTexture.active = previous; commands.Clear(); }
        }

        private void ReleaseMask()
        {
            if (mask == null) return;
            mask.Release();
            UnityEngine.Object.DestroyImmediate(mask);
            mask = null;
        }

        public void Dispose()
        {
            batches.Clear(); batching = false;
            batchOrder.Clear();
            batchCamera = null; batchTarget = null;
            ReleaseMask();
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            material = null;
            commands?.Release();
            commands = null;
        }
    }
}
