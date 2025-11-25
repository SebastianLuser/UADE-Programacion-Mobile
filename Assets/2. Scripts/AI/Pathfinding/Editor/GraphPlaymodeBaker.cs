#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

[InitializeOnLoad]
public static class GraphPlaymodeBaker
{
    static GraphPlaymodeBaker()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        var nodes = Object.FindObjectsOfType<GraphNode>();
        if (nodes == null || nodes.Length == 0) return;

        const string path = "Assets/AI/Graphs/BakedGraph.asset";
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var asset = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var index = new Dictionary<GraphNode, int>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++) index[nodes[i]] = i;

        asset.nodePositions.Clear();
        asset.neighbors.Clear();

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            asset.nodePositions.Add(n.transform.position);

            var list = new List<int>(n.neighbors.Count);
            foreach (var nb in n.neighbors)
            {
                if (nb && index.TryGetValue(nb, out int id)) list.Add(id);
            }
            asset.neighbors.Add(new GraphAsset.IntArray { data = list.ToArray() });
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        MyLogger.LogInfo($"GraphPlaymodeBaker: Baked GraphAsset refreshed before Play (nodes={asset.NodeCount}).");
    }
}
#endif
