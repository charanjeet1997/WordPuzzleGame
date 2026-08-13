using UnityEngine;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Models;
using WordPuzzle.UI;

namespace WordPuzzle.Managers
{
    public class WordPuzzleInitializer : MonoBehaviour
    {
        [Header("Initial View Config")]
        public ViewConfig configMainMenu;

        private void Awake()
        {
            if (!ServiceLocator.Current.Has<WondersOfWordGameModel>())
            {
                WondersOfWordGameModel model = new WondersOfWordGameModel();
                ServiceLocator.Current.Register<WondersOfWordGameModel>(model);
            }
        }

        private void Start()
        {
            var uiManager = ServiceLocator.Current.Has<UIManager>() ? ServiceLocator.Current.Get<UIManager>() : null;
            if (uiManager != null && configMainMenu != null)
            {
                uiManager.ShowView(configMainMenu);
            }
        }
    }
}
