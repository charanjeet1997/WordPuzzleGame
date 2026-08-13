#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class MakeFolder : EditorWindow
{
    private const string ContainerPath = "Assets/ExternalFramework/MakeFolder/Editor/FolderTemplateContainer.asset";

    private FolderTemplateContainer container;
    private int selectedTemplateIndex;
    private Vector2 scrollPos;
    private string projectName;

    [MenuItem("Tools/MakeFolder/Editor")]
    static void Initialize()
    {
        MakeFolder window = (MakeFolder)EditorWindow.GetWindow(typeof(MakeFolder));
        window.titleContent = new GUIContent("MakeFolder");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    private void OnEnable()
    {
        LoadOrCreateContainer();
    }

    private void LoadOrCreateContainer()
    {
        container = AssetDatabase.LoadAssetAtPath<FolderTemplateContainer>(ContainerPath);
        if (container == null)
        {
            container = ScriptableObject.CreateInstance<FolderTemplateContainer>();
            container.templates.Add(CreateDefaultTemplate());
            AssetDatabase.CreateAsset(container, ContainerPath);
            AssetDatabase.SaveAssets();
        }
        selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, Mathf.Max(0, container.templates.Count - 1));
    }

    private FolderTemplate CreateDefaultTemplate()
    {
        FolderTemplate template = new FolderTemplate { templateName = "Default" };
        template.roots.Add(new FolderNode { name = "Materials" });
        template.roots.Add(new FolderNode { name = "Prefabs" });

        FolderNode scripts = new FolderNode { name = "Scripts" };
        scripts.children.Add(new FolderNode { name = "Models" });
        scripts.children.Add(new FolderNode { name = "Data" });
        scripts.children.Add(new FolderNode { name = "Managers" });
        template.roots.Add(scripts);

        template.roots.Add(new FolderNode { name = "Shaders" });
        template.roots.Add(new FolderNode { name = "Sprites" });
        template.roots.Add(new FolderNode { name = "Models" });
        template.roots.Add(new FolderNode { name = "Sounds" });
        template.roots.Add(new FolderNode { name = "Fonts" });
        template.roots.Add(new FolderNode { name = "Editor" });
        template.roots.Add(new FolderNode { name = "Audio Mixers" });
        template.roots.Add(new FolderNode { name = "Resources" });
        template.roots.Add(new FolderNode { name = "Animator" });
        template.roots.Add(new FolderNode { name = "Animations" });
        return template;
    }

    public void OnGUI()
    {
        if (container == null)
        {
            LoadOrCreateContainer();
        }

        DrawTemplateToolbar();

        EditorGUILayout.Space();
        projectName = EditorGUILayout.TextField("Project Name...", projectName);
        EditorGUILayout.Space();

        FolderTemplate current = GetCurrentTemplate();
        if (current == null)
        {
            EditorGUILayout.HelpBox("No templates yet. Click New to create one.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("+ Add Root Folder"))
        {
            current.roots.Add(new FolderNode());
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        DrawNodeList(current.roots, 0);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(projectName)))
        {
            if (GUILayout.Button("Submit", GUILayout.Height(30)))
            {
                CreateFolders(projectName, current.roots);
            }
        }
    }

    private FolderTemplate GetCurrentTemplate()
    {
        if (container.templates.Count == 0) return null;
        selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, container.templates.Count - 1);
        return container.templates[selectedTemplateIndex];
    }

    private void DrawTemplateToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        string[] names = container.templates.Select(t => t.templateName).ToArray();
        int newIndex = EditorGUILayout.Popup(selectedTemplateIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(150));
        selectedTemplateIndex = names.Length > 0 ? newIndex : 0;

        FolderTemplate current = GetCurrentTemplate();
        if (current != null)
        {
            current.templateName = EditorGUILayout.TextField(current.templateName, GUILayout.Width(150));
        }

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            container.templates.Add(new FolderTemplate { templateName = "New Template" });
            selectedTemplateIndex = container.templates.Count - 1;
        }

        using (new EditorGUI.DisabledScope(current == null))
        {
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                container.templates.Add(CloneTemplate(current));
                selectedTemplateIndex = container.templates.Count - 1;
            }
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                container.templates.RemoveAt(selectedTemplateIndex);
            }
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndHorizontal();
    }

    private FolderTemplate CloneTemplate(FolderTemplate source)
    {
        FolderTemplate clone = new FolderTemplate { templateName = source.templateName + " Copy" };
        clone.roots = CloneNodes(source.roots);
        return clone;
    }

    private List<FolderNode> CloneNodes(List<FolderNode> nodes)
    {
        List<FolderNode> clones = new List<FolderNode>();
        foreach (FolderNode node in nodes)
        {
            FolderNode clone = new FolderNode { name = node.name, toAdd = node.toAdd, expanded = node.expanded };
            clone.children = CloneNodes(node.children);
            clones.Add(clone);
        }
        return clones;
    }

    private void DrawNodeList(List<FolderNode> nodes, int depth)
    {
        int removeIndex = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            FolderNode node = nodes[i];
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 20);

            Rect arrowRect = GUILayoutUtility.GetRect(15, EditorGUIUtility.singleLineHeight, GUILayout.Width(15));
            if (node.children.Count > 0)
            {
                string arrow = node.expanded ? "▼" : "▶";
                if (GUI.Button(arrowRect, arrow, EditorStyles.label))
                {
                    node.expanded = !node.expanded;
                }
            }

            node.toAdd = EditorGUILayout.Toggle(node.toAdd, GUILayout.Width(20));
            node.name = EditorGUILayout.TextField(node.name);

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                node.children.Add(new FolderNode());
                node.expanded = true;
            }
            if (GUILayout.Button("x", GUILayout.Width(24)))
            {
                removeIndex = i;
            }
            EditorGUILayout.EndHorizontal();

            if (node.expanded && node.children.Count > 0)
            {
                DrawNodeList(node.children, depth + 1);
            }
        }

        if (removeIndex >= 0)
        {
            nodes.RemoveAt(removeIndex);
        }
    }

    private void CreateFolders(string rootProjectName, List<FolderNode> roots)
    {
        string rootPath = "Assets/" + rootProjectName;
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            AssetDatabase.CreateFolder("Assets", rootProjectName);
        }
        CreateFolderRecursive(rootPath, roots);
        AssetDatabase.Refresh();
    }

    private void CreateFolderRecursive(string parentPath, List<FolderNode> nodes)
    {
        foreach (FolderNode node in nodes)
        {
            if (!node.toAdd || string.IsNullOrEmpty(node.name)) continue;

            string path = parentPath + "/" + node.name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentPath, node.name);
            }
            if (node.children.Count > 0)
            {
                CreateFolderRecursive(path, node.children);
            }
        }
    }
}

[System.Serializable]
public class FolderNode
{
    public string name = "NewFolder";
    public bool toAdd = true;
    public bool expanded = true;
    public List<FolderNode> children = new List<FolderNode>();
}

[System.Serializable]
public class FolderTemplate
{
    public string templateName = "New Template";
    public List<FolderNode> roots = new List<FolderNode>();
}

public class FolderTemplateContainer : ScriptableObject
{
    public List<FolderTemplate> templates = new List<FolderTemplate>();
}
#endif
