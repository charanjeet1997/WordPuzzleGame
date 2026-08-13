using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Data;

namespace WordPuzzle.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour
    {
        public PanelSettings defaultPanelSettings;
        public UIDocument MainUIDocument { get; private set; }
        public UINavigationManager NavigationManager { get; private set; }

        private readonly Dictionary<string, BaseUI> _viewControllers = new Dictionary<string, BaseUI>();
        private UIPresetOrientation _currentOrientation = UIPresetOrientation.Portrait;

        private void Awake()
        {
            MainUIDocument = GetComponent<UIDocument>();
            if (MainUIDocument != null && MainUIDocument.panelSettings == null && defaultPanelSettings != null)
            {
                MainUIDocument.panelSettings = defaultPanelSettings;
            }

            NavigationManager = new UINavigationManager();
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<UIManager>())
            {
                ServiceLocator.Current.Register<UIManager>(this);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Current.Has<UIManager>())
            {
                ServiceLocator.Current.Unregister<UIManager>();
            }
        }

        public BaseUI ShowView(ViewConfig config)
        {
            if (config == null)
            {
                Debug.LogError("ShowView failed: ViewConfig is null");
                return null;
            }

            if (MainUIDocument != null)
            {
                if (config.visualTreeAsset != null) MainUIDocument.visualTreeAsset = config.visualTreeAsset;
                if (config.panelSettings != null) MainUIDocument.panelSettings = config.panelSettings;
            }

            BaseUI view = GetOrCreateViewController(config);
            NavigationManager.PushView(view);
            view.SetUIPreset(_currentOrientation);
            return view;
        }

        public void HideView(ViewConfig config)
        {
            if (config == null) return;
            NavigationManager.PopView();
        }

        public BaseUI ShowOverlay(ViewConfig config)
        {
            if (config == null) return null;
            return ShowView(config);
        }

        public void HideOverlay(ViewConfig config)
        {
            if (config == null) return;
            HideView(config);
        }

        public void SetUIPreset(UIPresetOrientation orientation)
        {
            _currentOrientation = orientation;
            if (NavigationManager != null && NavigationManager.CurrentView != null)
            {
                NavigationManager.CurrentView.SetUIPreset(orientation);
            }
        }

        private BaseUI GetOrCreateViewController(ViewConfig config)
        {
            if (_viewControllers.TryGetValue(config.viewId, out BaseUI existingView))
            {
                existingView.BindToDocument(MainUIDocument);
                return existingView;
            }

            GameObject viewObj = new GameObject($"ViewController_{config.viewId}");
            viewObj.transform.SetParent(transform);

            Type scriptType = null;
            if (!string.IsNullOrEmpty(config.screenScriptTypeName))
            {
                scriptType = Type.GetType(config.screenScriptTypeName);
            }

            BaseUI viewScript;
            if (scriptType != null && typeof(BaseUI).IsAssignableFrom(scriptType))
            {
                viewScript = (BaseUI)viewObj.AddComponent(scriptType);
            }
            else
            {
                if (config.viewId == "MainMenu") viewScript = viewObj.AddComponent<MainMenuView>();
                else if (config.viewId == "HUD") viewScript = viewObj.AddComponent<HUDView>();
                else if (config.viewId == "PauseOverlay") viewScript = viewObj.AddComponent<PauseOverlayView>();
                else if (config.viewId == "LevelComplete") viewScript = viewObj.AddComponent<LevelCompleteView>();
                else viewScript = viewObj.AddComponent<GenericView>();
            }

            viewScript.config = config;
            viewScript.BindToDocument(MainUIDocument);

            _viewControllers.Add(config.viewId, viewScript);
            return viewScript;
        }
    }
}
