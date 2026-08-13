using UnityEngine;
using UnityEditor;

namespace SingKage.InspectorPro
{
    public static class PrefabUtils
    {
        public static SerializedProperty GetSerializedPropertyFromModification(GameObject root, PropertyModification mod)
        {
            Object targetAsset = mod.target;
            Object targetInstance = null;

            if (PrefabUtility.GetCorrespondingObjectFromSource(root) == targetAsset)
            {
                targetInstance = root;
            }
            else
            {
                foreach (var c in root.GetComponents<Component>())
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSource(c) == targetAsset)
                    {
                        targetInstance = c;
                        break;
                    }
                }

                if (targetInstance == null)
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == targetAsset)
                        {
                            targetInstance = child.gameObject;
                            break;
                        }
                        foreach (var c in child.GetComponents<Component>())
                        {
                            if (PrefabUtility.GetCorrespondingObjectFromSource(c) == targetAsset)
                            {
                                targetInstance = c;
                                break;
                            }
                        }
                        if (targetInstance != null) break;
                    }
                }
            }

            if (targetInstance != null)
            {
                SerializedObject so = new SerializedObject(targetInstance);
                return so.FindProperty(mod.propertyPath);
            }
            return null;
        }
    }
}