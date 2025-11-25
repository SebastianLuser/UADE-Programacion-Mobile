#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class GraphBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var nodes = Object.FindObjectsOfType<GraphNode>();
        if (nodes == null || nodes.Length == 0)
        {
            MyLogger.LogInfo("GraphBuildProcessor: No GraphNode found; assuming you already have a GraphAsset assigned.");
            return;
        }

        var index = new Dictionary<GraphNode, int>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++) index[nodes[i]] = i;

        const string path = "Assets/AI/Graphs/BakedGraph.asset";
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var asset = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.nodePositions.Clear();
        asset.neighbors.Clear();

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            asset.nodePositions.Add(n.transform.position);

            var neighIdx = new List<int>(n.neighbors.Count);
            foreach (var nb in n.neighbors)
            {
                if (nb && index.TryGetValue(nb, out int id)) neighIdx.Add(id);
            }
            asset.neighbors.Add(new GraphAsset.IntArray { data = neighIdx.ToArray() });
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        MyLogger.LogInfo($"GraphBuildProcessor: Baked GraphAsset at {path} (nodes={asset.NodeCount}). " +
                    "Tip: poner el contenedor de nodos con tag 'EditorOnly' para excluirlos del build.");
    }
}
#endif
