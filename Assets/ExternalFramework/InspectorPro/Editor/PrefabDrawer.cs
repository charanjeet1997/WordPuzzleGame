using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace SingKage.InspectorPro
{
    public static class PrefabDrawer
    {
        public static void DrawPrefabSection(GameObject selectedObject)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(selectedObject)) return;

            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selectedObject);
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(selectedObject);
            
            if (instanceRoot == null) return;

            // Row 1: Prefab Asset Field
            EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);

            // Row 2: Buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Open", EditorStyles.miniButton))
            {
                string path = AssetDatabase.GetAssetPath(prefabAsset);
                PrefabStageUtility.OpenPrefab(path);
            }

            if (GUILayout.Button("Select", EditorStyles.miniButton))
            {
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
            }

            DrawOverridesDropdown(selectedObject, instanceRoot);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private static void DrawOverridesDropdown(GameObject selectedObject, GameObject instanceRoot)
        {
            var prefabAssetType = PrefabUtility.GetPrefabAssetType(selectedObject);
            if (prefabAssetType == PrefabAssetType.Model) return;

            var addedComponents = PrefabUtility.GetAddedComponents(instanceRoot);
            var removedComponents = PrefabUtility.GetRemovedComponents(instanceRoot);
            var modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
            
            bool hasOverrides = addedComponents.Count > 0 || removedComponents.Count > 0 || (modifications != null && modifications.Length > 0);

            if (EditorGUILayout.DropdownButton(new GUIContent("Overrides"), FocusType.Keyboard, EditorStyles.miniButton))
            {
                var menu = new GenericMenu();

                if (!hasOverrides)
                {
                    menu.AddDisabledItem(new GUIContent("No Overrides"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Apply All"), false, () => PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.UserAction));
                    menu.AddItem(new GUIContent("Revert All"), false, () => PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.UserAction));
                    menu.AddSeparator("");

                    AddModificationItems(menu, modifications, instanceRoot);
                    AddAddedComponentItems(menu, addedComponents);
                    AddRemovedComponentItems(menu, removedComponents);
                }
                menu.ShowAsContext();
            }
        }

        private static void AddModificationItems(GenericMenu menu, PropertyModification[] modifications, GameObject instanceRoot)
        {
            if (modifications == null) return;
            foreach (var mod in modifications)
            {
                if (mod.target == null) continue;
                
                var currentMod = mod;
                menu.AddItem(new GUIContent($"Modified/{currentMod.target.GetType().Name}/{currentMod.propertyPath}"), false, () => 
                {
                    SerializedProperty prop = PrefabUtils.GetSerializedPropertyFromModification(instanceRoot, currentMod);
                    if (prop != null)
                    {
                        PrefabUtility.RevertPropertyOverride(prop, InteractionMode.UserAction);
                    }
                });
            }
        }

        private static void AddAddedComponentItems(GenericMenu menu, List<AddedComponent> addedComponents)
        {
            foreach (var added in addedComponents)
            {
                var currentAdded = added;
                menu.AddItem(new GUIContent($"Added Component/{currentAdded.instanceComponent.GetType().Name}"), false, () => Undo.DestroyObjectImmediate(currentAdded.instanceComponent));
            }
        }

        private static void AddRemovedComponentItems(GenericMenu menu, List<RemovedComponent> removedComponents)
        {
            foreach (var removed in removedComponents)
            {
                var currentRemoved = removed;
                menu.AddItem(new GUIContent($"Removed Component/{currentRemoved.assetComponent.GetType().Name}"), false, () => PrefabUtility.RevertRemovedComponent(currentRemoved.containingInstanceGameObject, currentRemoved.assetComponent, InteractionMode.UserAction));
            }
        }
    }
}