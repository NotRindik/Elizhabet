#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class PixelLayersWindow : EditorWindow
{
    public static readonly Color ClipColor = new Color(0.95f, 0.3f, 0.3f, 0.35f);
    public static readonly Color ClipButtonColor = new Color(1f, 0.55f, 0.55f, 1f);

    const float RowHeight = 23f;
    const string LeftWidthPref = "PixelDrawer.TimelineLeftWidth";

    class Row
    {
        public PixelLayer layer;
        public VisualElement left;
        public VisualElement right;
        public Button eye;
        public Button lockButton;
        public VisualElement clipMark;
        public Label name;
        public readonly List<VisualElement> cells = new List<VisualElement>();
    }

    [SerializeField] bool locked;
    [SerializeField] PixelCanvas canvas;

    readonly List<PixelLayer> layers = new List<PixelLayer>();
    readonly List<Row> rows = new List<Row>();
    readonly List<Label> headCells = new List<Label>();
    readonly StringBuilder keyBuilder = new StringBuilder();

    VisualElement body;
    Label empty;
    VisualElement banner;
    Label bannerText;
    PixelScrollArea leftScroll;
    PixelScrollArea rightScroll;
    PixelScrollArea headScroll;
    VisualElement leftContent;
    VisualElement rightContent;
    VisualElement headContent;
    VisualElement dropLine;
    Label canvasTitle;
    Button playButton;
    Label frameLabel;
    IntegerField durationField;
    EnumField loopField;
    Button onionButton;
    Button gearButton;
    VisualElement settings;
    Button dupLinkedButton;
    Button dupFullButton;
    Button mergeButton;
    Button clipButton;
    Button convertButton;
    Button uniqueButton;
    Button deleteButton;
    Slider opacitySlider;
    PopupField<PixelBlendMode> blendField;

    string structureKey;
    bool refreshQueued;
    PixelCanvas boundCanvas;
    PixelLayer renaming;
    PixelLayer dragLayer;
    Vector2 dragStart;
    bool dragging;
    int dropIndex;

    [MenuItem("Window/Pixel Canvas/Layers")]
    public static void Open()
    {
        GetWindow<PixelLayersWindow>("Pixel Layers");
    }

    void OnEnable()
    {
        Selection.selectionChanged += QueueRefresh;
        Undo.undoRedoPerformed += QueueRefresh;
        EditorApplication.hierarchyChanged += QueueRefresh;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        PixelArtOps.FramesChanged += OnFramesChanged;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= QueueRefresh;
        Undo.undoRedoPerformed -= QueueRefresh;
        EditorApplication.hierarchyChanged -= QueueRefresh;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        PixelArtOps.FramesChanged -= OnFramesChanged;
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            if(this != null) Refresh(true);
        };
    }

    bool HasDeadRows()
    {
        for(var i = 0; i < rows.Count; i++)
            if(rows[i].layer == null) return true;
        return false;
    }

    void ShowButton(Rect rect)
    {
        var next = GUI.Toggle(rect, locked, GUIContent.none, "IN LockButton");
        if(next == locked) return;
        locked = next;
        QueueRefresh();
    }

    void OnFramesChanged(PixelCanvas changed)
    {
        if(changed == canvas) QueueRefresh();
    }

    void QueueRefresh()
    {
        if(refreshQueued) return;
        refreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            refreshQueued = false;
            if(this != null) Refresh(false);
        };
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        PixelUI.Setup(root);
        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        body = new VisualElement();
        body.style.flexGrow = 1;
        root.Add(body);

        BuildToolbar();
        BuildSettings();
        BuildBanner();
        BuildTimeline();
        BuildBottomBar();

        empty = PixelUI.Label("Select a Pixel Canvas or one of its layers", "pd-empty");
        root.Add(empty);

        root.schedule.Execute(() => Refresh(false)).Every(250);
        Refresh(true);
    }

    void BuildToolbar()
    {
        var s = PixelDrawerAssets.Settings;
        var bar = new VisualElement();
        bar.AddToClassList("pd-bar");
        bar.AddToClassList("pd-bar--wrap");

        bar.Add(PixelUI.Group(
            PixelUI.IconButton("first", "First frame", () => GoTo(0)),
            PixelUI.IconButton("prev", "Previous frame (" + s.prevFrame + ")", () => Step(-1)),
            playButton = PixelUI.IconButton("play", "Play / pause (" + s.togglePlay + ")", TogglePlay),
            PixelUI.IconButton("next", "Next frame (" + s.nextFrame + ")", () => Step(1)),
            PixelUI.IconButton("last", "Last frame", () => GoTo(int.MaxValue))));

        frameLabel = PixelUI.Label("1 / 1", "pd-frame-label");
        frameLabel.tooltip = "Current frame / frame count";
        bar.Add(frameLabel);

        durationField = new IntegerField("ms") { tooltip = "Duration of the current frame in milliseconds" };
        durationField.AddToClassList("pd-field-small");
        durationField.isDelayed = true;
        durationField.RegisterValueChangedCallback(e =>
        {
            if(canvas != null) PixelArtOps.SetFrameDuration(canvas, canvas.frame, Mathf.Max(10, e.newValue) / 1000f);
        });
        bar.Add(durationField);

        bar.Add(PixelUI.Group(
            PixelUI.IconButton("frame_copy", "New frame: copy of the current one (" + s.newFrame + ")", () => FrameOp(() => PixelArtOps.AddFrame(canvas, true))),
            PixelUI.IconButton("frame_add", "New empty frame", () => FrameOp(() => PixelArtOps.AddFrame(canvas, false))),
            PixelUI.IconButton("arrow_left", "Move the current frame left", () => FrameOp(() => PixelArtOps.MoveFrame(canvas, -1))),
            PixelUI.IconButton("arrow_right", "Move the current frame right", () => FrameOp(() => PixelArtOps.MoveFrame(canvas, 1))),
            Danger(PixelUI.IconButton("trash", "Delete the current frame on all layers", () => FrameOp(() => PixelArtOps.DeleteFrame(canvas))))));

        bar.Add(PixelUI.Spacer());

        loopField = new EnumField(PixelArtAsset.LoopMode.Loop) { tooltip = "Loop mode of the canvas animation" };
        loopField.style.width = 80;
        loopField.RegisterValueChangedCallback(e =>
        {
            if(canvas == null || e.newValue == null) return;
            Undo.RecordObject(canvas, "Loop Mode");
            canvas.loop = (PixelArtAsset.LoopMode)e.newValue;
            EditorUtility.SetDirty(canvas);
        });
        bar.Add(loopField);

        onionButton = PixelUI.IconButton("onion", "Onion skin: previous frame in red, next frame in green", ToggleOnion);
        gearButton = PixelUI.IconButton("gear", "Playback and onion skin settings", ToggleSettings);
        bar.Add(onionButton);
        bar.Add(gearButton);
        body.Add(bar);
    }

    static Button Danger(Button button)
    {
        button.AddToClassList("pd-danger");
        return button;
    }

    void BuildSettings()
    {
        settings = new VisualElement();
        settings.AddToClassList("pd-settings");
        canvasTitle = PixelUI.Label("", "pd-sub");
        settings.Add(canvasTitle);
        string[] props = { "autoPlay", "playSpeed", "unscaledTime", "randomStart", "onionOpacity", "onionRange" };
        for(var i = 0; i < props.Length; i++) settings.Add(new PropertyField { bindingPath = props[i] });
        settings.EnableInClassList("pd-settings--hidden", !SessionState.GetBool("pd.timeline.settings", false));
        body.Add(settings);
    }

    void BuildBanner()
    {
        banner = new VisualElement();
        banner.AddToClassList("pd-banner");
        bannerText = new Label();
        banner.Add(bannerText);
        banner.Add(PixelUI.TextButton("Use layer frames", "Take frame count and durations from the layers", () =>
        {
            if(canvas != null) PixelArtOps.AdoptFromLayers(canvas);
            Refresh(true);
        }));
        banner.style.display = DisplayStyle.None;
        body.Add(banner);
    }

    void BuildTimeline()
    {
        var split = new TwoPaneSplitView(0, EditorPrefs.GetFloat(LeftWidthPref, 190f), TwoPaneSplitViewOrientation.Horizontal);
        split.AddToClassList("pd-timeline");

        var left = new VisualElement();
        left.AddToClassList("pd-tl-left");
        var leftHeader = new VisualElement();
        leftHeader.AddToClassList("pd-tl-header");
        var eyeHead = PixelUI.Icon("eye");
        eyeHead.AddToClassList("pd-icon--dim");
        eyeHead.style.marginLeft = 4;
        eyeHead.style.marginRight = 4;
        var lockHead = PixelUI.Icon("lock");
        lockHead.AddToClassList("pd-icon--dim");
        leftHeader.Add(eyeHead);
        leftHeader.Add(lockHead);
        leftHeader.Add(PixelUI.Label("Layers", "pd-dim"));
        left.Add(leftHeader);
        leftScroll = new PixelScrollArea(true, false, false);
        leftContent = new VisualElement();
        leftContent.style.paddingBottom = 16;
        leftScroll.Add(leftContent);
        dropLine = new VisualElement();
        dropLine.AddToClassList("pd-drop-line");
        leftScroll.RegisterCallback<WheelEvent>(e =>
        {
            rightScroll.ScrollBy(new Vector2(0, e.delta.y * 18f));
            e.StopPropagation();
        }, TrickleDown.TrickleDown);
        left.Add(leftScroll);
        left.RegisterCallback<GeometryChangedEvent>(e => EditorPrefs.SetFloat(LeftWidthPref, e.newRect.width));

        var right = new VisualElement();
        right.AddToClassList("pd-tl-right");
        headScroll = new PixelScrollArea(false, true, false);
        headScroll.style.flexGrow = 0;
        headScroll.style.flexShrink = 0;
        headScroll.style.height = 21;
        headScroll.RegisterCallback<WheelEvent>(e =>
        {
            rightScroll.ScrollBy(new Vector2(e.delta.y * 18f, 0));
            e.StopPropagation();
        }, TrickleDown.TrickleDown);
        headContent = new VisualElement();
        headContent.AddToClassList("pd-tl-header");
        headScroll.Add(headContent);
        right.Add(headScroll);
        rightScroll = new PixelScrollArea(true, true, true);
        rightContent = new VisualElement();
        rightScroll.Add(rightContent);
        rightScroll.OffsetChanged += o =>
        {
            leftScroll.Offset = new Vector2(0, o.y);
            headScroll.Offset = new Vector2(o.x, 0);
        };
        right.Add(rightScroll);

        split.Add(left);
        split.Add(right);
        RegisterDrop(split, null);
        body.Add(split);
    }

    void BuildBottomBar()
    {
        var s = PixelDrawerAssets.Settings;
        var bar = new VisualElement();
        bar.AddToClassList("pd-bar");
        bar.AddToClassList("pd-bar--bottom");
        bar.AddToClassList("pd-bar--wrap");

        bar.Add(PixelUI.Group(
            PixelUI.IconButton("plus", "New layer above the selected one (" + s.newLayer + ")", NewLayer),
            dupLinkedButton = PixelUI.IconButton("duplicate", "Duplicate linked: the copy shares pixels with the original", () => WithLayer(l => PixelLayerOps.DuplicateLayer(l, false))),
            dupFullButton = PixelUI.IconButton("duplicate_full", "Duplicate full copy: the copy gets its own pixels", () => WithLayer(l => PixelLayerOps.DuplicateLayer(l, true))),
            mergeButton = PixelUI.IconButton("merge", "Merge down into the layer below (" + s.mergeDown + ")", () => WithLayer(l => PixelLayerOps.MergeDown(l))),
            clipButton = PixelUI.IconButton("clip", "Clip with layer below: shows only over the pixels of the layer below", () => WithLayer(l => PixelLayerOps.SetClip(l, !l.clip))),
            PixelUI.IconButton("correction", "New correction layer: brightness, hue, levels, curves, gradient map...", ShowCorrectionMenu)));

        bar.Add(PixelUI.Group(
            uniqueButton = PixelUI.IconButton("link", "Make unique: stop sharing pixels with linked copies", () => WithLayer(l => PixelLayerOps.MakeUnique(l))),
            convertButton = PixelUI.IconButton("convert", "Convert to Pixel Art asset: own frames and glow", () => WithLayer(l => PixelArtOps.ConvertLayer(l))),
            deleteButton = Danger(PixelUI.IconButton("trash", "Delete layer", () => WithLayer(PixelLayerOps.DeleteLayer)))));

        opacitySlider = new Slider("Opacity", 0f, 1f) { showInputField = true, tooltip = "Opacity of the selected layer" };
        opacitySlider.AddToClassList("pd-slider");
        opacitySlider.style.minWidth = 160;
        opacitySlider.RegisterValueChangedCallback(e =>
        {
            var layer = ActiveLayer();
            if(layer == null) return;
            Undo.RecordObject(layer, "Layer Opacity");
            layer.opacity = Mathf.Clamp01(e.newValue);
            layer.Apply();
            EditorUtility.SetDirty(layer);
            SceneView.RepaintAll();
        });
        bar.Add(opacitySlider);

        var modes = new List<PixelBlendMode>((PixelBlendMode[])System.Enum.GetValues(typeof(PixelBlendMode)));
        blendField = new PopupField<PixelBlendMode>(modes, PixelBlendMode.Normal, BlendName, BlendName)
        {
            tooltip = "Blend mode of the selected layer. Modes marked 'preview' are shown live only in the editor (CPU backdrop); in play mode and builds they look Normal until merged or baked."
        };
        blendField.style.width = 130;
        blendField.RegisterValueChangedCallback(e =>
        {
            var layer = ActiveLayer();
            if(layer == null || layer.IsCorrection || layer.blendMode == e.newValue) return;
            Undo.RecordObject(layer, "Blend Mode");
            layer.blendMode = e.newValue;
            EditorUtility.SetDirty(layer);
            PixelCanvasSync.Sync(canvas);
            SceneView.RepaintAll();
        });
        bar.Add(blendField);
        body.Add(bar);
    }

    void Refresh(bool force)
    {
        if(body == null) return;
        FindCanvas();
        var has = canvas != null;
        body.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
        if(!has) return;

        if(boundCanvas != canvas)
        {
            boundCanvas = canvas;
            settings.Unbind();
            settings.Bind(new SerializedObject(canvas));
            force = true;
        }

        var key = StructureKey();
        if(force || key != structureKey || HasDeadRows())
        {
            structureKey = key;
            Rebuild();
        }
        UpdateState();
    }

    void FindCanvas()
    {
        if(locked && canvas != null) return;
        var go = Selection.activeGameObject;
        if(go == null || !go.scene.IsValid()) return;
        var found = go.GetComponentInParent<PixelCanvas>(true);
        if(found != null) canvas = found;
    }

    PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponent<PixelLayer>() : null;
        return layer != null && canvas != null && layer.Root == canvas ? layer : null;
    }

    void CollectLayers()
    {
        layers.Clear();
        if(canvas == null) return;
        var t = canvas.transform;
        for(var i = 0; i < t.childCount; i++)
        {
            var l = t.GetChild(i).GetComponent<PixelLayer>();
            if(l != null) layers.Add(l);
        }
    }

    string StructureKey()
    {
        CollectLayers();
        var sb = keyBuilder;
        sb.Length = 0;
        sb.Append(canvas.GetInstanceID()).Append('|').Append(canvas.FrameCount).Append('|');
        for(var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            sb.Append(l.GetInstanceID()).Append(':').Append(l.name).Append(':').Append(PixelLayerOps.LinkCount(l));
            if(l.IsCorrection) sb.Append(":c").Append((int)l.correction.type);
            var art = l.art;
            if(art != null)
            {
                sb.Append(':').Append(art.GetInstanceID());
                for(var f = 0; f < art.frames.Count; f++)
                {
                    var frame = art.frames[f];
                    sb.Append(',').Append(art.HasPixels(frame) ? frame.dataIndex : -1);
                }
            }
            sb.Append(';');
        }
        return sb.ToString();
    }

    void Rebuild()
    {
        renaming = null;
        rows.Clear();
        headCells.Clear();
        leftContent.Clear();
        rightContent.Clear();
        headContent.Clear();

        var count = canvas.FrameCount;
        for(var f = 0; f < count; f++)
        {
            var frame = f;
            var cell = new Label((f + 1).ToString());
            cell.AddToClassList("pd-tl-head-cell");
            cell.RegisterCallback<PointerDownEvent>(e =>
            {
                if(e.button != 0) return;
                GoTo(frame);
                e.StopPropagation();
            });
            cell.AddManipulator(new ContextualMenuManipulator(e => FrameMenu(e, frame)));
            headContent.Add(cell);
            headCells.Add(cell);
        }

        for(var i = 0; i < layers.Count; i++) rows.Add(BuildRow(layers[i], count));
        leftContent.Add(dropLine);

        var layerFrames = PixelArtOps.MaxLayerFrames(canvas);
        var mismatch = layerFrames > count;
        banner.style.display = mismatch ? DisplayStyle.Flex : DisplayStyle.None;
        if(mismatch) bannerText.text = "Some layers have " + layerFrames + " frames, the canvas timeline has " + count + ".";
    }

    Row BuildRow(PixelLayer layer, int frames)
    {
        var row = new Row { layer = layer };

        row.left = new VisualElement();
        row.left.AddToClassList("pd-tl-row");
        row.eye = PixelUI.IconButton("eye", "Show / hide", () => ToggleVisible(layer));
        row.eye.AddToClassList("pd-btn--small");
        row.lockButton = PixelUI.IconButton("unlock", "Lock: no drawing on this layer", () => ToggleLock(layer));
        row.lockButton.AddToClassList("pd-btn--small");
        row.clipMark = PixelUI.Icon("clip");
        row.clipMark.AddToClassList("pd-tl-clipmark");
        row.clipMark.tooltip = "Clipped to the layer below";
        row.clipMark.pickingMode = PickingMode.Position;
        row.name = PixelUI.Label(layer.name, "pd-tl-name");
        row.left.Add(row.eye);
        row.left.Add(row.lockButton);
        row.left.Add(row.clipMark);
        if(layer.IsCorrection)
        {
            row.lockButton.style.visibility = Visibility.Hidden;
            var kindIcon = PixelUI.Icon("correction");
            kindIcon.tooltip = "Correction layer: " + ObjectNames.NicifyVariableName(layer.correction.type.ToString()) + ". Double-click to edit.";
            kindIcon.pickingMode = PickingMode.Position;
            row.left.Add(kindIcon);
            row.name.AddToClassList("pd-tl-name--correction");
        }
        row.left.Add(row.name);

        var links = PixelLayerOps.LinkCount(layer);
        if(links > 1)
        {
            var badge = new VisualElement();
            badge.AddToClassList("pd-tl-badge");
            var icon = PixelUI.Icon("link");
            icon.style.width = 12;
            icon.style.height = 12;
            badge.Add(icon);
            badge.Add(new Label("x" + links));
            badge.tooltip = "Linked: " + links + " layers share these pixels. Painting here changes all of them.";
            row.left.Add(badge);
        }

        row.left.RegisterCallback<PointerDownEvent>(e => OnRowPointerDown(e, row));
        row.left.RegisterCallback<PointerMoveEvent>(e => OnRowPointerMove(e, row));
        row.left.RegisterCallback<PointerUpEvent>(e => OnRowPointerUp(e, row));
        row.left.AddManipulator(new ContextualMenuManipulator(e => LayerMenu(e, layer)));
        RegisterDrop(row.left, layer);
        leftContent.Add(row.left);

        row.right = new VisualElement();
        row.right.AddToClassList("pd-tl-row");
        var art = layer.art;
        for(var f = 0; f < frames; f++)
        {
            var frame = f;
            var cell = new VisualElement();
            cell.AddToClassList("pd-tl-cell");
            var cel = new VisualElement();
            cel.AddToClassList("pd-cel");
            cel.pickingMode = PickingMode.Ignore;
            if(layer.IsCorrection)
            {
                cel.AddToClassList("pd-cel--correction");
                cell.tooltip = "Correction layer: applies on every frame";
            }
            else if(art == null)
            {
                cel.AddToClassList("pd-cel--static");
                cell.tooltip = "PNG layer: the same image on every frame. Convert it to a Pixel Art asset to give it frames.";
            }
            else if(f >= art.frames.Count || !art.HasPixels(art.frames[f]))
            {
                cel.AddToClassList("pd-cel--empty");
            }
            else if(f > 0 && f < art.frames.Count && art.frames[f].dataIndex >= 0 && art.frames[f].dataIndex == art.frames[f - 1].dataIndex)
            {
                var link = new VisualElement();
                link.AddToClassList("pd-cel-link");
                link.pickingMode = PickingMode.Ignore;
                cell.Add(link);
                cell.tooltip = "Linked frame: same pixels as the previous frame";
            }
            cell.Add(cel);
            cell.RegisterCallback<PointerDownEvent>(e =>
            {
                if(e.button != 0) return;
                SelectLayer(layer);
                GoTo(frame);
                e.StopPropagation();
            });
            cell.AddManipulator(new ContextualMenuManipulator(e => FrameMenu(e, frame)));
            row.right.Add(cell);
            row.cells.Add(cell);
        }
        RegisterDrop(row.right, layer);
        rightContent.Add(row.right);
        return row;
    }

    void RegisterDrop(VisualElement target, PixelLayer layer)
    {
        target.RegisterCallback<DragUpdatedEvent>(e =>
        {
            if(canvas == null || !PixelImageImport.HasImages()) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.StopPropagation();
        });
        target.RegisterCallback<DragPerformEvent>(e =>
        {
            if(canvas == null || !PixelImageImport.HasImages()) return;
            DragAndDrop.AcceptDrag();
            var images = PixelImageImport.FromDrag();
            var c = canvas;
            EditorApplication.delayCall += () =>
            {
                if(layer != null) PixelImageImport.PasteInto(layer, images, null);
                else PixelImageImport.PasteAsLayers(c, ActiveLayer(), images, null);
                Refresh(true);
            };
            e.StopPropagation();
        });
    }

    void UpdateState()
    {
        var active = ActiveLayer();
        var frame = Mathf.Clamp(canvas.frame, 0, canvas.FrameCount - 1);
        for(var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var l = row.layer;
            if(l == null) continue;
            var selected = l == active;
            var clip = l.clip && l.clipped;
            row.left.EnableInClassList("pd-tl-row--selected", selected);
            row.right.EnableInClassList("pd-tl-row--selected", selected);
            row.left.EnableInClassList("pd-tl-row--clip", clip);
            row.right.EnableInClassList("pd-tl-row--clip", clip);
            row.left.EnableInClassList("pd-tl-row--hidden", !l.visible);
            PixelUI.SetButtonIcon(row.eye, l.visible ? "eye" : "eye_off");
            PixelUI.SetButtonIcon(row.lockButton, l.locked ? "lock" : "unlock");
            row.eye.Q(className: "pd-icon").EnableInClassList("pd-icon--dim", !l.visible);
            row.lockButton.Q(className: "pd-icon").EnableInClassList("pd-icon--dim", !l.locked);
            row.clipMark.style.visibility = l.clip ? Visibility.Visible : Visibility.Hidden;
            for(var f = 0; f < row.cells.Count; f++) row.cells[f].EnableInClassList("pd-tl-cell--current", f == frame);
        }
        for(var f = 0; f < headCells.Count; f++)
        {
            headCells[f].EnableInClassList("pd-tl-head-cell--current", f == frame);
            headCells[f].tooltip = "Frame " + (f + 1) + " · " + Mathf.RoundToInt(canvas.FrameDuration(f) * 1000f) + " ms";
        }

        var playing = PixelArtPreview.IsPlaying(canvas);
        PixelUI.SetButtonIcon(playButton, playing ? "pause" : "play");
        PixelUI.SetOn(playButton, playing);
        frameLabel.text = (frame + 1) + " / " + canvas.FrameCount;
        if(durationField.focusController == null || durationField.focusController.focusedElement != durationField)
            durationField.SetValueWithoutNotify(Mathf.RoundToInt(canvas.FrameDuration(frame) * 1000f));
        loopField.SetValueWithoutNotify(canvas.loop);
        PixelUI.SetOn(onionButton, canvas.onionSkin);
        PixelUI.SetOn(gearButton, !settings.ClassListContains("pd-settings--hidden"));
        canvasTitle.text = canvas.name.ToUpperInvariant();

        var has = active != null;
        dupLinkedButton.SetEnabled(has);
        dupFullButton.SetEnabled(has);
        mergeButton.SetEnabled(has && PixelLayerOps.LayerBelow(active) != null);
        clipButton.SetEnabled(has);
        clipButton.EnableInClassList("pd-clip-on", has && active.clip);
        uniqueButton.SetEnabled(has && !active.IsCorrection && PixelLayerOps.LinkCount(active) > 1);
        convertButton.SetEnabled(has && !active.IsCorrection && active.art == null);
        dupFullButton.SetEnabled(has && !active.IsCorrection);
        deleteButton.SetEnabled(has);
        opacitySlider.SetEnabled(has);
        if(has) opacitySlider.SetValueWithoutNotify(active.opacity);
        blendField.SetEnabled(has && !active.IsCorrection);
        if(has) blendField.SetValueWithoutNotify(active.blendMode);
    }

    static string BlendName(PixelBlendMode mode)
    {
        var name = ObjectNames.NicifyVariableName(mode.ToString());
        if(mode == PixelBlendMode.AddGlow) name = "Add (Glow)";
        return PixelBlend.IsLive(mode) ? name : name + "  · preview";
    }

    void SelectLayer(PixelLayer layer)
    {
        if(layer != null && Selection.activeGameObject != layer.gameObject) Selection.activeGameObject = layer.gameObject;
    }

    void GoTo(int index)
    {
        if(canvas == null) return;
        var target = Mathf.Clamp(index, 0, canvas.FrameCount - 1);
        if(target == canvas.frame && !PixelArtPreview.IsPlaying(canvas)) return;
        PixelArtOps.SetCanvasFrame(canvas, target, true);
    }

    void Step(int dir)
    {
        if(canvas != null) PixelArtOps.StepFrame(canvas, dir);
    }

    void TogglePlay()
    {
        if(canvas != null) PixelArtPreview.Toggle(canvas);
        UpdateState();
    }

    void FrameOp(System.Action action)
    {
        if(canvas == null) return;
        action();
        Refresh(true);
    }

    void ToggleOnion()
    {
        if(canvas == null) return;
        Undo.RecordObject(canvas, "Onion Skin");
        canvas.onionSkin = !canvas.onionSkin;
        EditorUtility.SetDirty(canvas);
        SceneView.RepaintAll();
        UpdateState();
    }

    void ToggleSettings()
    {
        var hidden = !settings.ClassListContains("pd-settings--hidden");
        settings.EnableInClassList("pd-settings--hidden", hidden);
        SessionState.SetBool("pd.timeline.settings", !hidden);
        UpdateState();
    }

    void ToggleVisible(PixelLayer layer)
    {
        if(layer == null)
        {
            Refresh(true);
            return;
        }
        Undo.RecordObject(layer, "Layer Visibility");
        layer.visible = !layer.visible;
        layer.Apply();
        EditorUtility.SetDirty(layer);
        SceneView.RepaintAll();
        UpdateState();
    }

    void ToggleLock(PixelLayer layer)
    {
        if(layer == null)
        {
            Refresh(true);
            return;
        }
        Undo.RecordObject(layer, "Layer Lock");
        layer.locked = !layer.locked;
        EditorUtility.SetDirty(layer);
        UpdateState();
    }

    void ShowCorrectionMenu()
    {
        if(canvas == null) return;
        var menu = new GenericMenu();
        foreach(PixelCorrectionType type in System.Enum.GetValues(typeof(PixelCorrectionType)))
        {
            var t = type;
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(t.ToString())), false, () => NewCorrection(t));
        }
        menu.ShowAsContext();
    }

    void NewCorrection(PixelCorrectionType type)
    {
        if(canvas == null) return;
        var layer = PixelLayerOps.CreateCorrection(canvas, ActiveLayer(), type);
        Refresh(true);
        PixelCorrectionWindow.Open(layer);
    }

    void NewLayer()
    {
        if(canvas == null) return;
        PixelLayerOps.CreateLayer(canvas, ActiveLayer(), true);
        Refresh(true);
    }

    void WithLayer(System.Action<PixelLayer> action)
    {
        var layer = ActiveLayer();
        if(layer == null) return;
        action(layer);
        PixelLinkCache.Invalidate();
        Refresh(true);
    }

    void OnRowPointerDown(PointerDownEvent e, Row row)
    {
        if(e.button != 0 || InsideButton(e.target as VisualElement) || renaming == row.layer) return;
        SelectLayer(row.layer);
        if(e.clickCount == 2)
        {
            if(row.layer.IsCorrection) PixelCorrectionWindow.Open(row.layer);
            else BeginRename(row);
            e.StopPropagation();
            return;
        }
        dragLayer = row.layer;
        dragStart = e.position;
        dragging = false;
        row.left.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    void OnRowPointerMove(PointerMoveEvent e, Row row)
    {
        if(dragLayer != row.layer || !row.left.HasPointerCapture(e.pointerId)) return;
        if(!dragging && (new Vector2(e.position.x, e.position.y) - dragStart).sqrMagnitude < 25f) return;
        dragging = true;
        var local = leftContent.WorldToLocal(e.position);
        dropIndex = Mathf.Clamp(Mathf.RoundToInt(local.y / RowHeight), 0, layers.Count);
        dropLine.style.display = DisplayStyle.Flex;
        dropLine.style.top = dropIndex * RowHeight - 1f;
    }

    void OnRowPointerUp(PointerUpEvent e, Row row)
    {
        if(!row.left.HasPointerCapture(e.pointerId)) return;
        row.left.ReleasePointer(e.pointerId);
        dropLine.style.display = DisplayStyle.None;
        var moved = dragging && dragLayer == row.layer;
        dragging = false;
        dragLayer = null;
        if(moved) Reorder(row.layer, dropIndex);
    }

    static bool InsideButton(VisualElement element)
    {
        for(var e = element; e != null; e = e.parent)
            if(e is Button) return true;
        return false;
    }

    void Reorder(PixelLayer layer, int index)
    {
        var from = layers.IndexOf(layer);
        if(from < 0) return;
        if(index > from) index--;
        if(index == from) return;
        var order = new List<PixelLayer>(layers);
        order.RemoveAt(from);
        order.Insert(Mathf.Clamp(index, 0, order.Count), layer);
        Undo.RegisterChildrenOrderUndo(canvas.transform, "Reorder Pixel Layers");
        for(var i = 0; i < order.Count; i++) order[i].transform.SetSiblingIndex(i);
        PixelCanvasSync.Sync(canvas);
        EditorUtility.SetDirty(canvas);
        Refresh(true);
    }

    void BeginRename(Row row)
    {
        renaming = row.layer;
        var field = new TextField { value = row.layer.name };
        field.AddToClassList("pd-tl-rename");
        var index = row.left.IndexOf(row.name);
        row.name.style.display = DisplayStyle.None;
        row.left.Insert(index, field);
        var done = false;
        System.Action<bool> finish = commit =>
        {
            if(done) return;
            done = true;
            if(commit && row.layer != null && !string.IsNullOrEmpty(field.value) && field.value != row.layer.name)
            {
                Undo.RecordObject(row.layer.gameObject, "Rename Layer");
                row.layer.gameObject.name = field.value;
                row.name.text = field.value;
            }
            field.RemoveFromHierarchy();
            row.name.style.display = DisplayStyle.Flex;
            renaming = null;
        };
        field.RegisterCallback<KeyDownEvent>(e =>
        {
            if(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) finish(true);
            else if(e.keyCode == KeyCode.Escape) finish(false);
            e.StopPropagation();
        });
        field.RegisterCallback<FocusOutEvent>(e => finish(true));
        field.schedule.Execute(() =>
        {
            field.Focus();
            field.SelectAll();
        });
    }

    void LayerMenu(ContextualMenuPopulateEvent e, PixelLayer layer)
    {
        SelectLayer(layer);
        var row = rows.Find(r => r.layer == layer);
        if(layer.IsCorrection) e.menu.AppendAction("Edit Correction...", a => PixelCorrectionWindow.Open(layer));
        e.menu.AppendAction("Rename", a => { if(row != null) BeginRename(row); });
        foreach(PixelCorrectionType type in System.Enum.GetValues(typeof(PixelCorrectionType)))
        {
            var t = type;
            e.menu.AppendAction("New Correction Layer/" + ObjectNames.NicifyVariableName(t.ToString()), a => NewCorrection(t));
        }
        e.menu.AppendSeparator();
        e.menu.AppendAction("Duplicate Linked", a => WithLayer(l => PixelLayerOps.DuplicateLayer(l, false)));
        e.menu.AppendAction("Duplicate Full Copy", a => WithLayer(l => PixelLayerOps.DuplicateLayer(l, true)));
        e.menu.AppendAction("Merge Down (" + PixelDrawerAssets.Settings.mergeDown + ")", a => WithLayer(l => PixelLayerOps.MergeDown(l)),
            PixelLayerOps.LayerBelow(layer) != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendAction(layer.clip ? "Release Clipping" : "Clip With Layer Below", a => WithLayer(l => PixelLayerOps.SetClip(l, !l.clip)));
        e.menu.AppendSeparator();
        var links = PixelLayerOps.LinkCount(layer);
        e.menu.AppendAction("Make Unique", a => WithLayer(l => PixelLayerOps.MakeUnique(l)),
            links > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendAction("Select Linked", a =>
        {
            var linked = PixelLayerOps.LinkedLayers(layer);
            var objects = new Object[linked.Count];
            for(var i = 0; i < linked.Count; i++) objects[i] = linked[i].gameObject;
            Selection.objects = objects;
        }, links > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendAction("Convert to Pixel Art Asset", a => WithLayer(l => PixelArtOps.ConvertLayer(l)),
            layer.art == null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendAction("Hue / Saturation / Value... (" + PixelDrawerAssets.Settings.hueSaturation + ")", a => PixelAdjustWindow.Open(layer));
        e.menu.AppendAction("Trim Empty Borders", a => WithLayer(l =>
        {
            if(PixelCanvasEditing.Trim(l)) PixelCanvasEditing.SaveAll();
        }));
        e.menu.AppendSeparator();
        e.menu.AppendAction("Delete", a => WithLayer(PixelLayerOps.DeleteLayer));
        e.StopPropagation();
    }

    void FrameMenu(ContextualMenuPopulateEvent e, int frame)
    {
        GoTo(frame);
        var many = canvas != null && canvas.FrameCount > 1;
        e.menu.AppendAction("New Frame (copy)", a => FrameOp(() => PixelArtOps.AddFrame(canvas, true)));
        e.menu.AppendAction("New Empty Frame", a => FrameOp(() => PixelArtOps.AddFrame(canvas, false)));
        e.menu.AppendSeparator();
        e.menu.AppendAction("Move Left", a => FrameOp(() => PixelArtOps.MoveFrame(canvas, -1)),
            frame > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendAction("Move Right", a => FrameOp(() => PixelArtOps.MoveFrame(canvas, 1)),
            canvas != null && frame < canvas.FrameCount - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.menu.AppendSeparator();
        e.menu.AppendAction("Delete Frame", a => FrameOp(() => PixelArtOps.DeleteFrame(canvas)),
            many ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        e.StopPropagation();
    }

    void OnKeyDown(KeyDownEvent e)
    {
        if(canvas == null || PixelUI.IsTextInput(e.target as VisualElement)) return;
        var s = PixelDrawerAssets.Settings;
        if(PixelUI.HotkeyMatches(s.togglePlay, e)) TogglePlay();
        else if(PixelUI.HotkeyMatches(s.prevFrame, e) || PixelUI.HotkeyMatches(s.prevFrameArrow, e)) Step(-1);
        else if(PixelUI.HotkeyMatches(s.nextFrame, e) || PixelUI.HotkeyMatches(s.nextFrameArrow, e)) Step(1);
        else if(PixelUI.HotkeyMatches(s.newLayer, e)) NewLayer();
        else if(PixelUI.HotkeyMatches(s.mergeDown, e)) WithLayer(l => PixelLayerOps.MergeDown(l));
        else if(PixelUI.HotkeyMatches(s.newFrame, e)) FrameOp(() => PixelArtOps.AddFrame(canvas, true));
        else if(e.keyCode == KeyCode.F2)
        {
            var row = rows.Find(r => r.layer == ActiveLayer());
            if(row != null) BeginRename(row);
        }
        else return;
        e.StopPropagation();
    }
}
#endif
