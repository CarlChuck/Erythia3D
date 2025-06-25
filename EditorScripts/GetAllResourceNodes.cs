#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[System.Serializable]
public class ResourceNodeInfo
{
    public ResourceNode node;
    public string nodeName;
    public string hierarchyPath;
    public HarvestType harvestType;
    public int currentHealth;
    public int maxHealth;
    public bool isActive;
    
    public ResourceNodeInfo(ResourceNode resourceNode)
    {
        node = resourceNode;
        nodeName = resourceNode.name;
        hierarchyPath = GetHierarchyPath(resourceNode.transform);
        harvestType = resourceNode.GetHarvestType();
        currentHealth = (int)resourceNode.GetHealthPercentage() * 100; // Approximate current health
        maxHealth = 100; // Default, could be expanded
        isActive = resourceNode.gameObject.activeInHierarchy;
    }
    
    private string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}

public class GetAllResourceNodes : MonoBehaviour
{
    [Header("ResourceNode Collection")]
    [SerializeField] private List<ResourceNodeInfo> resourceNodeInfos = new List<ResourceNodeInfo>();
    [SerializeField] private bool collectOnStart = true;
    
    [Space]
    [Header("Statistics")]
    [SerializeField] private int totalNodes = 0;
    [SerializeField] private int miningNodes = 0;
    [SerializeField] private int woodcuttingNodes = 0;
    [SerializeField] private int harvestingNodes = 0;
    
    void Start()
    {
        if (collectOnStart)
        {
            CollectResourceNodes();
        }
    }
    
    [ContextMenu("Collect ResourceNodes")]
    public void CollectResourceNodes()
    {
        resourceNodeInfos.Clear();
        
        // Get all ResourceNode components in children (including inactive)
        ResourceNode[] foundNodes = GetComponentsInChildren<ResourceNode>(true);
        
        foreach (ResourceNode node in foundNodes)
        {
            resourceNodeInfos.Add(new ResourceNodeInfo(node));
        }
        
        UpdateStatistics();
        
        Debug.Log($"Collected {resourceNodeInfos.Count} ResourceNodes from '{gameObject.name}' and its children.");
    }
    
    public void ClearCollection()
    {
        resourceNodeInfos.Clear();
        UpdateStatistics();
        Debug.Log("ResourceNode collection cleared.");
    }
    
    private void UpdateStatistics()
    {
        totalNodes = resourceNodeInfos.Count;
        miningNodes = 0;
        woodcuttingNodes = 0;
        harvestingNodes = 0;
        
        foreach (ResourceNodeInfo info in resourceNodeInfos)
        {
            switch (info.harvestType)
            {
                case HarvestType.Mining:
                    miningNodes++;
                    break;
                case HarvestType.Woodcutting:
                    woodcuttingNodes++;
                    break;
                case HarvestType.Harvesting:
                    harvestingNodes++;
                    break;
            }
        }
    }
    
    public List<ResourceNodeInfo> GetResourceNodeInfos()
    {
        return resourceNodeInfos;
    }
    
    public int GetTotalNodeCount()
    {
        return totalNodes;
    }
    
    public int GetNodeCountByType(HarvestType type)
    {
        switch (type)
        {
            case HarvestType.Mining: return miningNodes;
            case HarvestType.Woodcutting: return woodcuttingNodes;
            case HarvestType.Harvesting: return harvestingNodes;
            default: return 0;
        }
    }
}

[CustomEditor(typeof(GetAllResourceNodes))]
public class GetAllResourceNodesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GetAllResourceNodes script = (GetAllResourceNodes)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ResourceNode Collection Tool", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collect ResourceNodes", GUILayout.Height(30)))
        {
            script.CollectResourceNodes();
        }
        
        if (GUILayout.Button("Clear Collection", GUILayout.Height(30)))
        {
            script.ClearCollection();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Display statistics
        EditorGUILayout.LabelField("Collection Statistics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Total Nodes: {script.GetTotalNodeCount()}");
        EditorGUILayout.LabelField($"Mining Nodes: {script.GetNodeCountByType(HarvestType.Mining)}");
        EditorGUILayout.LabelField($"Woodcutting Nodes: {script.GetNodeCountByType(HarvestType.Woodcutting)}");
        EditorGUILayout.LabelField($"Harvesting Nodes: {script.GetNodeCountByType(HarvestType.Harvesting)}");
        
        EditorGUILayout.Space();
        
        // Display detailed list
        List<ResourceNodeInfo> nodeInfos = script.GetResourceNodeInfos();
        if (nodeInfos.Count > 0)
        {
            EditorGUILayout.LabelField("ResourceNode Details", EditorStyles.boldLabel);
            
            for (int i = 0; i < nodeInfos.Count; i++)
            {
                ResourceNodeInfo info = nodeInfos[i];
                
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}. {info.nodeName}", EditorStyles.boldLabel);
                
                // Ping button to highlight in hierarchy
                if (info.node != null && GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(info.node.gameObject);
                    Selection.activeGameObject = info.node.gameObject;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Path:", info.hierarchyPath);
                EditorGUILayout.LabelField("Type:", info.harvestType.ToString());
                EditorGUILayout.LabelField("Health:", $"{info.currentHealth}%");
                EditorGUILayout.LabelField("Active:", info.isActive ? "Yes" : "No");
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                
                if (i < nodeInfos.Count - 1)
                {
                    EditorGUILayout.Space(2);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No ResourceNodes collected. Click 'Collect ResourceNodes' to scan for components.", MessageType.Info);
        }
    }
}

#endif
