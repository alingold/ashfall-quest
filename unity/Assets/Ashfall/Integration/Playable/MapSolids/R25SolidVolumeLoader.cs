#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using Ashfall.Integration.Playable.World;

namespace Ashfall.Integration.Playable.MapSolids
{
    // Source reference extraction is separate from the preserved Afterlife r6 loader.
    public sealed class R25SolidVolumeLoader
    {
        public const double BuildBudgetMilliseconds = 3;
        sealed class Part
        {
            public R25SolidDefinition Definition;
            public readonly List<Vector3> Points = new List<Vector3>();
            public int Read;
        }
        struct Reference { public Part Part; public int Triangle; }
        readonly List<Part> parts = new List<Part>();
        readonly Dictionary<int,List<Reference>> byChunk = new Dictionary<int,List<Reference>>();
        readonly List<MeshCollider> colliders = new List<MeshCollider>();
        readonly string map;
        GameObject container;
        bool abandoned;
        public int Count { get { return colliders.Count; } }
        public R25SolidVolumeLoader(R25MapSolids data, string map)
        {
            this.map = map;
            foreach (var definition in data.Definitions)
            {
                var part = new Part { Definition = definition }; parts.Add(part);
                for (int i = 0; i < definition.References.Length; i += 2)
                {
                    int chunk = definition.References[i]; List<Reference> list;
                    if (!byChunk.TryGetValue(chunk,out list)) { list = new List<Reference>(); byChunk.Add(chunk,list); }
                    list.Add(new Reference { Part = part, Triangle = definition.References[i+1] });
                }
            }
        }
        public static string GeometryHash(Stream stream)
        {
            long position = stream.Position;
            try { stream.Position = 0; using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream)); }
            finally { stream.Position = position; }
        }
        static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        static void Need(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
        static bool Finite(float v) { return !Single.IsNaN(v) && !Single.IsInfinity(v); }
        void Abandon(Exception error)
        {
            if (abandoned) return;
            abandoned = true;
            if (container != null) { container.SetActive(false); UnityEngine.Object.Destroy(container); }
            colliders.Clear(); parts.Clear(); byChunk.Clear();
            Warn(map,error.Message);
        }
        public static void Warn(string map, string reason)
        {
            Debug.LogWarning("[R25 solids] " + map + ": " + reason.Replace('\r',' ').Replace('\n',' ') + "; map continues without solids");
        }
        public void ReadChunk(int chunk, int material, int flags, bool visible, int owner, Vector3[] positions, int[] indices)
        {
            if (abandoned) return; List<Reference> selected;
            if (!byChunk.TryGetValue(chunk,out selected)) return;
            try
            {
                Need(flags == 1 && visible && owner < 0, "nonstatic source geometry");
                foreach (var r in selected)
                {
                    Need((r.Part.Definition.Material < 0 || r.Part.Definition.Material == material) && r.Triangle < indices.Length / 3, "triangle/material range");
                    for (int j = 0; j < 3; j++)
                    {
                        var point = positions[indices[r.Triangle*3+j]];
                        Need(Finite(point.x) && Finite(point.y) && Finite(point.z), "nonfinite source vertex");
                        r.Part.Points.Add(point);
                    }
                    r.Part.Read++;
                }
                byChunk.Remove(chunk);
            }
            catch (Exception error) { Abandon(error); }
        }
        void BuildPart(Part part, List<UnityEngine.Object> owned)
        {
            var d = part.Definition; Need(part.Read == d.Triangles, "missing source triangles");
            // Match offline raw little-endian float32 bytes, before signed-zero normalization.
            byte[] bytes = new byte[part.Points.Count * 12];
            for (int i = 0; i < part.Points.Count; i++)
            {
                var p = part.Points[i]; float[] xyz = { p.x,p.y,p.z };
                for (int j = 0; j < 3; j++) { byte[] f = BitConverter.GetBytes(xyz[j]); if (!BitConverter.IsLittleEndian) Array.Reverse(f); Buffer.BlockCopy(f,0,bytes,i*12+j*4,4); }
            }
            using (var sha = SHA256.Create()) Need(Hex(sha.ComputeHash(bytes)) == d.PositionsHash, "exact source positions hash");
            var vertices = new List<Vector3>(); var indices = new int[part.Points.Count]; var lookup = new Dictionary<Vector3,int>();
            for (int i = 0; i < indices.Length; i++)
            {
                var p = part.Points[i]; p = new Vector3(p.x+0f,p.y+0f,p.z+0f); int index;
                if (!lookup.TryGetValue(p,out index)) { index = vertices.Count; lookup.Add(p,index); vertices.Add(p); }
                indices[i] = index;
            }
            Need(vertices.Count == d.Vertices && vertices.Count <= 255, "source vertex identity");
            Vector3 origin = vertices[0]; for (int i = 0; i < vertices.Count; i++) vertices[i] -= origin;
            var mesh = new Mesh { name = d.Id + " exact closed source surface" };
            owned.Add(mesh); // Registered before cooking: map cancellation/failure also destroys meshes.
            mesh.SetVertices(vertices); mesh.triangles = indices; mesh.RecalculateBounds();
            var node = new GameObject(d.Id); node.layer = R23PlayableMap.PlayerOnlyLayer;
            node.transform.SetParent(container.transform,false); node.transform.localPosition = origin;
            var collider = node.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = true;
            Need(collider.sharedMesh != null, "convex cooking produced no mesh");
            colliders.Add(collider); part.Points.Clear();
        }
        public IEnumerator Build(Transform parent, List<UnityEngine.Object> owned)
        {
            if (abandoned) yield break;
            try
            {
                Need(byChunk.Count == 0, "incomplete source coverage");
                container = new GameObject("R25 player-only solids"); container.SetActive(false);
                container.layer = R23PlayableMap.PlayerOnlyLayer; container.transform.SetParent(parent,false);
            }
            catch (Exception error) { Abandon(error); }
            if (abandoned) yield break;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            foreach (var part in parts)
            {
                try { BuildPart(part,owned); }
                catch (Exception error) { Abandon(error); }
                if (abandoned) yield break;
                if (timer.Elapsed.TotalMilliseconds >= BuildBudgetMilliseconds) { yield return null; timer.Restart(); }
            }
            container.SetActive(true); parts.Clear(); byChunk.Clear();
        }
    }
}
#endif
