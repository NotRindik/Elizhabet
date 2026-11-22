using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    using Button = UnityEngine.UIElements.Button;

    public sealed class TrackHeadersView : VisualElement
    {
        private readonly MultiAnimatorContext _ctx;
        private readonly List<(VisualElement element, int part)> _partRows = new();

        public TrackHeadersView(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-headers");
            style.width = TrackLayout.HeaderWidth;
        }

        public void Rebuild(List<TrackRow> rows)
        {
            Clear();
            _partRows.Clear();

            foreach (var row in rows)
            {
                var element = row.IsProperty ? BuildPropertyRow(row) : BuildPartRow(row);
                element.style.height = row.Height;
                Add(element);
            }

            UpdateFocus();
        }

        public void UpdateFocus()
        {
            foreach (var (element, part) in _partRows)
                element.EnableInClassList("ma-track--focused", part == _ctx.Session.FocusedPart);
        }

        private VisualElement BuildPartRow(TrackRow row)
        {
            var session = _ctx.Session;
            var part = session.Parts[row.Part];
            int index = row.Part;
            var info = ClipCache.Get(part.clip);
            bool hasAnimator = session.TryGetAnimator(part, out var animator);

            var element = new VisualElement();
            element.AddToClassList("ma-track");
            element.AddToClassList("ma-track--part");
            _partRows.Add((element, index));

            var fold = new Label(session.Expanded.Contains(index) ? "▾" : "▸");
            fold.AddToClassList("ma-track__fold");
            if (info == null || info.Groups.Count == 0)
                fold.style.visibility = Visibility.Hidden;
            fold.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0)
                    return;
                session.ToggleExpanded(index);
                e.StopPropagation();
            });
            element.Add(fold);

            var swatch = new VisualElement();
            swatch.AddToClassList("ma-track__swatch");
            swatch.style.backgroundColor = Palette.ForPart(index);
            element.Add(swatch);

            var name = new Label(string.IsNullOrEmpty(part.partName) ? "(часть?)" : part.partName);
            name.AddToClassList("ma-track__name");
            name.tooltip = hasAnimator
                ? "Клик: выбрать дорожку. Двойной клик: показать объект в иерархии"
                : "Animator с таким именем не найден на объекте";
            name.EnableInClassList("ma-track__name--missing", !hasAnimator);
            element.Add(name);

            var clipButton = new Button(() => Menus.ShowClipMenu(_ctx, index))
            {
                text = part.clip != null ? part.clip.name : "＋ клип",
                focusable = false,
                tooltip = part.clip != null
                    ? $"{part.clip.name}\n{part.clip.frameRate:0.##} fps, {part.clip.length:0.###} c"
                    : "Назначить клип"
            };
            clipButton.AddToClassList("ma-track__clip");
            clipButton.EnableInClassList("ma-track__clip--empty", part.clip == null);
            element.Add(clipButton);

            element.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 1)
                {
                    session.FocusPart(index);
                    ShowPartContextMenu(index);
                    e.StopPropagation();
                    return;
                }
                if (e.button != 0)
                    return;
                session.FocusPart(index);
                if (e.clickCount == 2 && hasAnimator)
                {
                    Selection.activeGameObject = animator.gameObject;
                    EditorGUIUtility.PingObject(animator.gameObject);
                }
            });

            return element;
        }

        private VisualElement BuildPropertyRow(TrackRow row)
        {
            var element = new VisualElement();
            element.AddToClassList("ma-track");
            element.AddToClassList("ma-track--prop");
            element.style.paddingLeft = 26f + Mathf.Min(row.Group.Depth * 10f, 80f);

            var glyph = new Label(row.Group.IsObject ? "■" : "◆");
            glyph.AddToClassList("ma-track__glyph");
            glyph.style.color = Palette.ForPart(row.Part);
            element.Add(glyph);

            var label = new Label(row.Group.Label) { tooltip = row.Group.Label };
            label.AddToClassList("ma-track__prop-name");
            element.Add(label);

            int part = row.Part;
            element.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                    _ctx.Session.FocusPart(part);
            });

            return element;
        }

        private void ShowPartContextMenu(int index)
        {
            var session = _ctx.Session;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Сменить часть…"), false, () => Menus.ShowPartNameMenu(_ctx, index));
            menu.AddItem(new GUIContent("Сменить клип…"), false, () => Menus.ShowClipMenu(_ctx, index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Удалить дорожку из стейта"), false, () =>
            {
                StateAssets.RemovePart(session.State, index);
                session.Expanded.Clear();
                session.FocusPart(-1);
                session.Notify(SessionChange.Data | SessionChange.Layout);
            });
            menu.ShowAsContext();
        }
    }
}
