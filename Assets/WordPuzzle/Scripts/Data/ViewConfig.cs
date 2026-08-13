using UnityEngine;
using UnityEngine.UIElements;

namespace WordPuzzle.Data
{
    [CreateAssetMenu(fileName = "ViewConfig_", menuName = "WordPuzzle/UI/ViewConfig")]
    public class ViewConfig : ScriptableObject
    {
        [Header("View Identifier")]
        public string viewId;

        [Header("UI Toolkit Assets")]
        public VisualTreeAsset visualTreeAsset;
        public PanelSettings panelSettings;

        [Header("Screen Script Type")]
        public string screenScriptTypeName;

        [Header("Navigation Rules")]
        public bool shouldHidePreviousUI = true;

        [Header("Orientation Settings")]
        public UIPresetOrientation defaultOrientation = UIPresetOrientation.Portrait;
    }
}
