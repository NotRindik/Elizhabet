using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    public sealed class ToolbarView : Toolbar
    {
        private readonly MultiAnimatorContext _ctx;
        private readonly Label _target;
        private readonly ToolbarToggle _preview;
        private readonly ToolbarToggle _record;
        private readonly Image _playIcon;
        private readonly IntegerField _frame;
        private readonly Label _time;
        private readonly Label _fps;

        private Texture _playTexture;
        private Texture _pauseTexture;

        public ToolbarView(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-toolbar");

            _target = new Label();
            _target.AddToClassList("ma-toolbar__target");
            Add(_target);

            _preview = new ToolbarToggle { text = "Preview", focusable = false, tooltip = "Показывать позу из стейта на объекте" };
            _preview.RegisterValueChangedCallback(e => _ctx.Preview.SetPreview(e.newValue));
            Add(_preview);

            _record = new ToolbarToggle { focusable = false, tooltip = "Запись ключей (R)" };
            _record.AddToClassList("ma-toolbar__record");
            _record.Add(IconImage("Animation.Record", "●"));
            _record.RegisterValueChangedCallback(e => _ctx.Recorder.SetRecording(e.newValue));
            Add(_record);

            Add(new ToolbarSpacer());

            Add(IconButton("Animation.FirstKey", "|◀", "В начало (Home)", () => _ctx.Session.SetTime(0f)));
            Add(IconButton("Animation.PrevKey", "◀", "Предыдущий ключ (Alt+←)", () => _ctx.StepKey(-1)));

            _playTexture = LoadIcon("Animation.Play");
            _pauseTexture = LoadIcon("PauseButton");
            var play = new ToolbarButton(_ctx.Preview.TogglePlay) { focusable = false, tooltip = "Play / Pause (Space)" };
            play.AddToClassList("ma-toolbar__icon");
            _playIcon = new Image { image = _playTexture };
            play.Add(_playIcon);
            if (_playTexture == null)
                play.text = "▶";
            Add(play);

            Add(IconButton("Animation.NextKey", "▶", "Следующий ключ (Alt+→)", () => _ctx.StepKey(1)));
            Add(IconButton("Animation.LastKey", "▶|", "В конец (End)", () => _ctx.Session.SetTime(_ctx.Session.Length)));

            Add(new ToolbarSpacer());

            _frame = new IntegerField { isDelayed = true, tooltip = "Текущий кадр" };
            _frame.AddToClassList("ma-toolbar__frame");
            _frame.RegisterValueChangedCallback(e => _ctx.Session.SetFrame(e.newValue));
            Add(_frame);

            _time = new Label();
            _time.AddToClassList("ma-toolbar__time");
            Add(_time);

            _fps = new Label();
            _fps.AddToClassList("ma-toolbar__fps");
            Add(_fps);

            Add(new ToolbarSpacer { flex = true });

            Add(new ToolbarButton(_ctx.AddKeysAtPlayhead) { text = "Ключ", focusable = false, tooltip = "Поставить ключи на текущем кадре (K)" });
            Add(new ToolbarButton(_ctx.RequestFrameAll) { text = "Вместить", focusable = false, tooltip = "Показать весь стейт (F)" });
            Add(new ToolbarButton(_ctx.Save) { text = "Сохранить", focusable = false, tooltip = "Ctrl+S" });

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _ctx.Session.Changed += OnSessionChanged;
                _ctx.Preview.StateChanged += Refresh;
                _ctx.Recorder.StateChanged += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _ctx.Session.Changed -= OnSessionChanged;
                _ctx.Preview.StateChanged -= Refresh;
                _ctx.Recorder.StateChanged -= Refresh;
            });
        }

        private void OnSessionChanged(SessionChange change) => Refresh();

        public void Refresh()
        {
            var session = _ctx.Session;
            bool hasState = session.State != null;

            _target.text = session.Tag != null
                ? $"{session.Root.name}  ›  {(session.Config != null ? session.Config.name : "нет конфига")}"
                : "—";

            _preview.SetValueWithoutNotify(_ctx.Preview.IsPreviewing);
            _record.SetValueWithoutNotify(_ctx.Recorder.IsRecording);
            _record.EnableInClassList("ma-toolbar__record--on", _ctx.Recorder.IsRecording);
            _preview.SetEnabled(hasState);
            _record.SetEnabled(hasState);

            if (_playTexture != null)
                _playIcon.image = _ctx.Preview.IsPlaying && _pauseTexture != null ? _pauseTexture : _playTexture;

            if (_frame.focusController?.focusedElement != _frame)
                _frame.SetValueWithoutNotify(session.Frame);

            float fps = session.FrameRate;
            _time.text = $"{FrameGrid.Format(session.Time, fps)} / {FrameGrid.Format(session.Length, fps)}";
            _fps.text = session.HasMixedFrameRates ? $"⚠ {fps:0.##} fps" : $"{fps:0.##} fps";
            _fps.tooltip = session.HasMixedFrameRates ? "У клипов разный FPS, шаг считается по максимальному" : "";
            _fps.EnableInClassList("ma-toolbar__fps--warn", session.HasMixedFrameRates);
        }

        private static Texture LoadIcon(string name)
        {
            var content = EditorGUIUtility.IconContent(name);
            return content != null ? content.image : null;
        }

        private static VisualElement IconImage(string icon, string fallback)
        {
            var tex = LoadIcon(icon);
            if (tex != null)
                return new Image { image = tex };
            return new Label(fallback);
        }

        private static ToolbarButton IconButton(string icon, string fallback, string tooltip, Action action)
        {
            var button = new ToolbarButton(action) { focusable = false, tooltip = tooltip };
            button.AddToClassList("ma-toolbar__icon");
            button.Add(IconImage(icon, fallback));
            return button;
        }
    }
}
