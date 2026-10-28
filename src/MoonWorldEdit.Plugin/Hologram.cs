using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoonWorldEdit.Plugin;

// Glowing wireframe boxes in the world (requested: show the selection box and what is inside it). Each box's twelve
// edges are thin beams in one mesh, drawn with a copy of the builder's own ghost material so it renders like the
// game's build previews.
internal sealed class Hologram : IDisposable
{
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();

    public Hologram(string name, Material template, Color color)
    {
        root = new GameObject(name);
        UnityEngine.Object.DontDestroyOnLoad(root);
        mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        material = new Material(template) { name = name };
        foreach (var property in new[] { "_BaseColor", "_Color", "_TintColor" })
            if (material.HasProperty(property))
                material.SetColor(property, color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 2f); // glows on the lit fallback shader
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        root.SetActive(false);
    }

    /// <summary>Draws one wireframe box per entry: the eight corners in min/max order (x, then y, then z fastest).</summary>
    public void Draw(IEnumerable<Vector3[]> boxes, float thickness)
    {
        vertices.Clear();
        triangles.Clear();
        foreach (var c in boxes)
        {
            if (c.Length != 8)
                continue;
            // corner index = 4x + 2y + z
            Beam(c[0], c[4], thickness); Beam(c[1], c[5], thickness); Beam(c[2], c[6], thickness); Beam(c[3], c[7], thickness);
            Beam(c[0], c[2], thickness); Beam(c[1], c[3], thickness); Beam(c[4], c[6], thickness); Beam(c[5], c[7], thickness);
            Beam(c[0], c[1], thickness); Beam(c[2], c[3], thickness); Beam(c[4], c[5], thickness); Beam(c[6], c[7], thickness);
        }
        mesh.Clear();
        if (vertices.Count == 0)
        {
            root.SetActive(false);
            return;
        }
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);

    private void Beam(Vector3 from, Vector3 to, float thickness)
    {
        var along = to - from;
        if (along.sqrMagnitude < 1e-8f)
            return;
        var d = along.normalized;
        var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized * thickness;
        var v = Vector3.Cross(d, u).normalized * thickness;
        var start = vertices.Count;
        foreach (var end in new[] { from, to })
        {
            vertices.Add(end - u - v);
            vertices.Add(end + u - v);
            vertices.Add(end + u + v);
            vertices.Add(end - u + v);
        }
        // Four sides (no caps needed for a thin beam), both windings so it shows from any side.
        for (var side = 0; side < 4; side++)
        {
            int a = start + side, b = start + (side + 1) % 4, c = a + 4, e = b + 4;
            triangles.AddRange(new[] { a, b, e, a, e, c, a, e, b, a, c, e });
        }
    }

    public void Dispose()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root);
        if (mesh != null)
            UnityEngine.Object.Destroy(mesh);
        if (material != null)
            UnityEngine.Object.Destroy(material);
    }
}
