using System.Collections.Generic;

namespace WordPuzzle.UI
{
    public class UINavigationManager
    {
        private readonly Stack<BaseUI> _historyStack = new Stack<BaseUI>();

        public void PushView(BaseUI view)
        {
            if (view == null) return;

            if (_historyStack.Count > 0)
            {
                BaseUI previousView = _historyStack.Peek();
                if (view.config != null && view.config.shouldHidePreviousUI)
                {
                    previousView.Hide();
                }
            }

            _historyStack.Push(view);
            view.Show();
        }

        public void PopView()
        {
            if (_historyStack.Count == 0) return;

            BaseUI currentView = _historyStack.Pop();
            currentView.Hide();

            if (_historyStack.Count > 0)
            {
                BaseUI previousView = _historyStack.Peek();
                previousView.Show();
            }
        }

        public BaseUI CurrentView => GetCurrentView();

        public BaseUI GetCurrentView()
        {
            return _historyStack.Count > 0 ? _historyStack.Peek() : null;
        }

        public void ClearHistory()
        {
            while (_historyStack.Count > 0)
            {
                BaseUI view = _historyStack.Pop();
                view.Hide();
            }
        }
    }
}
