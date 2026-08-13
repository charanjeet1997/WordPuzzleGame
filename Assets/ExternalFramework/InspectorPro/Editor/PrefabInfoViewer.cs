using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace SingKage.InspectorPro
{
    public class PrefabInfoViewer : EditorWindow
    {
        private GameObject selectedObject;
        private Vector2 scrollPosition;
        private bool showOverrides = true;

        [MenuItem("Tools/Prefab Info Viewer")]
        public static void ShowWindow()
        {
            GetWindow<PrefabInfoViewer>("Prefab Info");
        }

        private void OnSelectionChange()
        {
            selectedObject = Selection.activeGameObject;
            Repaint();
        }

        private void OnGUI()
        {
            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject to view Prefab info.", MessageType.Info);
                return;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(selectedObject))
            {
                EditorGUILayout.HelpBox("Selected object is not a Prefab instance.", MessageType.Info);
                return;
            }

            DrawPrefabHeader();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            DrawOverrides();
            GUILayout.EndScrollView();
        }

        private void DrawPrefabHeader()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Prefab", GUILayout.Width(50));
            
            if (GUILayout.Button("Open", EditorStyles.toolbarButton))
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedObject);
                PrefabStageUtility.OpenPrefab(assetPath);
            }

            if (GUILayout.Button("Select", EditorStyles.toolbarButton))
            {
                GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selectedObject);
                if (prefabAsset != null)
                {
                    Selection.activeObject = prefabAsset;
                    EditorGUIUtility.PingObject(prefabAsset);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawOverrides()
        {
            showOverrides = EditorGUILayout.Foldout(showOverrides, "Overrides", true);
            if (!showOverrides) return;

            EditorGUI.indentLevel++;

            // Added Components
            List<AddedComponent> addedComponents = PrefabUtility.GetAddedComponents(selectedObject);
            if (addedComponents.Count > 0)
            {
                GUILayout.Label("Added Components", EditorStyles.boldLabel);
                foreach (var added in addedComponents)
                {
                    if (added.instanceComponent != null)
                    {
                        EditorGUILayout.LabelField(added.instanceComponent.GetType().Name, EditorStyles.miniLabel);
                    }
                }
                GUILayout.Space(5);
            }

            // Removed Components
            List<RemovedComponent> removedComponents = PrefabUtility.GetRemovedComponents(selectedObject);
            if (removedComponents.Count > 0)
            {
                GUILayout.Label("Removed Components", EditorStyles.boldLabel);
                foreach (var removed in removedComponents)
                {
                    if (removed.assetComponent != null)
                    {
                        EditorGUILayout.LabelField(removed.assetComponent.GetType().Name, EditorStyles.miniLabel);
                    }
                }
                GUILayout.Space(5);
            }

            // Property Modifications
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(selectedObject);
            if (modifications != null && modifications.Length > 0)
            {
                var grouped = modifications.Where(m => m.target != null).GroupBy(m => m.target);
                
                if (grouped.Any())
                {
                    GUILayout.Label("Modified Properties", EditorStyles.boldLabel);
                    foreach (var group in grouped)
                    {
                        GUILayout.Label(group.Key.GetType().Name, EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
                        foreach (var mod in group)
                        {
                            // Display property path and its new value
                            EditorGUILayout.LabelField(mod.propertyPath, mod.value);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }
            
            EditorGUI.indentLevel--;
            
            GUILayout.Space(10);

            var prefabAssetType = PrefabUtility.GetPrefabAssetType(selectedObject);
            bool isModelPrefab = prefabAssetType == PrefabAssetType.Model;

            EditorGUI.BeginDisabledGroup(isModelPrefab);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply All"))
            {
                PrefabUtility.ApplyPrefabInstance(selectedObject, InteractionMode.UserAction);
            }
            if (GUILayout.Button("Revert All"))
            {
                PrefabUtility.RevertPrefabInstance(selectedObject, InteractionMode.UserAction);
            }
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }
    }
}