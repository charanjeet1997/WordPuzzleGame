using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Presets;

namespace SingKage.InspectorPro
{
    public static class ComponentDrawer
    {
        private static List<Component> _componentClipboard = new List<Component>();

        public static List<Component> GetComponentClipboard() => _componentClipboard;
        public static void ClearClipboard() => _componentClipboard.Clear();

        public static void DrawFilterBar(ref string searchText)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchText = EditorGUILayout.TextField("", searchText, EditorStyles.toolbarSearchField);
            
            if (GUILayout.Button("Clipboard", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                ComponentClipboardWindow.ShowWindow();
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchText = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        public static void DrawTypeFilters(GameObject selectedObject, ref string selectedTypeFilter, float windowWidth)
        {
            Component[] components = selectedObject.GetComponents<Component>();
            var uniqueTypes = components.Where(c => c != null).Select(c => c.GetType().Name).Distinct().ToList();
            uniqueTypes.Insert(0, "All");

            float currentX = 0;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            foreach (var typeName in uniqueTypes)
            {
                GUIStyle style = new GUIStyle(EditorStyles.miniButton);
                if (selectedTypeFilter == typeName)
                {
                    style.normal.textColor = Color.cyan;
                    style.fontStyle = FontStyle.Bold;
                }

                GUIContent content = new GUIContent(typeName);
                Vector2 size = style.CalcSize(content);
                float buttonWidth = size.x + 10;

                if (currentX + buttonWidth > windowWidth - 20)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    currentX = 0;
                }

                if (GUILayout.Button(typeName, style, GUILayout.Width(buttonWidth)))
                {
                    selectedTypeFilter = typeName;
                }
                currentX += buttonWidth + 4;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        public static void DrawComponents(GameObject selectedObject, string searchText, string typeFilter, Dictionary<Component, bool> foldouts, GUIStyle foldoutStyle)
        {
            Component[] components = selectedObject.GetComponents<Component>();
            string lowerSearchText = searchText.ToLower();

            foreach (Component component in components)
            {
                if (component == null) continue;
                
                string componentName = component.GetType().Name;
                if (!string.IsNullOrEmpty(lowerSearchText) && !componentName.ToLower().Contains(lowerSearchText)) continue;
                if (typeFilter != "All" && componentName != typeFilter) continue;

                DrawSingleComponent(component, foldouts, foldoutStyle);
            }
        }

        private static void DrawSingleComponent(Component component, Dictionary<Component, bool> foldouts, GUIStyle foldoutStyle)
        {
            if (!foldouts.ContainsKey(component)) foldouts.Add(component, true);

            GUILayout.BeginVertical("box");

            Rect headerRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            
            Rect toggleRect = new Rect(headerRect.x, headerRect.y, 16, headerRect.height);
            Rect iconRect = new Rect(headerRect.x + 20, headerRect.y, 16, headerRect.height);
            Rect foldoutRect = new Rect(headerRect.x + 40, headerRect.y, headerRect.width - 120, headerRect.height); 
            
            Rect copyRect = new Rect(headerRect.x + headerRect.width - 80, headerRect.y, 20, headerRect.height);
            Rect presetRect = new Rect(headerRect.x + headerRect.width - 40, headerRect.y, 20, headerRect.height);
            Rect menuRect = new Rect(headerRect.x + headerRect.width - 20, headerRect.y, 20, headerRect.height);

            HandleContextMenu(headerRect, component);
            DrawComponentToggle(component, toggleRect);

            Texture icon = EditorGUIUtility.ObjectContent(component, component.GetType()).image;
            if (icon != null)
            {
                GUI.Label(iconRect, icon);
            }

            bool isExpanded = foldouts[component];
            bool newIsExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, component.GetType().Name, true, foldoutStyle);
            if (newIsExpanded != isExpanded) foldouts[component] = newIsExpanded;

            bool isInClipboard = _componentClipboard.Contains(component);
            GUIContent copyIcon = isInClipboard ? EditorGUIUtility.IconContent("TreeEditor.Trash") : EditorGUIUtility.IconContent("TreeEditor.Duplicate");
            copyIcon.tooltip = isInClipboard ? "Remove from Clipboard" : "Copy to Clipboard";
            
            if (GUI.Button(copyRect, copyIcon, EditorStyles.iconButton))
            {
                if (isInClipboard)
                    _componentClipboard.Remove(component);
                else
                    _componentClipboard.Add(component);
            }

            if (GUI.Button(presetRect, EditorGUIUtility.IconContent("Preset.Context"), EditorStyles.iconButton))
            {
                ShowPresetSelector(component);
            }

            if (GUI.Button(menuRect, EditorGUIUtility.IconContent("_Menu"), EditorStyles.iconButton))
            {
                ShowContextMenu(component);
            }

            if (newIsExpanded)
            {
                Editor componentEditor = Editor.CreateEditor(component);
                if (componentEditor != null)
                {
                    EditorGUI.indentLevel++;
                    componentEditor.OnInspectorGUI();
                    EditorGUI.indentLevel--;
                    Object.DestroyImmediate(componentEditor);
                }

                if (component is Renderer renderer)
                {
                    DrawMaterials(renderer);
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private static void DrawMaterials(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) return;

            GUILayout.Space(5);
            GUILayout.Label("Materials", EditorStyles.boldLabel);

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;

                GUILayout.BeginVertical("box");
                
                bool isExpanded = EditorPrefs.GetBool($"InspectorPro_Mat_{mat.GetInstanceID()}", true);
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, mat.name, true);
                if (newExpanded != isExpanded)
                {
                    EditorPrefs.SetBool($"InspectorPro_Mat_{mat.GetInstanceID()}", newExpanded);
                }

                if (newExpanded)
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUI.BeginChangeCheck();
                    Shader shader = (Shader)EditorGUILayout.ObjectField("Shader", mat.shader, typeof(Shader), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(mat, "Change Shader");
                        mat.shader = shader;
                    }

                    Editor materialEditor = Editor.CreateEditor(mat);
                    if (materialEditor != null)
                    {
                        materialEditor.OnInspectorGUI();
                        Object.DestroyImmediate(materialEditor);
                    }
                    
                    EditorGUI.indentLevel--;
                }

                GUILayout.EndVertical();
            }
        }

        private static void ShowPresetSelector(Component component)
        {
            PresetSelector.ShowSelector(new Object[] { component }, null, true);
        }

        private static void HandleContextMenu(Rect headerRect, Component component)
        {
            Event evt = Event.current;
            if (evt.type == EventType.ContextClick && headerRect.Contains(evt.mousePosition))
            {
                ShowContextMenu(component);
                evt.Use();
            }
        }

        private static void ShowContextMenu(Component component)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Reset"), false, () => {
                Undo.RecordObject(component, "Reset Component");
                if (component is Transform t)
                {
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                }
                else
                {
                    GameObject tempGO = new GameObject();
                    Component tempComp = tempGO.AddComponent(component.GetType());
                    EditorUtility.CopySerialized(tempComp, component);
                    Object.DestroyImmediate(tempGO);
                }
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Move Up"), false, () => ComponentUtility.MoveComponentUp(component));
            menu.AddItem(new GUIContent("Move Down"), false, () => ComponentUtility.MoveComponentDown(component));
            
            menu.AddItem(new GUIContent("Copy Component"), false, () => ComponentUtility.CopyComponent(component));
            menu.AddItem(new GUIContent("Paste Component Values"), false, () => ComponentUtility.PasteComponentValues(component));
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Remove Component"), false, () => Undo.DestroyObjectImmediate(component));
            
            menu.ShowAsContext();
        }

        private static void DrawComponentToggle(Component component, Rect rect)
        {
            System.Action<bool> setEnabled = null;
            bool? isEnabled = null;

            if (component is Behaviour b) { isEnabled = b.enabled; setEnabled = val => b.enabled = val; }
            else if (component is Renderer r) { isEnabled = r.enabled; setEnabled = val => r.enabled = val; }
            else if (component is Collider c) { isEnabled = c.enabled; setEnabled = val => c.enabled = val; }
            else if (component is LODGroup lg) { isEnabled = lg.enabled; setEnabled = val => lg.enabled = val; }
            else if (component is Cloth cl) { isEnabled = cl.enabled; setEnabled = val => cl.enabled = val; }

            if (isEnabled.HasValue)
            {
                bool newIsEnabled = EditorGUI.Toggle(rect, isEnabled.Value);
                if (newIsEnabled != isEnabled.Value)
                {
                    Undo.RecordObject(component, "Toggle Component");
                    setEnabled(newIsEnabled);
                }
            }
        }
    }
}