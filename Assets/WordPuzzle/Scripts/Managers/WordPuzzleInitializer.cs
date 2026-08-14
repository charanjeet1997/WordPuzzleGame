using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.UI;
using WordPuzzle.Services;

namespace WordPuzzle.Managers
{
    public class WordPuzzleInitializer : MonoBehaviour
    {
        [Header("View Configurations")]
        public ViewConfig configSplashScreen;
        public ViewConfig configMainMenu;

        private void Awake()
        {
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<IProgressionService>())
            {
                var progObj = new GameObject("ProgressionService");
                progObj.transform.SetParent(transform);
                progObj.AddComponent<ProgressionService>();
            }

            if (!ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                WondersOfWordGameModel model = new WondersOfWordGameModel();
                ServiceLocator.Current.Register<WondersOfWordGameModel>(model);
            }
        }

        private void Start()
        {
            var uiManager = ServiceLocator.Current.Has<UIManager>() ? ServiceLocator.Current.Get<UIManager>() : null;
            if (uiManager != null)
            {
                if (configSplashScreen != null)
                {
                    var splashView = uiManager.ShowView(configSplashScreen) as SplashScreenView;
                    if (splashView != null && configMainMenu != null)
                    {
                        splashView.nextViewConfig = configMainMenu;
                    }
                }
                else if (configMainMenu != null)
                {
                    uiManager.ShowView(configMainMenu);
                }
            }
        }
    }
}
