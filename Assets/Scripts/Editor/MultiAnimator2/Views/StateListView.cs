using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    using Button = UnityEngine.UIElements.Button;

    public sealed class StateListView : VisualElement
    {
        private readonly MultiAnimatorContext _ctx;
        private readonly ScrollView _list;
        private readonly ToolbarSearchField _search;
        private string _filter = "";

        public StateListView(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-states");

            var header = new VisualElement();
            header.AddToClassList("ma-panel-header");
            var title = new Label("Стейты");
            title.AddToClassList("ma-panel-header__title");
            header.Add(title);
            header.Add(MakeIconButton("＋", "Новый стейт", AddState));
            header.Add(MakeIconButton("⧉", "Дублировать стейт", DuplicateState));
            header.Add(MakeIconButton("🗑", "Удалить стейт", DeleteState));
            Add(header);

            _search = new ToolbarSearchField();
            _search.AddToClassList("ma-states__search");
            _search.RegisterValueChangedCallback(e =>
            {
                _filter = e.newValue ?? "";
                Rebuild();
            });
            Add(_search);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("ma-states__list");
            Add(_list);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _ctx.Session.Changed += OnSessionChanged;
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _ctx.Session.Changed -= OnSessionChanged);
        }

        private static Button MakeIconButton(string text, string tooltip, System.Action action)
        {
            var b = new Button(action) { text = text, tooltip = tooltip, focusable = false };
            b.AddToClassList("ma-icon-button");
            return b;
        }

        private void OnSessionChanged(SessionChange change)
        {
            if ((change & (SessionChange.Root | SessionChange.State | SessionChange.Data)) != 0)
                Rebuild();
        }

        private void Rebuild()
        {
            _list.Clear();
            var config = _ctx.Session.Config;
            if (config == null || config.states == null)
                return;

            var grouped = new HashSet<AnimationStateConfig>();
            if (config.layers != null)
            {
                foreach (var layer in config.layers)
                {
                    if (layer == null)
                        continue;
                    AddGroup(layer.layerName, layer.states, layer.maskParts);
                    if (layer.states != null)
                        grouped.UnionWith(layer.states);
                }
            }

            var rest = new List<AnimationStateConfig>();
            foreach (var s in config.states)
                if (s != null && !grouped.Contains(s))
                    rest.Add(s);

            if (rest.Count > 0 || grouped.Count == 0)
                AddGroup(grouped.Count == 0 ? "Все стейты" : "Без слоя", rest, null);
        }

        private void AddGroup(string title, List<AnimationStateConfig> states, List<string> mask)
        {
            var header = new Label(string.IsNullOrEmpty(title) ? "(слой без имени)" : title);
            header.AddToClassList("ma-states__group");
            if (mask != null && mask.Count > 0)
                header.tooltip = "Маска: " + string.Join(", ", mask);
            _list.Add(header);

            if (states == null)
                return;

            foreach (var s in states)
            {
                if (s == null)
                    continue;
                if (_filter.Length > 0 && (s.stateName ?? "").IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                _list.Add(MakeItem(s));
            }
        }

        private VisualElement MakeItem(AnimationStateConfig state)
        {
            var item = new VisualElement();
            item.AddToClassList("ma-state");
            item.EnableInClassList("ma-state--selected", state == _ctx.Session.State);

            var label = new Label(string.IsNullOrEmpty(state.stateName) ? "(без имени)" : state.stateName);
            label.AddToClassList("ma-state__name");
            item.Add(label);

            var count = new Label(state.parts != null ? state.parts.Count.ToString() : "0") { tooltip = "Дорожек" };
            count.AddToClassList("ma-state__count");
            item.Add(count);

            item.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    _ctx.Session.SelectState(state);
                    if (e.clickCount == 2)
                        BeginRename(item, label, state);
                }
                else if (e.button == 1)
                {
                    _ctx.Session.SelectState(state);
                    ShowContextMenu(item, label, state);
                }
                e.StopPropagation();
            });

            return item;
        }

        private void BeginRename(VisualElement item, Label label, AnimationStateConfig state)
        {
            var field = new TextField { value = state.stateName };
            field.AddToClassList("ma-state__rename");
            label.style.display = DisplayStyle.None;
            item.Insert(0, field);

            bool done = false;
            void Commit(bool apply)
            {
                if (done)
                    return;
                done = true;
                if (apply)
                    StateAssets.RenameState(_ctx.Session.Config, state, field.value.Trim());
                _ctx.Session.Notify(SessionChange.Data);
            }

            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    Commit(true);
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Commit(false);
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => Commit(true));
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        private void ShowContextMenu(VisualElement item, Label label, AnimationStateConfig state)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Переименовать"), false, () => BeginRename(item, label, state));
            menu.AddItem(new GUIContent("Дублировать"), false, DuplicateState);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Удалить"), false, DeleteState);
            menu.ShowAsContext();
        }

        private void AddState()
        {
            var session = _ctx.Session;
            var state = StateAssets.AddState(session.Config, "NewState");
            if (state == null)
                return;
            session.Notify(SessionChange.Data);
            session.SelectState(state);
        }

        private void DuplicateState()
        {
            var session = _ctx.Session;
            if (session.State == null)
                return;
            var state = StateAssets.AddState(session.Config, session.State.stateName, session.State);
            session.Notify(SessionChange.Data);
            session.SelectState(state);
        }

        private void DeleteState()
        {
            var session = _ctx.Session;
            var state = session.State;
            if (state == null)
                return;
            if (!EditorUtility.DisplayDialog("Удалить стейт?",
                    $"Удалить стейт «{state.stateName}»? Клипы останутся, удалится только запись в конфиге.",
                    "Удалить", "Отмена"))
                return;

            _ctx.Preview.SetPreview(false);
            StateAssets.RemoveState(session.Config, state);
            session.ValidateState();
            session.Notify(SessionChange.State | SessionChange.Data | SessionChange.Layout | SessionChange.Selection);
        }
    }
}
