using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    public sealed class MultiAnimator2Window : EditorWindow
    {
        [SerializeField] private bool _locked;

        private MultiAnimatorContext _ctx;
        private VisualElement _main;
        private Label _empty;

        [MenuItem("Window/Multi Animator 2")]
        public static void Open() => GetWindow<MultiAnimator2Window>("Multi Animator 2");

        private void OnEnable()
        {
            _ctx = new MultiAnimatorContext();
            _ctx.Message += text => ShowNotification(new GUIContent(text), 1.0);
            _ctx.Session.Changed += OnSessionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            if (_ctx == null)
                return;
            _ctx.Session.Changed -= OnSessionChanged;
            _ctx.Dispose();
            _ctx = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("ma-root");
            var sheet = LoadStyleSheet();
            if (sheet != null)
                root.styleSheets.Add(sheet);

            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            root.Add(new ToolbarView(_ctx));

            _main = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            _main.AddToClassList("ma-split");
            var right = new TwoPaneSplitView(1, 290f, TwoPaneSplitViewOrientation.Horizontal);
            right.AddToClassList("ma-split");
            right.Add(new TimelineView(_ctx));
            right.Add(new InspectorView(_ctx));
            _main.Add(new StateListView(_ctx));
            _main.Add(right);
            root.Add(_main);

            _empty = new Label("Выбери объект с AnimationComposerTag в сцене или префабе");
            _empty.AddToClassList("ma-empty");
            _empty.AddToClassList("ma-empty--window");
            root.Add(_empty);

            OnSelectionChanged();
            UpdateEmpty();
        }

        private void ShowButton(Rect rect)
        {
            bool locked = GUI.Toggle(rect, _locked, GUIContent.none, "IN LockButton");
            if (locked == _locked)
                return;
            _locked = locked;
            if (!_locked)
                OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (_ctx == null)
                return;

            var session = _ctx.Session;
            if (_locked && session.Tag != null)
                return;

            var go = Selection.activeGameObject;
            if (go == null)
                return;

            var tag = go.GetComponent<AnimationComposerTag>();
            if (tag == null)
                tag = go.GetComponentInParent<AnimationComposerTag>(true);
            if (tag == null || tag == session.Tag)
                return;

            _ctx.Recorder.SetRecording(false);
            _ctx.Preview.SetPreview(false);
            session.SetTag(tag);
        }

        private void OnHierarchyChanged()
        {
            if (_ctx == null)
                return;
            var session = _ctx.Session;
            if (session.Tag == null && !ReferenceEquals(session.Tag, null))
                session.SetTag(null);
        }

        private void OnSessionChanged(SessionChange change)
        {
            if ((change & SessionChange.Root) != 0)
                UpdateEmpty();
        }

        private void UpdateEmpty()
        {
            if (_main == null)
                return;
            bool hasTag = _ctx.Session.Tag != null;
            _main.style.display = hasTag ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = hasTag ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (IsTextInput(evt.target as VisualElement))
                return;
            rootVisualElement.schedule.Execute(() =>
            {
                if (!IsTextInput(rootVisualElement.focusController?.focusedElement as VisualElement))
                    rootVisualElement.Focus();
            });
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.None || _ctx.Session.Tag == null)
                return;

            if (evt.actionKey && evt.keyCode == KeyCode.S)
            {
                _ctx.Save();
                evt.StopPropagation();
                return;
            }

            if (IsTextInput(rootVisualElement.focusController?.focusedElement as VisualElement))
                return;

            if (!HandleHotkey(evt))
                return;

            evt.StopPropagation();
        }

        private bool HandleHotkey(KeyDownEvent evt)
        {
            var session = _ctx.Session;

            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    _ctx.Preview.TogglePlay();
                    return true;
                case KeyCode.Home:
                    session.SetTime(0f);
                    return true;
                case KeyCode.End:
                    session.SetTime(session.Length);
                    return true;
                case KeyCode.LeftArrow:
                    if (evt.altKey) _ctx.StepKey(-1);
                    else _ctx.StepFrame(evt.shiftKey ? -10 : -1);
                    return true;
                case KeyCode.RightArrow:
                    if (evt.altKey) _ctx.StepKey(1);
                    else _ctx.StepFrame(evt.shiftKey ? 10 : 1);
                    return true;
                case KeyCode.R when !evt.actionKey:
                    _ctx.Recorder.Toggle();
                    return true;
                case KeyCode.K when !evt.actionKey:
                    _ctx.AddKeysAtPlayhead();
                    return true;
                case KeyCode.F when !evt.actionKey:
                    _ctx.RequestFrameAll();
                    return true;
                case KeyCode.C when evt.actionKey:
                    _ctx.CopySelectedKeys();
                    return true;
                case KeyCode.V when evt.actionKey:
                    _ctx.Paste();
                    return true;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _ctx.DeleteSelectedKeys();
                    return true;
                case KeyCode.Escape:
                    session.Keys.Clear();
                    session.Notify(SessionChange.Selection);
                    return true;
            }
            return false;
        }

        private static bool IsTextInput(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
                if (e.ClassListContains("unity-base-text-field"))
                    return true;
            return false;
        }

        private static StyleSheet LoadStyleSheet()
        {
            foreach (var guid in AssetDatabase.FindAssets("MultiAnimator2 t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/MultiAnimator2.uss"))
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return null;
        }
    }
}
