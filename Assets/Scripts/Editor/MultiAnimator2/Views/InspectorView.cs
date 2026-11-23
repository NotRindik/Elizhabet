using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    using Button = UnityEngine.UIElements.Button;

    public sealed class InspectorView : VisualElement
    {
        private const int MaxKeyRows = 16;

        private readonly MultiAnimatorContext _ctx;
        private readonly ScrollView _scroll;
        private bool _pending;

        public InspectorView(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-inspector");

            var header = new VisualElement();
            header.AddToClassList("ma-panel-header");
            var title = new Label("Инспектор");
            title.AddToClassList("ma-panel-header__title");
            header.Add(title);
            Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("ma-inspector__scroll");
            Add(_scroll);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _ctx.Session.Changed += OnSessionChanged;
                RequestRebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _ctx.Session.Changed -= OnSessionChanged);
        }

        private void OnSessionChanged(SessionChange change)
        {
            if ((change & ~SessionChange.Time) != 0)
                RequestRebuild();
        }

        private void RequestRebuild()
        {
            if (_pending)
                return;
            _pending = true;
            schedule.Execute(() =>
            {
                _pending = false;
                Rebuild();
            });
        }

        private void Rebuild()
        {
            _scroll.Clear();
            var session = _ctx.Session;
            if (!session.HasContent && !session.ComboMode)
                return;

            if (session.Keys.Count > 0)
                _scroll.Add(BuildKeysSection());

            if (session.FocusedEntry != null)
                _scroll.Add(BuildPartSection(session.FocusedPart));

            _scroll.Add(session.ComboMode ? BuildComboSection() : BuildStateSection());
        }

        private VisualElement BuildComboSection()
        {
            var session = _ctx.Session;
            var section = Section("Комбинация слоёв");
            var config = session.Config;

            if (config != null && config.layers != null)
            {
                foreach (var layer in config.layers)
                {
                    if (layer == null)
                        continue;
                    var state = session.ComboStateOf(layer);
                    var line = new VisualElement();
                    line.AddToClassList("ma-section__row");
                    var name = new Label(layer.layerName);
                    name.AddToClassList("ma-combo__layer");
                    var value = new Label(state != null ? state.stateName : "—");
                    value.AddToClassList("ma-combo__state");
                    line.Add(name);
                    line.Add(value);
                    if (state != null)
                    {
                        var l = layer;
                        line.Add(IconButtons.Create(IconButtons.Minus, "-", "Снять стейт со слоя", () => session.SetLayerState(l, null)));
                    }
                    section.Add(line);
                }
            }

            int held = 0, silent = 0;
            foreach (var info in session.ComboInfo)
            {
                if (info.Kind == ResolveKind.Held) held++;
                if (info.Kind is ResolveKind.Silent or ResolveKind.Inactive) silent++;
            }

            section.Add(Info($"Длина: {session.FrameCount} кадров, {session.Length:0.###} c"));
            if (held > 0)
                section.Add(Info($"Удержано частей: {held}. Верхний слой забирает их маской без клипа: в игре они продолжат прошлую анимацию, здесь показан нижний слой."));
            if (silent > 0)
                section.Add(Info($"Без анимации: {silent}."));
            section.Add(Info("Клик по стейту в группе слоя слева назначает его, повторный клик снимает. Ключи редактируются в клипе-победителе."));
            return section;
        }

        private static VisualElement Section(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("ma-section");
            var label = new Label(title);
            label.AddToClassList("ma-section__title");
            section.Add(label);
            return section;
        }

        private static Label Info(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ma-section__info");
            return label;
        }

        private VisualElement BuildStateSection()
        {
            var session = _ctx.Session;
            var state = session.State;
            var section = Section("Стейт");

            var name = new TextField("Имя") { value = state.stateName, isDelayed = true };
            name.RegisterValueChangedCallback(e =>
            {
                StateAssets.RenameState(session.Config, state, e.newValue.Trim());
                session.Notify(SessionChange.Data);
            });
            section.Add(name);

            var layers = new List<string>();
            var config = session.Config;
            if (config != null && config.layers != null)
                foreach (var l in config.layers)
                    if (l != null && l.states != null && l.states.Contains(state))
                        layers.Add(l.layerName);
            section.Add(Info(layers.Count > 0 ? "Слои: " + string.Join(", ", layers) : "Не входит ни в один слой"));
            section.Add(Info($"Длина: {session.FrameCount} кадров, {session.Length:0.###} c"));

            var parts = new VisualElement();
            parts.AddToClassList("ma-parts");
            for (int i = 0; i < session.Parts.Count; i++)
                parts.Add(BuildPartLine(i));
            section.Add(parts);

            var add = new Button(() => Menus.ShowAddPartMenu(_ctx)) { text = "+ Добавить дорожку", focusable = false };
            add.AddToClassList("ma-section__button");
            section.Add(add);
            return section;
        }

        private VisualElement BuildPartLine(int index)
        {
            var session = _ctx.Session;
            var part = session.Parts[index];
            var line = new VisualElement();
            line.AddToClassList("ma-part-line");
            line.EnableInClassList("ma-part-line--focused", index == session.FocusedPart);

            var swatch = new VisualElement();
            swatch.AddToClassList("ma-track__swatch");
            swatch.style.backgroundColor = Palette.ForPart(index);
            line.Add(swatch);

            var name = new Button(() => Menus.ShowPartNameMenu(_ctx, index))
                { text = string.IsNullOrEmpty(part.partName) ? "(часть?)" : part.partName, focusable = false };
            name.AddToClassList("ma-part-line__name");
            name.EnableInClassList("ma-track__name--missing", !session.TryGetAnimator(part, out _));
            line.Add(name);

            var clip = new Button(() => Menus.ShowClipMenu(_ctx, index))
                { text = part.clip != null ? part.clip.name : "+ клип", focusable = false };
            clip.AddToClassList("ma-part-line__clip");
            line.Add(clip);

            var remove = IconButtons.Create(IconButtons.Minus, "-", "Убрать дорожку", () =>
            {
                StateAssets.RemovePart(session.State, index);
                session.Expanded.Clear();
                session.FocusPart(-1);
                session.Notify(SessionChange.Data | SessionChange.Layout);
            });
            line.Add(remove);

            return line;
        }

        private VisualElement BuildPartSection(int index)
        {
            var session = _ctx.Session;
            var part = session.Parts[index];
            var section = Section($"Дорожка: {part.partName}");
            var clip = part.clip;

            if (clip == null)
            {
                section.Add(Info("Клип не назначен"));
                var assign = new Button(() => Menus.ShowClipMenu(_ctx, index)) { text = "Назначить клип", focusable = false };
                assign.AddToClassList("ma-section__button");
                section.Add(assign);
                return section;
            }

            var clipField = new ObjectField("Клип") { objectType = typeof(AnimationClip), value = clip };
            clipField.SetEnabled(false);
            section.Add(clipField);

            var fps = new FloatField("FPS") { value = clip.frameRate, isDelayed = true };
            fps.RegisterValueChangedCallback(e =>
            {
                if (e.newValue <= 0f)
                    return;
                Undo.RecordObject(clip, "Change Frame Rate");
                clip.frameRate = e.newValue;
                EditorUtility.SetDirty(clip);
                session.Notify(SessionChange.Data);
            });
            section.Add(fps);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var loopTime = new Toggle("Loop Time") { value = settings.loopTime };
            var loopPose = new Toggle("Loop Pose") { value = settings.loopBlend };
            loopPose.SetEnabled(settings.loopTime);

            loopTime.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(clip, "Toggle Loop Time");
                var s = AnimationUtility.GetAnimationClipSettings(clip);
                s.loopTime = e.newValue;
                AnimationUtility.SetAnimationClipSettings(clip, s);
                EditorUtility.SetDirty(clip);
                loopPose.SetEnabled(e.newValue);
            });
            loopPose.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(clip, "Toggle Loop Pose");
                var s = AnimationUtility.GetAnimationClipSettings(clip);
                s.loopBlend = e.newValue;
                AnimationUtility.SetAnimationClipSettings(clip, s);
                EditorUtility.SetDirty(clip);
            });
            section.Add(loopTime);
            section.Add(loopPose);

            section.Add(Info($"Длина: {Mathf.RoundToInt(clip.length * clip.frameRate)} кадров, {clip.length:0.###} c"));
            section.Add(Info($"Стейт в аниматоре: {part.AnimatorStateAlias}"));

            var info = ClipCache.Get(clip);
            section.Add(Info($"Анимируемых свойств: {info.Groups.Count}, ключевых моментов: {info.Times.Length}"));
            return section;
        }

        private VisualElement BuildKeysSection()
        {
            var session = _ctx.Session;
            var keys = session.Keys.ToList();
            keys.Sort((a, b) => a.Time.CompareTo(b.Time));
            var section = Section($"Ключи: {keys.Count}");

            float fps = session.FrameRate;
            float minTime = keys[0].Time;
            var frame = new IntegerField("Кадр") { value = Mathf.RoundToInt(minTime * fps), isDelayed = true };
            frame.tooltip = keys.Select(k => k.TimeKey).Distinct().Count() > 1
                ? "Сдвигает всё выделение так, чтобы первый ключ встал на этот кадр"
                : "Кадр ключа";
            frame.RegisterValueChangedCallback(e =>
            {
                float delta = Mathf.Max(0, e.newValue) / fps - minTime;
                if (Mathf.Approximately(delta, 0f))
                    return;
                var moved = KeyOps.ApplyMove(KeyOps.Snapshot(keys), keys, delta, "Move Keys");
                session.Keys.Set(moved);
                session.Notify(SessionChange.Data | SessionChange.Selection);
            });
            section.Add(frame);

            int shown = 0;
            foreach (var key in keys)
            {
                if (shown++ >= MaxKeyRows)
                {
                    section.Add(Info($"…и ещё {keys.Count - MaxKeyRows}"));
                    break;
                }
                section.Add(BuildKeyField(key));
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("ma-section__row");
            var copy = new Button(_ctx.CopySelectedKeys) { text = "Копировать", focusable = false };
            var delete = new Button(_ctx.DeleteSelectedKeys) { text = "Удалить", focusable = false };
            buttons.Add(copy);
            buttons.Add(delete);
            section.Add(buttons);
            return section;
        }

        private VisualElement BuildKeyField(KeyRef key)
        {
            var session = _ctx.Session;
            string label = $"{BindingLabel(key.Binding)} @{Mathf.RoundToInt(key.Time * session.FrameRate)}";

            if (key.IsObject)
            {
                KeyOps.TryGetObject(key, out var value);
                var field = new ObjectField(label)
                {
                    objectType = ObjectTypeOf(key),
                    allowSceneObjects = false,
                    value = value
                };
                field.RegisterValueChangedCallback(e =>
                {
                    KeyOps.SetObject(key, e.newValue);
                    session.Notify(SessionChange.Data);
                });
                field.AddToClassList("ma-key-field");
                return field;
            }

            KeyOps.TryGetFloat(key, out var f);
            var floatField = new FloatField(label) { value = f, isDelayed = true };
            floatField.RegisterValueChangedCallback(e =>
            {
                KeyOps.SetFloat(key, e.newValue);
                session.Notify(SessionChange.Data);
            });
            floatField.AddToClassList("ma-key-field");
            return floatField;
        }

        private System.Type ObjectTypeOf(KeyRef key)
        {
            var session = _ctx.Session;
            int part = session.PartIndexOf(key.Clip);
            if (part >= 0 && session.TryGetAnimator(session.Parts[part], out var animator))
            {
                var type = AnimationUtility.GetEditorCurveValueType(animator.gameObject, key.Binding);
                if (type != null)
                    return type;
            }
            return typeof(Object);
        }

        private static string BindingLabel(EditorCurveBinding b)
        {
            var prop = b.propertyName;
            var axis = "";
            var baseName = ClipCache.StripAxis(prop);
            if (baseName.Length != prop.Length)
                axis = "." + prop[^1];
            var segment = string.IsNullOrEmpty(b.path) ? "" : b.path.Split('/')[^1] + ": ";
            return segment + ClipCache.Nicify(baseName) + axis;
        }
    }
}
