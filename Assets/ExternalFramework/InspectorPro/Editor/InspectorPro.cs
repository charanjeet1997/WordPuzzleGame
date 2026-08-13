using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace SingKage.InspectorPro
{
    public class InspectorPro : EditorWindow
    {
        private GameObject _selectedObject;
        private Vector2 _scrollPosition;
        
        private string _searchText = "";
        private string _selectedTypeFilter = "All";
        
        private readonly Dictionary<Component, bool> _componentFoldouts = new Dictionary<Component, bool>();
        private GUIStyle _boldFoldoutStyle;

        [MenuItem("Tools/Inspector Pro")]
        public static void ShowWindow()
        {
            GetWindow<InspectorPro>("Inspector Pro");
        }

        private void OnEnable()
        {
            _selectedObject = Selection.activeGameObject;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnSelectionChange()
        {
            if (_selectedObject != Selection.activeGameObject)
            {
                ResetState();
            }
            _selectedObject = Selection.activeGameObject;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (_selectedObject != null)
            {
                Repaint();
            }
        }

        private void ResetState()
        {
            _componentFoldouts.Clear();
            _selectedTypeFilter = "All";
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (_selectedObject == null)
            {
                DrawEmptyState();
                return;
            }

            DrawHeader();
            
            GUILayout.Space(5);

            PrefabDrawer.DrawPrefabSection(_selectedObject);
            
            ComponentDrawer.DrawFilterBar(ref _searchText);
            ComponentDrawer.DrawTypeFilters(_selectedObject, ref _selectedTypeFilter, position.width);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            ComponentDrawer.DrawComponents(_selectedObject, _searchText, _selectedTypeFilter, _componentFoldouts, _boldFoldoutStyle);
            GUILayout.EndScrollView();

            DrawAddComponentButton();
        }

        private void InitializeStyles()
        {
            if (_boldFoldoutStyle == null)
            {
                _boldFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.HelpBox("Select a GameObject to use Inspector Pro.", MessageType.Info);
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            
            GUIContent iconContent = EditorGUIUtility.ObjectContent(_selectedObject, typeof(GameObject));
            if (GUILayout.Button(iconContent.image, EditorStyles.label, GUILayout.Width(20), GUILayout.Height(20)))
            {
                ShowIconSelector(_selectedObject);
            }

            bool isActive = _selectedObject.activeSelf;
            bool newActive = EditorGUILayout.Toggle(isActive, GUILayout.Width(20));
            if (newActive != isActive)
            {
                Undo.RecordObject(_selectedObject, "Toggle Active State");
                _selectedObject.SetActive(newActive);
            }

            string currentName = _selectedObject.name;
            string newName = EditorGUILayout.TextField(currentName);
            if (newName != currentName)
            {
                Undo.RecordObject(_selectedObject, "Rename GameObject");
                _selectedObject.name = newName;
            }

            GUILayout.EndHorizontal();
        }

        private void ShowIconSelector(GameObject target)
        {
            var iconSelectorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.IconSelector");
            if (iconSelectorType != null)
            {
                var showMethod = iconSelectorType.GetMethod("ShowAtPosition", 
                    BindingFlags.Static | BindingFlags.NonPublic, 
                    null, 
                    new System.Type[] { typeof(Object), typeof(Rect), typeof(bool) }, 
                    null);

                if (showMethod != null)
                {
                    Rect activatorRect = new Rect(Event.current.mousePosition, Vector2.zero);
                    showMethod.Invoke(null, new object[] { target, activatorRect, true });
                }
                else
                {
                    Debug.LogWarning("InspectorPro: Could not find IconSelector.ShowAtPosition method.");
                }
            }
            else
            {
                Debug.LogWarning("InspectorPro: Could not find UnityEditor.IconSelector type.");
            }
        }

        private void DrawAddComponentButton()
        {
            GUILayout.Space(10);
            if (GUILayout.Button("Add Component", GUILayout.Height(30)))
            {
                if (!ShowAddComponentWindow())
                {
                    Debug.LogWarning("InspectorPro: Could not open Add Component window. Please use the standard Inspector.");
                }
            }
        }

        private bool ShowAddComponentWindow()
        {
            try
            {
                var assembly = typeof(EditorWindow).Assembly;
                var type = assembly.GetType("UnityEditor.AddComponentWindow");
                if (type != null)
                {
                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    foreach (var method in methods)
                    {
                        if (method.Name == "Show")
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 2 && 
                                parameters[0].ParameterType == typeof(Rect) && 
                                parameters[1].ParameterType == typeof(GameObject[]))
                            {
                                Rect buttonRect = GUILayoutUtility.GetLastRect();
                                buttonRect.position = GUIUtility.GUIToScreenPoint(buttonRect.position);
                                method.Invoke(null, new object[] { buttonRect, new[] { _selectedObject } });
                                return true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"InspectorPro: Reflection failed: {e.Message}");
            }

            // Fallback to Menu Item
            if (EditorApplication.ExecuteMenuItem("Component/Add...")) return true;
            
            return false;
        }
    }
}