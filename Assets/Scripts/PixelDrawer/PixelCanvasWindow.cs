#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PixelCanvasWindow : EditorWindow
{
    [SerializeField] PixelCanvas canvas;
    [SerializeField] bool locked;

    // Временное хранилище цвета. Замени GetPaintColor/SetPaintColor ниже
    // на реальный цвет кисти из PixelPaintState — тогда пикер начнёт красить.
    [SerializeField] Color paintColor = Color.black;
    PixelColorPickerGUI picker;

    Vector2 scroll;
    SerializedObject so;
    GUIStyle lockStyle;

    // ---------- стили (пересоздаются после перезагрузки доменов) ----------
    GUIStyle headerStyle, bodyStyle, chipStyle, subLabelStyle, hintStyle, bigButtonStyle, frameCellStyle;
    Texture2D bodyTex;

    static readonly Color Accent = new Color(0.27f, 0.55f, 0.95f);
    static readonly Color Danger = new Color(0.95f, 0.38f, 0.38f);
    static bool Pro { get { return EditorGUIUtility.isProSkin; } }
    static Color HeaderColor { get { return Pro ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.70f, 0.70f, 0.70f); } }
    static Color BodyColor { get { return Pro ? new Color(0.20f, 0.20f, 0.20f) : new Color(0.80f, 0.80f, 0.80f); } }
    static Color LineColor { get { return Pro ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.15f); } }

    [MenuItem("Window/Pixel Canvas/Canvas")]
    public static void Open()
    {
        GetWindow<PixelCanvasWindow>("Pixel Canvas").Follow();
    }

    public static void Open(PixelCanvas target)
    {
        var window = GetWindow<PixelCanvasWindow>("Pixel Canvas");
        if(target != null)
        {
            window.canvas = target;
            window.so = null;
        }
        window.Repaint();
    }

    void OnEnable()
    {
        minSize = new Vector2(290, 320);
        Selection.selectionChanged += Follow;
        Follow();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= Follow;
        if(picker != null)
        {
            picker.Dispose();
            picker = null;
        }
    }

    void OnInspectorUpdate()
    {
        Repaint();
    }

    void Follow()
    {
        if(locked) return;
        var go = Selection.activeGameObject;
        if(go == null || !go.scene.IsValid()) return;
        var found = go.GetComponentInParent<PixelCanvas>(true);
        if(found == null || found == canvas) return;
        canvas = found;
        so = null;
        Repaint();
    }

    // Вызывается самим Unity (кнопка замка в заголовке вкладки)
    void ShowButton(Rect position)
    {
        if(lockStyle == null) lockStyle = "IN LockButton";
        var next = GUI.Toggle(position, locked, GUIContent.none, lockStyle);
        if(next == locked) return;
        locked = next;
        if(!locked) Follow();
    }

    static void Defer(System.Action action)
    {
        EditorApplication.delayCall += () => action();
    }

    PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponentInParent<PixelLayer>(true) : null;
        return layer != null && layer.Root == canvas ? layer : null;
    }

    // =====================================================================
    //  UI-помощники
    // =====================================================================

    void EnsureStyles()
    {
        if(bodyTex == null)
        {
            bodyTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bodyTex.SetPixel(0, 0, Color.white);
            bodyTex.Apply();
        }

        if(headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
        }

        if(bodyStyle == null)
        {
            bodyStyle = new GUIStyle
            {
                padding = new RectOffset(8, 8, 6, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }
        bodyStyle.normal.background = bodyTex;

        if(chipStyle == null)
        {
            chipStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 20,
                alignment = TextAnchor.MiddleCenter
            };
        }

        if(frameCellStyle == null)
        {
            frameCellStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 0,
                padding = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };
        }

        if(bigButtonStyle == null)
        {
            bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 26,
                fontStyle = FontStyle.Bold
            };
        }

        if(subLabelStyle == null)
        {
            subLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9,
                padding = new RectOffset(0, 0, 4, 2)
            };
            var c = Pro ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.3f, 0.3f, 0.3f);
            subLabelStyle.normal.textColor = c;
        }

        if(hintStyle == null)
        {
            hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            var c = Pro ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.35f, 0.35f, 0.35f);
            hintStyle.normal.textColor = c;
        }
    }

    // Сворачиваемая панель в стиле Photoshop / Clip Studio
    bool BeginPanel(string key, string title, bool defaultOpen)
    {
        var sessionKey = "pcw." + key;
        var open = SessionState.GetBool(sessionKey, defaultOpen);

        var r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        if(Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(r, HeaderColor);
            if(open) EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), Accent);
        }

        var next = EditorGUI.Foldout(new Rect(r.x + 6, r.y + 3, r.width - 6, r.height - 4), open, title, true, headerStyle);
        if(next != open) SessionState.SetBool(sessionKey, next);

        if(!next)
        {
            GUILayout.Space(2);
            return false;
        }

        GUI.backgroundColor = BodyColor;
        EditorGUILayout.BeginVertical(bodyStyle);
        GUI.backgroundColor = Color.white;
        return true;
    }

    void EndPanel()
    {
        EditorGUILayout.EndVertical();
        GUILayout.Space(3);
    }

    // Мелкий заголовок группы внутри панели + тонкая линия
    void SubLabel(string text)
    {
        GUILayout.Label(text.ToUpperInvariant(), subLabelStyle);
        var r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        if(Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, LineColor);
        GUILayout.Space(3);
    }

    void Hint(string text)
    {
        GUILayout.Label(text, hintStyle);
    }

    void Prop(string name, string label = null)
    {
        var p = so.FindProperty(name);
        if(p == null) return;
        if(label == null) EditorGUILayout.PropertyField(p);
        else EditorGUILayout.PropertyField(p, new GUIContent(label));
    }

    // Булево свойство в виде кнопки-переключателя (как в панелях дизайнерских программ)
    void Chip(string name, string label, string tip = null)
    {
        var p = so.FindProperty(name);
        if(p == null) return;
        if(p.propertyType != SerializedPropertyType.Boolean)
        {
            EditorGUILayout.PropertyField(p);
            return;
        }
        var v = GUILayout.Toggle(p.boolValue, new GUIContent(label, tip), chipStyle, GUILayout.MinWidth(30));
        if(v != p.boolValue) p.boolValue = v;
    }

    static GUIContent Icon(string iconName, string fallback, string tip)
    {
        var c = EditorGUIUtility.IconContent(iconName);
        if(c == null || c.image == null) return new GUIContent(fallback, tip);
        return new GUIContent(c.image, tip);
    }

    // Кнопка с цветной подсветкой
    static bool TintButton(GUIContent content, Color tint, GUIStyle style, params GUILayoutOption[] options)
    {
        var old = GUI.backgroundColor;
        GUI.backgroundColor = tint;
        var pressed = GUILayout.Button(content, style, options);
        GUI.backgroundColor = old;
        return pressed;
    }

    // =====================================================================
    //  Окно
    // =====================================================================

    void OnGUI()
    {
        var e = Event.current;

        if(e.type == EventType.KeyDown || e.type == EventType.KeyUp)
        {
            PixelPaintHotkeys.HandleWindowEvent(e);
        }

        EnsureStyles();

        if(canvas == null)
        {
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Select a Pixel Canvas or any of its layers.",
                MessageType.Info);
            return;
        }

        if(so == null || so.targetObject != canvas) so = new SerializedObject(canvas);
        so.Update();

        DrawToolbar();

        var oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.38f, 80f, 150f);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPaint(ActiveLayer());
        DrawAnimation();
        DrawLayers();
        DrawCanvas();
        DrawReference();
        DrawBake();
        GUILayout.Space(6);
        EditorGUILayout.EndScrollView();

        EditorGUIUtility.labelWidth = oldLabelWidth;

        so.ApplyModifiedProperties();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var icon = EditorGUIUtility.IconContent("Grid.PaintTool");
        if(icon != null && icon.image != null)
            GUILayout.Label(icon.image, GUILayout.Width(18), GUILayout.Height(18));
        GUILayout.Label(canvas.name, EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        if(GUILayout.Button(new GUIContent("Ping", "Highlight the canvas in the Hierarchy"), EditorStyles.toolbarButton, GUILayout.Width(40)))
            EditorGUIUtility.PingObject(canvas);

        var nextLock = GUILayout.Toggle(locked, new GUIContent("Lock", "Don't follow selection"), EditorStyles.toolbarButton, GUILayout.Width(44));
        if(nextLock != locked)
        {
            locked = nextLock;
            if(!locked) Follow();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    Color GetPaintColor()
    {
        return paintColor;
    }

    void SetPaintColor(Color c)
    {
        paintColor = c;
        PixelPaintState.Color = paintColor;
    }

    void DrawColorPicker()
    {
        const string key = "pcw.paint.picker";
        var open = SessionState.GetBool(key, true);
        var next = EditorGUILayout.Foldout(open, "Color picker", true, EditorStyles.foldoutHeader);
        if(next != open) SessionState.SetBool(key, next);
        if(!next) return;

        if(picker == null) picker = new PixelColorPickerGUI();
        var c = GetPaintColor();
        if(picker.Draw(ref c, position.width - 28f)) SetPaintColor(c);
        GUILayout.Space(6);
    }

    void DrawPaint(PixelLayer layer)
    {
        if(!BeginPanel("paint", "Paint", true)) return;

        var settings = PixelDrawerAssets.Settings;

        DrawColorPicker();

        PixelPaintTool.DrawPaletteFull(position.width - 24f);
        GUILayout.Space(2);
        PixelPaintTool.DrawGlowFull();

        var brush = PixelPaintState.Brush;
        if(brush != null)
        {
            GUILayout.Space(2);
            SubLabel("Brush");
            PixelPaintTool.DrawBrushSection(layer, brush, settings);
        }

        GUILayout.Space(2);
        SubLabel("Tool options");
        PixelPaintTool.DrawToolOptions(layer);

        if(layer == null)
        {
            Hint("Select a layer of this canvas to use symmetry, selection and clipboard actions.");
            EndPanel();
            return;
        }

        GUILayout.Space(2);
        PixelPaintTool.DrawOptionsFoldout(layer);

        SubLabel("Selection");
        PixelPaintTool.DrawSelectionActions(layer, true);

        GUILayout.Space(2);
        Hint("Ctrl + click a pixel: select the layer that owns it");
        EndPanel();
    }

    // ---------------------------------------------------------------------
    //  ANIMATION
    // ---------------------------------------------------------------------
    void DrawAnimation()
    {
        if(!BeginPanel("animation", "Animation", true)) return;

        var settings = PixelDrawerAssets.Settings;
        var count = canvas.FrameCount;
        var index = Mathf.Clamp(canvas.frame, 0, count - 1);

        var layerFrames = PixelArtOps.MaxLayerFrames(canvas);
        if(layerFrames > count)
        {
            EditorGUILayout.HelpBox("Some layers have " + layerFrames + " frames but the canvas timeline has " + count + ". Frames now belong to the canvas.", MessageType.Warning);
            if(GUILayout.Button("Use the layers' frame count and durations")) Defer(() => PixelArtOps.AdoptFromLayers(canvas));
            GUILayout.Space(4);
        }

        // ----- транспорт: ◀ Play ▶ ... счётчик -----
        var playing = PixelArtPreview.IsPlaying(canvas);
        EditorGUILayout.BeginHorizontal();
        using(new EditorGUI.DisabledScope(count < 2))
        {
            if(GUILayout.Button(Icon("Animation.PrevKey", "|<", "Previous frame  (" + settings.prevFrame + ")"), GUILayout.Width(30), GUILayout.Height(22)))
                Defer(() => PixelArtOps.SetCanvasFrame(canvas, (index - 1 + count) % count, true));

            var playIcon = Icon(playing ? "PauseButton" : "PlayButton", playing ? "||" : ">", null);
            var playContent = new GUIContent(playing ? " Pause" : " Play", playIcon.image, (playing ? "Pause" : "Play") + "  (" + settings.togglePlay + ")");
            if(TintButton(playContent, playing ? Accent : Color.white, GUI.skin.button, GUILayout.Height(22), GUILayout.MinWidth(70)))
                Defer(() => PixelArtPreview.Toggle(canvas));

            if(GUILayout.Button(Icon("Animation.NextKey", ">|", "Next frame  (" + settings.nextFrame + ")"), GUILayout.Width(30), GUILayout.Height(22)))
                Defer(() => PixelArtOps.SetCanvasFrame(canvas, (index + 1) % count, true));
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label((index + 1) + " / " + count, EditorStyles.boldLabel, GUILayout.Width(52));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ----- лента кадров -----
        DrawFrameStrip(count, index);

        GUILayout.Space(4);

        // ----- операции с кадрами -----
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(new GUIContent("+ Copy", "Add a copy of the current frame"), EditorStyles.miniButtonLeft))
            Defer(() => PixelArtOps.AddFrame(canvas, true));
        if(GUILayout.Button(new GUIContent("+ Blank", "Add an empty frame"), EditorStyles.miniButtonMid))
            Defer(() => PixelArtOps.AddFrame(canvas, false));
        if(TintButton(new GUIContent("Delete", "Delete the current frame"), Danger, EditorStyles.miniButtonMid))
            Defer(() => PixelArtOps.DeleteFrame(canvas));
        if(GUILayout.Button(new GUIContent("«", "Move frame left"), EditorStyles.miniButtonMid, GUILayout.Width(28)))
            Defer(() => PixelArtOps.MoveFrame(canvas, -1));
        if(GUILayout.Button(new GUIContent("»", "Move frame right"), EditorStyles.miniButtonRight, GUILayout.Width(28)))
            Defer(() => PixelArtOps.MoveFrame(canvas, 1));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ----- длительность -----
        EditorGUI.BeginChangeCheck();
        var duration = EditorGUILayout.FloatField("Frame duration (s)", canvas.FrameDuration(index));
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(canvas, "Frame Duration");
            while(canvas.frameDurations.Count <= index) canvas.frameDurations.Add(0.1f);
            canvas.frameDurations[index] = Mathf.Max(0.01f, duration);
            EditorUtility.SetDirty(canvas);
            so.Update();
        }
        Hint("Total: " + canvas.TotalDuration.ToString("0.00") + " s  ·  " + count + " frame(s)");

        // ----- воспроизведение -----
        GUILayout.Space(2);
        SubLabel("Playback");
        EditorGUILayout.BeginHorizontal();
        Chip("loop", "Loop");
        Chip("autoPlay", "Auto-play");
        Chip("unscaledTime", "Unscaled", "Ignore Time.timeScale");
        Chip("randomStart", "Random start");
        EditorGUILayout.EndHorizontal();
        Prop("playSpeed", "Speed");

        // ----- onion skin -----
        SubLabel("Onion skin");
        EditorGUILayout.BeginHorizontal();
        Chip("onionSkin", "Onion skin");
        EditorGUILayout.EndHorizontal();
        if(canvas.onionSkin)
        {
            Prop("onionOpacity", "Opacity");
            Prop("onionRange", "Range");
        }

        GUILayout.Space(2);
        Hint("Frame keys: " + settings.prevFrame + " / " + settings.nextFrame);
        EndPanel();
    }

    // Лента кадров: пронумерованные ячейки, активная подсвечена
    void DrawFrameStrip(int count, int index)
    {
        if(count > 60)
        {
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.IntSlider("Frame", index + 1, 1, Mathf.Max(1, count)) - 1;
            if(EditorGUI.EndChangeCheck() && picked != index) Defer(() => PixelArtOps.SetCanvasFrame(canvas, picked, true));
            return;
        }

        const float cell = 26f;
        const float gap = 2f;
        var width = position.width - 28f; // отступы панели + скролл
        var cols = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (cell + gap)));
        var rows = Mathf.CeilToInt(count / (float)cols);

        var area = GUILayoutUtility.GetRect(0, rows * (cell - 4f + gap), GUILayout.ExpandWidth(true));

        for(var i = 0; i < count; i++)
        {
            var cx = i % cols;
            var cy = i / cols;
            var r = new Rect(area.x + cx * (cell + gap), area.y + cy * (cell - 4f + gap), cell, cell - 4f);

            var current = i == index;
            var old = GUI.backgroundColor;
            if(current) GUI.backgroundColor = Accent;
            var tip = "Frame " + (i + 1) + "  ·  " + canvas.FrameDuration(i).ToString("0.00") + " s";
            var clicked = GUI.Toggle(r, current, new GUIContent((i + 1).ToString(), tip), frameCellStyle);
            GUI.backgroundColor = old;

            if(clicked && !current)
            {
                var target = i;
                Defer(() => PixelArtOps.SetCanvasFrame(canvas, target, true));
            }
        }
    }

    // ---------------------------------------------------------------------
    //  LAYERS
    // ---------------------------------------------------------------------
    void DrawLayers()
    {
        if(!BeginPanel("layers", "Layers and copies", false)) return;

        EditorGUILayout.BeginHorizontal();
        if(TintButton(new GUIContent("+ Add Layer", "Create a new layer"), Accent, GUI.skin.button, GUILayout.Height(22)))
            Defer(() => PixelLayerOps.CreateLayer(canvas, null, true));
        if(GUILayout.Button(new GUIContent("Layers window", "Open the Layers window"), GUILayout.Height(22)))
            PixelLayersWindow.Open();
        EditorGUILayout.EndHorizontal();

        SubLabel("Duplicate");
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(new GUIContent("Linked", "Copy that shares the same PNGs")))
            Defer(() => PixelLayerOps.DuplicateCanvas(canvas, false));
        if(GUILayout.Button(new GUIContent("Full copy", "Copy with its own PNGs")))
            Defer(() => PixelLayerOps.DuplicateCanvas(canvas, true));
        EditorGUILayout.EndHorizontal();

        SubLabel("Assets");
        if(GUILayout.Button(new GUIContent("Make All Layers Unique (new folder)", "Give every layer its own files in a new folder")))
            Defer(() => PixelLayerOps.MakeCanvasUnique(canvas));
        if(GUILayout.Button("Convert all layers to Pixel Art assets"))
            Defer(() => PixelArtOps.ConvertCanvas(canvas));
        if(GUILayout.Button("Clean unused PNGs"))
            Defer(() => PixelLayerOps.CleanUnusedPngs(canvas));

        EndPanel();
    }

    // ---------------------------------------------------------------------
    //  CANVAS
    // ---------------------------------------------------------------------
    void DrawCanvas()
    {
        if(!BeginPanel("canvas", "Canvas", false)) return;

        SubLabel("Size");
        Prop("pixelsPerUnit");
        Prop("maxSize");
        Prop("growStep");

        SubLabel("Storage");
        Prop("assetFolder");

        SubLabel("Tiling");
        Prop("tileMode");
        if(canvas.tileMode != PixelCanvas.TileMode.Off) Prop("tileSize");

        EndPanel();
    }

    // ---------------------------------------------------------------------
    //  REFERENCE
    // ---------------------------------------------------------------------
    void DrawReference()
    {
        if(!BeginPanel("reference", "Reference image", false)) return;

        Prop("reference");
        Prop("referenceVisible", "Visible");
        Prop("referenceOpacity", "Opacity");

        SubLabel("Transform");
        Prop("referenceOffset", "Offset");
        Prop("referenceScale", "Scale");

        EndPanel();
    }

    // ---------------------------------------------------------------------
    //  BAKE
    // ---------------------------------------------------------------------
    void DrawBake()
    {
        if(!BeginPanel("bake", "Bake", false)) return;

        Prop("baked");
        Prop("useBakedInPlay");

        GUILayout.Space(4);
        if(TintButton(new GUIContent("Bake to one sprite"), Accent, bigButtonStyle))
            Defer(() => PixelBaker.Bake(canvas));

        EndPanel();
    }
}
#endif
