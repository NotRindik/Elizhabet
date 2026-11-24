using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{

    public sealed class TimelineView : VisualElement
    {
        private readonly MultiAnimatorContext _ctx;
        private readonly RulerElement _ruler;
        private readonly TrackHeadersView _headers;
        private readonly LanesCanvas _lanes;
        private readonly VisualElement _playhead;
        private readonly Scroller _scroller;
        private readonly ScrollView _scroll;
        private readonly Label _empty;
        private readonly Button _addPart;

        private List<TrackRow> _rows = new();
        private string _signature;
        private bool _fitPending;

        public TimelineView(MultiAnimatorContext ctx)
        {
            _ctx = ctx;
            AddToClassList("ma-timeline");

            var top = new VisualElement();
            top.AddToClassList("ma-timeline__top");
            var corner = new VisualElement();
            corner.AddToClassList("ma-timeline__corner");
            corner.style.width = TrackLayout.HeaderWidth;
            var cornerLabel = new Label("Дорожки");
            cornerLabel.AddToClassList("ma-timeline__corner-label");
            corner.Add(cornerLabel);
            _addPart = IconButtons.Create(IconButtons.Plus, "+", "Добавить дорожку", () => Menus.ShowAddPartMenu(_ctx));
            corner.Add(_addPart);
            top.Add(corner);
            _ruler = new RulerElement(ctx);
            top.Add(_ruler);
            Add(top);

            var body = new VisualElement();
            body.AddToClassList("ma-timeline__body");

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("ma-timeline__scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var rowsHost = new VisualElement();
            rowsHost.AddToClassList("ma-timeline__rows");
            _headers = new TrackHeadersView(ctx);
            _lanes = new LanesCanvas(ctx);
            rowsHost.Add(_headers);
            rowsHost.Add(_lanes);
            _scroll.Add(rowsHost);
            body.Add(_scroll);

            _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _playhead.AddToClassList("ma-playhead");
            body.Add(_playhead);

            _empty = new Label { pickingMode = PickingMode.Ignore };
            _empty.AddToClassList("ma-empty");
            body.Add(_empty);
            Add(body);

            _scroller = new Scroller(0f, 1f, v => _ctx.Axis.SetScroll(v), SliderDirection.Horizontal);
            _scroller.AddToClassList("ma-timeline__hscroll");
            Add(_scroller);

            _lanes.RegisterCallback<GeometryChangedEvent>(_ => OnLanesResized());
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
        }

        private void Subscribe()
        {
            _ctx.Session.Changed += OnSessionChanged;
            _ctx.Axis.Changed += OnAxisChanged;
            _ctx.FrameAllRequested += FrameAll;
            RebuildRows(true);
        }

        private void Unsubscribe()
        {
            _ctx.Session.Changed -= OnSessionChanged;
            _ctx.Axis.Changed -= OnAxisChanged;
            _ctx.FrameAllRequested -= FrameAll;
        }

        private void OnSessionChanged(SessionChange change)
        {
            if ((change & (SessionChange.Root | SessionChange.State)) != 0)
                _fitPending = true;

            if ((change & (SessionChange.Root | SessionChange.State | SessionChange.Data | SessionChange.Layout)) != 0)
                RebuildRows((change & (SessionChange.Root | SessionChange.State)) != 0);

            if ((change & SessionChange.Selection) != 0)
            {
                _headers.UpdateFocus();
                _lanes.MarkDirtyRepaint();
            }

            if ((change & SessionChange.Time) != 0)
            {
                _ruler.MarkDirtyRepaint();
                UpdatePlayhead();
            }
        }

        private void OnAxisChanged()
        {
            _ruler.MarkDirtyRepaint();
            _lanes.MarkDirtyRepaint();
            UpdatePlayhead();
            UpdateScroller();
        }

        private void RebuildRows(bool force)
        {
            var session = _ctx.Session;
            _rows = TrackLayout.Build(session);
            var signature = TrackLayout.Signature(session, _rows);

            if (force || signature != _signature)
            {
                _signature = signature;
                _headers.Rebuild(_rows);
            }

            _lanes.SetRows(_rows);
            _ruler.MarkDirtyRepaint();
            _addPart.SetEnabled(session.CanEditStructure);

            if (session.ComboMode)
                ShowEmpty(session.Parts.Count == 0
                    ? "В слоях конфига нет масок"
                    : session.Combo.Count == 0 ? "Выбери стейты в слоях слева" : null);
            else if (session.State == null)
                ShowEmpty(session.Config == null ? "У объекта не назначен AnimationComposerConfig" : "Выбери или создай стейт слева");
            else if (session.Parts.Count == 0)
                ShowEmpty("В стейте нет дорожек. Нажми + над списком дорожек");
            else
                ShowEmpty(null);

            UpdatePlayhead();
            UpdateScroller();

            if (_fitPending && _lanes.layout.width > 60f)
            {
                _fitPending = false;
                FrameAll();
            }
        }

        private void ShowEmpty(string text)
        {
            _empty.text = text ?? "";
            _empty.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnLanesResized()
        {
            if (_fitPending && _lanes.layout.width > 60f)
            {
                _fitPending = false;
                FrameAll();
            }
            UpdatePlayhead();
            UpdateScroller();
        }

        private void FrameAll() => _ctx.Axis.Fit(_ctx.Session.Length, _lanes.layout.width);

        private void UpdatePlayhead()
        {
            var session = _ctx.Session;
            float x = _ctx.Axis.T2X(session.Time);
            float width = _lanes.layout.width;
            bool visible = session.HasContent && session.Parts.Count > 0 && x >= 0f && x <= width;
            _playhead.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _playhead.style.left = TrackLayout.HeaderWidth + x;
        }

        private void UpdateScroller()
        {
            float content = _ctx.Session.Length * _ctx.Axis.PixelsPerSecond + 100f;
            float view = Mathf.Max(1f, _lanes.layout.width);
            float max = Mathf.Max(0f, content - view);
            _scroller.lowValue = 0f;
            _scroller.highValue = Mathf.Max(max, _ctx.Axis.Scroll);
            _scroller.Adjust(Mathf.Clamp01(view / Mathf.Max(view, content)));
            _scroller.slider.SetValueWithoutNotify(_ctx.Axis.Scroll);
        }
    }
}
