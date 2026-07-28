using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal sealed class SettingsNavController
    {
        private readonly VisualElement _container;
        private readonly VisualElement _slotA;
        private readonly VisualElement _slotB;
        private readonly Stack<VisualElement> _stack = new Stack<VisualElement>();
        private VisualElement _rootPage;
        private VisualElement _currentPage;
        private bool _animating;
        private bool _isPop;

        private const long AnimMs = BiomeUI.PageMotionMs;

        internal int Depth => _stack.Count;

        internal SettingsNavController(VisualElement hostRoot)
        {
            var viewport = new VisualElement();
            viewport.AddToClassList("nav-viewport");

            _container = new VisualElement();
            _container.AddToClassList("nav-container");

            _slotA = new VisualElement();
            _slotA.AddToClassList("nav-slot");
            _slotA.AddToClassList("nav-slot-a");

            _slotB = new VisualElement();
            _slotB.AddToClassList("nav-slot");
            _slotB.AddToClassList("nav-slot-b");

            _container.Add(_slotA);
            _container.Add(_slotB);
            viewport.Add(_container);
            hostRoot.Add(viewport);

            _container.RegisterCallback<DetachFromPanelEvent>(_ => _animating = false);

            hostRoot.focusable = true;
            hostRoot.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape && _stack.Count > 0)
                {
                    Pop();
                    e.StopPropagation();
                }
            });
        }

        internal void SetRoot(VisualElement homePage)
        {
            _slotA.Clear();
            _slotA.Add(homePage);
            _stack.Clear();
            _rootPage = homePage;
            _currentPage = homePage;
        }

        internal void Push(VisualElement page)
        {
            if (_animating) return;
            if (_currentPage == null) { SetRoot(page); return; }
            _isPop = false;
            _animating = true;
            _stack.Push(_currentPage);
            _currentPage = page;

            _slotB.Clear();
            _slotB.Add(page);

            _container.AddToClassList("nav-no-transition");
            _container.style.translate = new Translate(Length.Percent(0), Length.Percent(0));

            _container.schedule.Execute(() =>
            {
                _container.RemoveFromClassList("nav-no-transition");
                _container.style.translate = new Translate(Length.Percent(-50), Length.Percent(0));
            });

            _container.schedule.Execute(() => FinishTransition()).ExecuteLater(AnimMs + 50);
        }

        internal void Pop()
        {
            if (_animating || _stack.Count == 0) return;
            _isPop = true;
            _animating = true;

            var prev = _stack.Pop();

            _slotB.Clear();
            _slotB.Add(_currentPage); // current page moves to slotB (auto-removes from slotA)

            _currentPage = prev;
            _slotA.Clear();
            _slotA.Add(prev);

            _container.AddToClassList("nav-no-transition");
            _container.style.translate = new Translate(Length.Percent(-50), Length.Percent(0));

            _container.schedule.Execute(() =>
            {
                _container.RemoveFromClassList("nav-no-transition");
                _container.style.translate = new Translate(Length.Percent(0), Length.Percent(0));
            });

            _container.schedule.Execute(() => FinishTransition()).ExecuteLater(AnimMs + 50);
        }

        internal void PopToRoot()
        {
            _animating = false;
            if (_stack.Count == 0) return;
            _stack.Clear();
            _slotA.Clear();
            if (_rootPage != null) _slotA.Add(_rootPage);
            _currentPage = _rootPage;
            _container.AddToClassList("nav-no-transition");
            _container.style.translate = new Translate(Length.Percent(0), Length.Percent(0));
            _container.schedule.Execute(() =>
                _container.RemoveFromClassList("nav-no-transition"));
        }

        private void FinishTransition()
        {
            if (_isPop)
            {
                _slotB.Clear();
            }
            else if (_slotB.childCount > 0)
            {
                var page = _slotB[0];
                _slotB.Remove(page);
                _slotA.Clear();
                _slotA.Add(page);
            }
            else
            {
                _slotB.Clear();
            }

            _container.AddToClassList("nav-no-transition");
            _container.style.translate = new Translate(Length.Percent(0), Length.Percent(0));
            _container.schedule.Execute(() =>
                _container.RemoveFromClassList("nav-no-transition"));

            _animating = false;
        }
    }
}
