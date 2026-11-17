using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Shelf")]
    public sealed class ProceduralShelf : ProceduralPath
    {
        public ShelfFamily family;
        // Zero inherits the family, including on shelves saved before local sizing was added.
        [Min(0)] public float height, depth;
        [Min(0)] public int decks;
        [Range(0, 1)] public float fill = .65f;
        public int seed = 1;
        public float Height => height > 0 ? Mathf.Max(.25f, height) : family ? family.Height : 2f;
        public override float PathWidth => depth > 0 ? Mathf.Max(.15f, depth) : family ? family.Depth : .6f;
        float HeightScale => Height / Mathf.Max(.001f, family.Height);
        float FirstDeck => Mathf.Clamp(family.firstDeckHeight * HeightScale, family.deck.bounds.size.y, Height);
        float LastDeck => Mathf.Clamp(Height - family.topInset * HeightScale, FirstDeck, Height);
        public int DeckLimit => family && family.deck ? Mathf.Clamp(1 + Mathf.FloorToInt(
            (LastDeck - FirstDeck) / (family.deck.bounds.size.y + .025f)), 1, 12) : 12;
        public int DeckCount => Mathf.Clamp(decks > 0 ? decks : family ? family.levels : 4, 1, DeckLimit);
        public override Material Pavement => null;

        Transform generated;
        internal bool IsGeneratedPart(Transform part) => generated && part.IsChildOf(generated);
        static readonly int finishValue = Shader.PropertyToID("_FinishValue");
        MaterialPropertyBlock finish;

        // Split nodes on a straight line edit the path without changing bay boundaries or stock.
        internal sealed class Run
        {
            public int id, start, end;
            public Vector3 a, b;
            public float trimA, trimB;
            public Vector3 Direction => (b - a).normalized;
        }

        internal static List<Run> Runs(RoadGraph graph, float depth)
        {
            var nodes = graph.nodes.ToDictionary(node => node.id);
            var incident = graph.spans.SelectMany(s => new[] { (s.start.node, s), (s.end.node, s) })
                .ToLookup(pair => pair.node, pair => pair.s);
            var visited = new HashSet<int>();
            var runs = new List<Run>();
            foreach (var span in graph.spans.OrderBy(s => s.id))
            {
                if (!visited.Add(span.id)) continue;
                int Extend(int from, int awayFrom)
                {
                    var direction = (nodes[from].position - nodes[awayFrom].position).normalized;
                    while (incident[from].Count() == 2)
                    {
                        var next = incident[from].FirstOrDefault(s => !visited.Contains(s.id));
                        if (next == null) break;
                        int far = next.start.node == from ? next.end.node : next.start.node;
                        var delta = nodes[far].position - nodes[from].position;
                        if (Vector3.Dot(direction, delta.normalized) < .999999f) break;
                        visited.Add(next.id);
                        from = far;
                    }
                    return from;
                }
                int start = Extend(span.start.node, span.end.node);
                int end = Extend(span.end.node, span.start.node);
                runs.Add(new Run { id = span.id, start = start, end = end,
                    a = nodes[start].position, b = nodes[end].position });
            }
            // Earlier runs own the joint. End the other rack against their side at a right angle.
            var directions = new Dictionary<int, List<Vector3>>();
            float Clearance(int node, Vector3 direction)
            {
                if (!directions.TryGetValue(node, out var others))
                    directions[node] = others = new List<Vector3>();
                float clearance = 0;
                foreach (var other in others)
                {
                    float cosine = Mathf.Clamp(Vector3.Dot(direction, other), -1, 1);
                    float sine = Vector3.Cross(direction, other).magnitude;
                    float setback = cosine <= 0 ? sine : (1 + cosine) / Mathf.Max(.0001f, sine);
                    clearance = Mathf.Max(clearance, .5f * depth * setback);
                }
                others.Add(direction);
                return clearance;
            }
            foreach (var run in runs)
            {
                run.trimA = Clearance(run.start, run.Direction);
                run.trimB = Clearance(run.end, -run.Direction);
            }
            return runs;
        }

        internal static int Fit(float length, float preferred, out float pitch)
        {
            int count = Mathf.FloorToInt(length / (preferred * .78f));
            count = Mathf.Clamp(Mathf.RoundToInt(length / preferred), 0, Mathf.Max(0, count));
            pitch = count > 0 ? Mathf.Min(length / count, preferred * 1.15f) : 0;
            return count;
        }

        internal int FitRun(Run run, out Vector3 first, out float pitch)
        {
            float frameWidth = family.frame.bounds.size.x;
            first = run.a + run.Direction * (run.trimA + frameWidth * .5f);
            float length = Vector3.Distance(run.a, run.b) - run.trimA - run.trimB - frameWidth;
            return Fit(length, family.deck.bounds.size.x + frameWidth, out pitch);
        }

        protected override void BuildSurface()
        {
            ClearParts();
            if (!isActiveAndEnabled || !family || !family.frame || !family.deck || !family.steel || !family.timber || paths.spans.Count == 0) return;
            var root = new GameObject("Shelf parts");
            root.transform.SetParent(transform, false);
            // Only hide the container. Hiding individual renderers also disables Scene view picking.
            root.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            generated = root.transform;
            foreach (var run in Runs(paths, PathWidth)) BuildRun(run);
        }

        void BuildRun(Run run)
        {
            var forward = run.Direction;
            var rotation = Quaternion.LookRotation(Vector3.Cross(forward, Vector3.up), Vector3.up);
            float frameWidth = family.frame.bounds.size.x;
            float nominalDeck = family.deck.bounds.size.x;
            if (frameWidth <= 0 || nominalDeck <= 0) return;
            int count = FitRun(run, out var first, out var pitch);
            if (count == 0) return;
            float clear = pitch - frameWidth;
            float depthScale = PathWidth / Mathf.Max(.001f, family.Depth);
            var frameScale = new Vector3(1, HeightScale, depthScale);
            var deckScale = new Vector3(clear / nominalDeck, 1, depthScale);
            float usableDepth = family.deck.bounds.size.z * depthScale;
            int levels = DeckCount;
            float firstY = FirstDeck;
            float spacing = levels > 1 ? (LastDeck - firstY) / (levels - 1) : 0;
            float innerClearance = spacing - family.deck.bounds.size.y - .025f;
            int slots = Mathf.Max(1, Mathf.RoundToInt(clear / .48f));
            int usableLevels = innerClearance < .09f ? 1 : levels;
            int runSeed = unchecked(seed * 397 ^ run.id);
            var random = new System.Random(runSeed);
            var occupied = family.stock && fill > 0
                ? StockPositions(count * usableLevels * slots, fill, runSeed) : null;
            int stockIndex = 0;
            for (int i = 0; i <= count; i++)
            {
                var at = first + forward * (i * pitch);
                Part(family.frame, family.steel, at, rotation, frameScale, 1);
                if (i == count) break;
                for (int level = 0; level < levels; level++)
                {
                    var top = at + forward * (pitch * .5f) + Vector3.up * (firstY + level * spacing);
                    Part(family.deck, family.timber, top, rotation, deckScale,
                        .96f + .04f * (float)random.NextDouble());
                    int deckSeed = random.Next();
                    float maxHeight = level + 1 == levels ? .48f : innerClearance;
                    if (occupied == null || maxHeight < .09f) continue;
                    StockDeck(top, rotation, clear, usableDepth, maxHeight, new System.Random(deckSeed), occupied, stockIndex, slots);
                    stockIndex += slots;
                }
            }
        }

        static bool[] StockPositions(int total, float fill, int seed)
        {
            // Shuffle the same positions for every fill value. Occupied slots at 25% are a
            // subset of the slots at 50%, even for a short run with only a few spaces.
            var occupied = new bool[total];
            int filled = Mathf.RoundToInt(Mathf.Clamp01(fill) * total);
            for (int i = 0; i < filled; i++) occupied[i] = true;
            var order = new System.Random(seed);
            for (int i = total - 1; i > 0; i--)
            {
                int j = order.Next(i + 1);
                (occupied[i], occupied[j]) = (occupied[j], occupied[i]);
            }
            return occupied;
        }

        void StockDeck(Vector3 top, Quaternion rotation, float width, float depthAvailable, float clearance,
            System.Random random, bool[] occupied, int firstSlot, int slots)
        {
            float pitch = width / slots;
            for (int i = 0; i < slots; i++)
            {
                // Give each position its own random stream. Raising Fill only reveals more boxes.
                int appearanceSeed = random.Next();
                if (!occupied[firstSlot + i]) continue;
                var slot = new System.Random(appearanceSeed);
                float boxWidth = pitch * Mathf.Lerp(.84f, .97f, (float)slot.NextDouble());
                float boxHeight = Mathf.Min(clearance, boxWidth * 1.2f) * Mathf.Lerp(.83f, .99f, (float)slot.NextDouble());
                float boxDepth = Mathf.Min(depthAvailable * .92f, boxWidth * 1.12f);
                float x = (i + .5f) * pitch - width * .5f + (pitch - boxWidth) * ((float)slot.NextDouble() - .5f);
                var box = Instantiate(family.stock, generated);
                SetPose(box, top + rotation * (Vector3.right * x), rotation, new Vector3(boxWidth, boxHeight, boxDepth));
                box.GetComponent<ISeededStock>()?.SetStockSeed(slot.Next());
            }
        }

        void Part(Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, float tint)
        {
            var part = new GameObject(mesh.name);
            part.transform.SetParent(generated, false);
            SetPose(part, position, rotation, scale);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (material.HasProperty(finishValue))
            {
                finish ??= new MaterialPropertyBlock();
                finish.SetFloat(finishValue, tint);
                renderer.SetPropertyBlock(finish);
            }
            var collider = part.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }

        void SetPose(GameObject part, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            part.layer = gameObject.layer;
            part.transform.SetLocalPositionAndRotation(position, rotation);
            part.transform.localScale = scale;
        }

        void OnDisable() => ClearParts();
        void ClearParts()
        {
            if (!generated) return;
            var old = generated.gameObject;
            generated = null;
            old.SetActive(false);
            if (Application.IsPlaying(old)) Destroy(old); else DestroyImmediate(old);
        }
    }
}
