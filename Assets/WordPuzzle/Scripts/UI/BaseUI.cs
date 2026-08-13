using UnityEngine;
using UnityEngine.UIElements;
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

        public virtual void Show()
        {
            if (mainUIDocument != null && config != null && config.visualTreeAsset != null)
            {
                mainUIDocument.visualTreeAsset = config.visualTreeAsset;
                rootElement = mainUIDocument.rootVisualElement;
                if (rootElement != null)
                {
                    rootElement.style.display = DisplayStyle.Flex;
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
