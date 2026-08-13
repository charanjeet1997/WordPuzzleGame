using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace SingKage.InspectorPro
{
    public class ComponentClipboardWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        public static void ShowWindow()
        {
            GetWindow<ComponentClipboardWindow>("Clipboard");
        }

        private void OnGUI()
        {
            GUILayout.Label("Component Clipboard", EditorStyles.boldLabel);
            
            var clipboard = ComponentDrawer.GetComponentClipboard();
            GameObject target = Selection.activeGameObject;

            if (clipboard.Count == 0)
            {
                EditorGUILayout.HelpBox("Clipboard is empty. Copy components from Inspector Pro.", MessageType.Info);
                return;
            }

            if (target == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject to paste components.", MessageType.Warning);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All"))
            {
                ComponentDrawer.ClearClipboard();
            }
            
            EditorGUI.BeginDisabledGroup(target == null);
            if (GUILayout.Button("Paste All As New"))
            {
                foreach (var comp in clipboard)
                {
                    if (comp == null) continue;
                    ComponentUtility.CopyComponent(comp);
                    ComponentUtility.PasteComponentAsNew(target);
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            for (int i = clipboard.Count - 1; i >= 0; i--)
            {
                var comp = clipboard[i];
                if (comp == null)
                {
                    clipboard.RemoveAt(i);
                    continue;
                }

                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                
                GUILayout.Label(EditorGUIUtility.ObjectContent(comp, comp.GetType()).image, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(comp.GetType().Name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), EditorStyles.iconButton))
                {
                    clipboard.RemoveAt(i);
                }
                
                GUILayout.EndHorizontal();

                if (target != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Paste As New", EditorStyles.miniButton))
                    {
                        ComponentUtility.CopyComponent(comp);
                        ComponentUtility.PasteComponentAsNew(target);
                    }

                    bool canPasteValue = target.GetComponent(comp.GetType()) != null;
                    EditorGUI.BeginDisabledGroup(!canPasteValue);
                    if (GUILayout.Button("Paste Values", EditorStyles.miniButton))
                    {
                        Component targetComp = target.GetComponent(comp.GetType());
                        if (targetComp != null)
                        {
                            ComponentUtility.CopyComponent(comp);
                            ComponentUtility.PasteComponentValues(targetComp);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.EndScrollView();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}