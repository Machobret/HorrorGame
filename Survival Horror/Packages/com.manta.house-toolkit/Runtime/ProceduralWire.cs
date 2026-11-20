using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralWire : MonoBehaviour
    {
        [Min(.001f)] public float diameter = .018f;
        [SerializeField, HideInInspector] internal List<Vector3> nodes = new();
        [Range(1, 8)] public int numberOfWires = 1;
        [System.Serializable]
        internal sealed class Segment
        {
            public int a, b;
            [Range(0, .5f)] public float sag;
            internal Segment(int a, int b, float sag = 0) { this.a=a; this.b=b; this.sag=sag; }
            internal int Other(int n) => n==a ? b : a;
        }
        [SerializeField, HideInInspector] internal List<Segment> segments = new();
        [SerializeField, HideInInspector] bool hasTopology;
        internal void EnsureTopology()
        {
            if (hasTopology || nodes.Count < 2) return;
            if (segments.Count == 0)
                for (int i=1; i<nodes.Count; i++) segments.Add(new Segment(i-1,i));
            hasTopology=true;
        }
        internal bool Connect(int a,int b,float sag=0)
        {
            EnsureTopology();
            if(a==b || a<0 || b<0 || a>=nodes.Count || b>=nodes.Count ||
                segments.Exists(e=>(e.a==a && e.b==b)||(e.a==b && e.b==a))) return false;
            segments.Add(new Segment(a,b,sag));
            return true;
        }
        internal int Degree(int n) => segments.FindAll(e=>e.a==n || e.b==n).Count;

        // Sag is midspan drop divided by horizontal span, measured in world space.
        internal Vector3 Point(Segment edge,float t)
        {
            var start=nodes[edge.a]; var end=nodes[edge.b];
            var span=transform.TransformVector(end-start); span.y=0;
            return Vector3.Lerp(start,end,t)+transform.InverseTransformVector(Vector3.down)*
                (4*t*(1-t)*span.magnitude*Mathf.Clamp(edge.sag,0,.5f));
        }
        internal int Steps(Segment edge)
        {
            if(edge.sag<=0)return 1;
            var span=transform.TransformVector(nodes[edge.b]-nodes[edge.a]);span.y=0;
            return span.sqrMagnitude < 1e-8f ? 1 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(span.magnitude*edge.sag/.004f)),4,64);
        }
        Quaternion lastRotation;
        Vector3 lastScale;
        bool hasSag;
        Mesh mesh;
        bool dirty;
        void OnEnable() => Rebuild();
        void OnValidate() => dirty = true;
#if UNITY_EDITOR
        void Update() { if (dirty || hasSag && (lastRotation!=transform.rotation || lastScale!=transform.lossyScale)) Rebuild(); }
#endif
        void OnDestroy()
        {
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
        internal void Rebuild()
        {
            dirty = false;
            if (!mesh) mesh = new Mesh { name = "Wire", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            EnsureTopology();
            lastRotation=transform.rotation; lastScale=transform.lossyScale;
            hasSag=segments.Exists(e=>e.sag>0);
            if (nodes.Count<2 || segments.Count==0) return;
            var incident=new List<int>[nodes.Count];
            for(int i=0;i<nodes.Count;i++)incident[i]=new List<int>();
            for(int i=0;i<segments.Count;i++)
            {
                var edge=segments[i];
                if(edge.a<0 || edge.b<0 || edge.a>=nodes.Count || edge.b>=nodes.Count || edge.a==edge.b)continue;
                incident[edge.a].Add(i); incident[edge.b].Add(i);
            }
            int strands=Mathf.Clamp(numberOfWires,1,8);
            var frames=new Quaternion[nodes.Count];
            if(strands>1) BuildFrames(incident,frames);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            var used=new bool[segments.Count];
            // Walk degree-two runs together so existing bends retain their continuous tube.
            for(int pass=0;pass<2;pass++)
            for(int n=0;n<nodes.Count;n++)
            {
                if(pass==0 && incident[n].Count==2) continue;
                foreach(int first in incident[n])
                {
                    if(used[first])continue;
                    var run=new List<(int from,int edge)>();
                    int from=n,next=first;
                    while(!used[next])
                    {
                        used[next]=true;
                        run.Add((from,next));from=segments[next].Other(from);
                        if(incident[from].Count!=2)break;
                        next=incident[from][0]==next?incident[from][1]:incident[from][0];
                    }
                    for(int strand=0;strand<strands;strand++)
                    {
                        float angle=2*Mathf.PI*strand/strands;
                        float spacing=strands==1?0:Mathf.Max(diameter,.001f)*.56f/Mathf.Sin(Mathf.PI/strands);
                        var offset=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*spacing;
                        var path=new List<Vector3>();
                        foreach(var item in run)
                        {
                            var edge=segments[item.edge]; int steps=Steps(edge);
                            for(int k=path.Count==0?0:1;k<=steps;k++)
                            {
                                float t=(float)k/steps;
                                if(item.from!=edge.a)t=1-t;
                                var p=Point(edge,t);
                                if(strands>1)p+=Quaternion.Slerp(frames[edge.a],frames[edge.b],t)*offset;
                                if(path.Count==0 || (path[path.Count-1]-p).sqrMagnitude>1e-10f)path.Add(p);
                            }
                        }
                        if(path.Count>=2)AddTube(path,vertices,normals,uv,triangles);
                    }
                }
            }
            mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        }
        void BuildFrames(List<int>[] incident, Quaternion[] frames)
        {
            var visited=new bool[nodes.Count];var queue=new Queue<int>();
            for(int root=0;root<nodes.Count;root++)
            {
                if(visited[root] || incident[root].Count==0)continue;
                var direction=(nodes[segments[incident[root][0]].Other(root)]-nodes[root]).normalized;
                if(direction.sqrMagnitude<.5f)direction=Vector3.forward;
                frames[root]=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right);
                visited[root]=true;queue.Enqueue(root);
                while(queue.Count>0)
                {
                    int n=queue.Dequeue();
                    foreach(int index in incident[n])
                    {
                        int other=segments[index].Other(n);if(visited[other])continue;
                        var forward=frames[n]*Vector3.forward;
                        var axis=(nodes[other]-nodes[n]).normalized;
                        if(incident[other].Count==2)
                        {
                            int onward=incident[other][0]==index?incident[other][1]:incident[other][0];
                            var bisector=(axis+(nodes[segments[onward].Other(other)]-nodes[other]).normalized).normalized;
                            if(bisector.sqrMagnitude>.5f)axis=bisector;
                        }
                        if(Vector3.Dot(axis,forward)<0)axis=-axis;
                        frames[other]=axis.sqrMagnitude>.5f?Quaternion.FromToRotation(forward,axis)*frames[n]:frames[n];
                        visited[other]=true;queue.Enqueue(other);
                    }
                }
            }
        }
        void AddTube(List<Vector3> path,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> triangles)
        {
            const int sides=8;
            int baseVertex=vertices.Count;
            float length = 0, radius = Mathf.Max(.001f, diameter) * .5f;
            var previous = (path[1]-path[0]).normalized;
            var axis = Vector3.Cross(previous, Mathf.Abs(previous.y) < .9f ? Vector3.up : Vector3.right).normalized;
            for (int i=0; i<path.Count; i++)
            {
                var before = i>0 ? (path[i]-path[i-1]).normalized : previous;
                var after = i+1<path.Count ? (path[i+1]-path[i]).normalized : before;
                var tangent = (before+after).normalized;
                if (tangent.sqrMagnitude < .5f) tangent = after;
                axis = (Quaternion.FromToRotation(previous,tangent)*axis).normalized;
                var second = Vector3.Cross(tangent,axis);
                previous = tangent;
                if (i>0) length += Vector3.Distance(path[i],path[i-1]);
                int start=vertices.Count;
                for (int j=0; j<=sides; j++)
                {
                    float a = j*(2*Mathf.PI/sides);
                    var normal = axis*Mathf.Cos(a)+second*Mathf.Sin(a);
                    vertices.Add(path[i]+normal*radius); normals.Add(normal); uv.Add(new Vector2((float)j/sides,length));
                    if (i==0 || j==sides) continue;
                    int b=start+j, c=b-sides-1;
                    triangles.Add(c);triangles.Add(c+1);triangles.Add(b);
                    triangles.Add(c+1);triangles.Add(b+1);triangles.Add(b);
                }
            }
            for (int end=0; end<2; end++)
            {
                int ring=baseVertex+(end==0?0:(path.Count-1)*(sides+1)), start=vertices.Count;
                var normal=end==0?(path[0]-path[1]).normalized:previous;
                for(int j=0;j<sides;j++){vertices.Add(vertices[ring+j]);normals.Add(normal);uv.Add(new Vector2(.5f+.5f*Mathf.Cos(j*2*Mathf.PI/sides),.5f+.5f*Mathf.Sin(j*2*Mathf.PI/sides)));}
                for(int j=1;j<sides-1;j++){triangles.Add(start);triangles.Add(start+j+(end==0?1:0));triangles.Add(start+j+(end==0?0:1));}
            }
        }
    }
}
