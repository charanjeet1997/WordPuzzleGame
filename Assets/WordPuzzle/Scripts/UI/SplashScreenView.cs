using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using ServiceLocatorFramework;
using WordPuzzle.Data;
using WordPuzzle.Managers;

namespace WordPuzzle.UI
{
    public class SplashScreenView : BaseUI
    {
        private VisualElement _splashRoot;
        private Label _statusLabel;
        private Label _percentLabel;
        private VisualElement _loadingBarFill;

        [Header("Splash Configuration")]
        public float splashDuration = 2.0f;
        public ViewConfig nextViewConfig;

        private Coroutine _splashCoroutine;
        private bool _isCompleted = false;

        protected override void OnInitialize()
        {
        }

        protected override void OnShow()
        {
            if (rootElement == null) return;

            _splashRoot = rootElement.Q<VisualElement>("splash-root");
            _statusLabel = rootElement.Q<Label>("lbl-status");
            _percentLabel = rootElement.Q<Label>("lbl-percent");
            _loadingBarFill = rootElement.Q<VisualElement>("loading-bar-fill");

            if (_splashRoot != null)
            {
                _splashRoot.RemoveFromClassList("splash-container--hidden");
            }

            if (_splashCoroutine != null)
            {
                StopCoroutine(_splashCoroutine);
            }

            _isCompleted = false;
            _splashCoroutine = StartCoroutine(AnimateSplashSequence());

            rootElement.RegisterCallback<ClickEvent>(OnClickAnywhere);
        }

        protected override void OnHide()
        {
            if (_splashCoroutine != null)
            {
                StopCoroutine(_splashCoroutine);
                _splashCoroutine = null;
            }

            if (rootElement != null)
            {
                rootElement.UnregisterCallback<ClickEvent>(OnClickAnywhere);
            }
        }

        private void OnClickAnywhere(ClickEvent evt)
        {
            if (_isCompleted)
            {
                TransitionToNextView();
            }
        }

        private IEnumerator AnimateSplashSequence()
        {
            float elapsed = 0f;

            string[] statusSteps = new string[]
            {
                "PREPARING PUZZLES...",
                "LOADING DICTIONARY...",
                "POLISHING WONDERS...",
                "READY TO EXPLORE!"
            };

            while (elapsed < splashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / splashDuration);

                // Smooth ease out curve
                float easedProgress = 1f - Mathf.Pow(1f - progress, 2.5f);

                int stepIndex = Mathf.Clamp((int)(progress * statusSteps.Length), 0, statusSteps.Length - 1);

                if (_statusLabel != null)
                {
                    _statusLabel.text = statusSteps[stepIndex];
                }

                if (_percentLabel != null)
                {
                    _percentLabel.text = $"{(int)(easedProgress * 100f)}%";
                }

                if (_loadingBarFill != null)
                {
                    _loadingBarFill.style.width = new Length(easedProgress * 100f, LengthUnit.Percent);
                }

                yield return null;
            }

            if (_percentLabel != null) _percentLabel.text = "100%";
            if (_loadingBarFill != null) _loadingBarFill.style.width = new Length(100f, LengthUnit.Percent);
            if (_statusLabel != null) _statusLabel.text = "WELCOME!";
            _isCompleted = true;

            yield return new WaitForSecondsRealtime(0.35f);

            if (_splashRoot != null)
            {
                _splashRoot.AddToClassList("splash-container--hidden");
            }

            yield return new WaitForSecondsRealtime(0.35f);

            TransitionToNextView();
        }

        private void TransitionToNextView()
        {
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<UIManager>())
            {
                var uiManager = ServiceLocator.Current.Get<UIManager>();
                if (nextViewConfig != null)
                {
                    uiManager.ShowView(nextViewConfig);
                }
                else
                {
                    // Fallback to MainMenu
                    var mainMenuConfig = Resources.Load<ViewConfig>("Configs/ViewConfig_MainMenu");
                    if (mainMenuConfig != null)
                    {
                        uiManager.ShowView(mainMenuConfig);
                    }
                }
            }
        }
    }
}
