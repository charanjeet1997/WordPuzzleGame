using UnityEngine;
using UnityEngine.UIElements;
using WordPuzzle.Services;
using WordPuzzle.Data;

namespace WordPuzzle.UI
{
    public abstract class BaseUI : MonoBehaviour
    {
        [HideInInspector] public UIDocument mainUIDocument;
        [HideInInspector] public VisualElement rootElement;

        public ViewConfig config;
        public bool IsVisible { get; protected set; }

        public virtual void BindToDocument(UIDocument sharedUIDocument)
        {
            mainUIDocument = sharedUIDocument;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnEnable() => LayoutService.LayoutChanged += ApplyLayoutClass;

        protected virtual void OnDisable() => LayoutService.LayoutChanged -= ApplyLayoutClass;

        /// <summary>Tags the root with `layout--portrait` or `layout--landscape`.</summary>
        private void ApplyLayoutClass(ScreenLayout layout)
        {
            if (rootElement == null) return;

            rootElement.EnableInClassList("layout--portrait", layout == ScreenLayout.Portrait);
            rootElement.EnableInClassList("layout--landscape", layout == ScreenLayout.Landscape);
        }

        public virtual void Show()
        {
            if (mainUIDocument != null && config != null && config.visualTreeAsset != null)
            {
                mainUIDocument.visualTreeAsset = config.visualTreeAsset;
                rootElement = mainUIDocument.rootVisualElement;
                if (rootElement != null)
                {
                    rootElement.style.display = DisplayStyle.Flex;

                    // Every screen gets the orientation as a class, so layout differences live
                    // in USS next to the rules they modify rather than in each view's code.
                    ApplyLayoutClass(LayoutService.Current);
                }
            }
            IsVisible = true;
            OnShow();
        }

        public virtual void Hide()
        {
            if (rootElement != null)
            {
                rootElement.style.display = DisplayStyle.None;
            }
            IsVisible = false;
            OnHide();
        }

        public virtual void SetUIPreset(UIPresetOrientation orientation)
        {
            if (rootElement == null && mainUIDocument != null) rootElement = mainUIDocument.rootVisualElement;
            if (rootElement == null) return;

            VisualElement targetContainer = rootElement.Q<VisualElement>(className: "root-container") ?? rootElement;

            if (orientation == UIPresetOrientation.Portrait)
            {
                rootElement.RemoveFromClassList("landscape-preset");
                rootElement.AddToClassList("portrait-preset");
                targetContainer.RemoveFromClassList("landscape-preset");
                targetContainer.AddToClassList("portrait-preset");
            }
            else
            {
                rootElement.RemoveFromClassList("portrait-preset");
                rootElement.AddToClassList("landscape-preset");
                targetContainer.RemoveFromClassList("portrait-preset");
                targetContainer.AddToClassList("landscape-preset");
            }
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }

    public class GenericView : BaseUI { }
}
