using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveMonitorWindow : EditorWindow
{
    enum Change { None, Added, Changed }

    static readonly Color Panel = new Color(0.16f, 0.16f, 0.18f);
    static readonly Color Row = new Color(0.20f, 0.20f, 0.23f);
    static readonly Color Selected = new Color(0.24f, 0.36f, 0.55f);
    static readonly Color Dim = new Color(0.62f, 0.62f, 0.68f);
    static readonly Color KeyColor = new Color(0.62f, 0.80f, 1f);
    static readonly Color StringColor = new Color(0.82f, 0.90f, 0.62f);
    static readonly Color NumberColor = new Color(0.95f, 0.75f, 0.50f);
    static readonly Color BoolColor = new Color(0.85f, 0.62f, 0.95f);
    static readonly Color AddedColor = new Color(0.25f, 0.55f, 0.30f, 0.55f);
    static readonly Color ChangedColor = new Color(0.65f, 0.55f, 0.15f, 0.55f);
    static readonly Color RemovedColor = new Color(0.65f, 0.22f, 0.22f, 0.55f);

    string BasePath => Application.persistentDataPath + "/saves/";

    int slot = -1;
    string snapshot;
    string file;
    bool autoRefresh = true;
    bool showDiff = true;
    string search = "";

    VisualElement slotList;
    VisualElement historyList;
    VisualElement fileList;
    Image screenshot;
    Label header;
    ScrollView content;
    Label status;

    double nextPoll;
    string pollSignature;
    Dictionary<string, string> shownKeys = new Dictionary<string, string>();

    [MenuItem("Window/Eliza/Save Monitor")]
    public static void Open()
    {
        var window = GetWindow<SaveMonitorWindow>("Save Monitor");
        window.minSize = new Vector2(760, 360);
    }

    void OnEnable()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.update -= Poll;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            slot = -1;
            Reload(true);
        }
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.backgroundColor = Panel;

        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(() => Reload(false)) { text = "Refresh" });
        var auto = new ToolbarToggle { text = "Auto", value = autoRefresh, tooltip = "Follow new saves automatically" };
        auto.RegisterValueChangedCallback(e => autoRefresh = e.newValue);
        toolbar.Add(auto);
        var diff = new ToolbarToggle { text = "Diff", value = showDiff, tooltip = "Highlight what changed compared to the previous save of this branch" };
        diff.RegisterValueChangedCallback(e =>
        {
            showDiff = e.newValue;
            ShowFile();
        });
        toolbar.Add(diff);
        toolbar.Add(new ToolbarButton(OpenFolder) { text = "Open Folder" });
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        toolbar.Add(spacer);
        var searchField = new ToolbarSearchField { tooltip = "Filter by key or value" };
        searchField.style.width = 220;
        searchField.RegisterValueChangedCallback(e =>
        {
            search = e.newValue ?? "";
            ShowFile();
        });
        toolbar.Add(searchField);
        root.Add(toolbar);

        var outer = new TwoPaneSplitView(0, 230, TwoPaneSplitViewOrientation.Horizontal);
        outer.style.flexGrow = 1;
        root.Add(outer);

        var left = Column();
        left.Add(Title("SLOTS"));
        slotList = new VisualElement();
        left.Add(slotList);
        left.Add(Title("HISTORY"));
        var historyScroll = new ScrollView();
        historyScroll.style.flexGrow = 1;
        historyList = historyScroll.contentContainer;
        left.Add(historyScroll);
        outer.Add(left);

        var inner = new TwoPaneSplitView(0, 190, TwoPaneSplitViewOrientation.Horizontal);
        outer.Add(inner);

        var middle = Column();
        middle.Add(Title("FILES"));
        fileList = new VisualElement();
        middle.Add(fileList);
        screenshot = new Image { scaleMode = ScaleMode.ScaleToFit };
        screenshot.style.marginTop = 8;
        screenshot.style.height = 110;
        middle.Add(screenshot);
        inner.Add(middle);

        var right = Column();
        header = new Label();
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4;
        header.style.whiteSpace = WhiteSpace.Normal;
        right.Add(header);
        content = new ScrollView();
        content.style.flexGrow = 1;
        right.Add(content);
        status = new Label();
        status.style.color = Dim;
        status.style.fontSize = 10;
        right.Add(status);
        inner.Add(right);

        Reload(true);
    }

    static VisualElement Column()
    {
        var column = new VisualElement();
        column.style.flexGrow = 1;
        column.style.paddingLeft = 6;
        column.style.paddingRight = 6;
        column.style.paddingTop = 6;
        column.style.paddingBottom = 6;
        return column;
    }

    static Label Title(string text)
    {
        var label = new Label(text);
        label.style.color = Dim;
        label.style.fontSize = 10;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 6;
        label.style.marginBottom = 3;
        return label;
    }

    static VisualElement Item(string title, string subtitle, bool selected, bool marked, Action click)
    {
        var item = new VisualElement();
        item.style.backgroundColor = selected ? Selected : Row;
        item.style.marginBottom = 2;
        item.style.paddingLeft = 6;
        item.style.paddingRight = 6;
        item.style.paddingTop = 3;
        item.style.paddingBottom = 3;
        item.style.borderTopLeftRadius = item.style.borderTopRightRadius = item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 3;
        var titleLabel = new Label((marked ? "● " : "") + title);
        if (marked) titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        item.Add(titleLabel);
        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = new Label(subtitle);
            sub.style.color = Dim;
            sub.style.fontSize = 10;
            sub.style.whiteSpace = WhiteSpace.Normal;
            item.Add(sub);
        }
        item.RegisterCallback<PointerDownEvent>(e => click());
        return item;
    }

    string SlotRoot(int index) => $"{BasePath}slot_{index}/";

    List<int> Slots()
    {
        if (!Directory.Exists(BasePath)) return new List<int>();
        return Directory.GetDirectories(BasePath, "slot_*")
            .Select(d => int.TryParse(Path.GetFileName(d).Substring(5), out var n) ? n : -1)
            .Where(n => n >= 0).OrderBy(n => n).ToList();
    }

    int ActiveSlot(List<int> slots)
    {
        if (EditorApplication.isPlaying) return SaveManager.Instance.CurrSlot;
        var best = slots.Count > 0 ? slots[0] : 0;
        var bestTime = DateTime.MinValue;
        foreach (var s in slots)
        {
            var index = SlotRoot(s) + "history.json";
            if (!File.Exists(index)) continue;
            var time = File.GetLastWriteTimeUtc(index);
            if (time > bestTime)
            {
                bestTime = time;
                best = s;
            }
        }
        return best;
    }

    void Reload(bool followActive)
    {
        if (slotList == null) return;
        var slots = Slots();
        var activeSlot = ActiveSlot(slots);
        if (slot < 0 || !slots.Contains(slot)) slot = activeSlot;

        slotList.Clear();
        foreach (var s in slots)
        {
            var index = SaveHistory.ReadIndex(SlotRoot(s));
            var manifest = ReadManifest(SaveHistory.SnapshotPath(SlotRoot(s), index.Active));
            var subtitle = index.Active == null ? "empty" : manifest;
            var captured = s;
            slotList.Add(Item($"Slot {s}", subtitle, s == slot, s == activeSlot, () =>
            {
                slot = captured;
                snapshot = null;
                Reload(true);
            }));
        }

        var history = SaveHistory.ReadIndex(SlotRoot(slot));
        if (followActive || snapshot == null || history.Find(snapshot) == null) snapshot = history.Active;

        historyList.Clear();
        foreach (var info in history.Snapshots.OrderByDescending(x => x.Time))
        {
            var captured = info.Id;
            var parent = string.IsNullOrEmpty(info.ParentId) ? "root" : "after " + info.ParentId;
            historyList.Add(Item(info.Id, parent, info.Id == snapshot, info.Id == history.Active, () =>
            {
                snapshot = captured;
                Reload(false);
            }));
        }

        fileList.Clear();
        var dir = SnapshotDir(slot, snapshot);
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir).Where(f => f.EndsWith(".json") || f.EndsWith(".xml")).Select(Path.GetFileName).OrderBy(f => f).ToList()
            : new List<string>();
        if (file == null || !files.Contains(file)) file = files.FirstOrDefault();
        foreach (var f in files)
        {
            var captured = f;
            var size = new FileInfo(Path.Combine(dir, f)).Length;
            fileList.Add(Item(f, $"{size} bytes", f == file, false, () =>
            {
                file = captured;
                Reload(false);
            }));
        }

        LoadScreenshot();
        pollSignature = Signature();
        ShowFile();
    }

    string SnapshotDir(int slotIndex, string id) => id == null ? null : SaveHistory.SnapshotPath(SlotRoot(slotIndex), id);

    string ParentSnapshot()
    {
        var info = SaveHistory.ReadIndex(SlotRoot(slot)).Find(snapshot);
        return info?.ParentId;
    }

    static string ReadManifest(string dir)
    {
        try
        {
            var path = Path.Combine(dir, "SaveManifest.xml");
            if (!File.Exists(path)) return "";
            var doc = XDocument.Load(path);
            string Get(string name) => doc.Root?.Element(name)?.Value ?? "";
            var seconds = float.TryParse(Get("currPlaySec"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) ? sec : 0f;
            var played = TimeSpan.FromSeconds(seconds);
            var date = DateTime.TryParse(Get("dateTime"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToLocalTime().ToString("dd.MM HH:mm") : "";
            return $"{Get("sceneName")}  ·  {date}  ·  {(int)played.TotalHours}h {played.Minutes}m";
        }
        catch
        {
            return "manifest unreadable";
        }
    }

    void LoadScreenshot()
    {
        var path = SlotRoot(slot) + "Screen.jpg";
        if (screenshot.image != null) DestroyImmediate(screenshot.image);
        screenshot.image = null;
        if (!File.Exists(path)) return;
        var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
        if (tex.LoadImage(File.ReadAllBytes(path))) screenshot.image = tex;
        else DestroyImmediate(tex);
    }

    void OpenFolder()
    {
        var dir = SnapshotDir(slot, snapshot) ?? SlotRoot(slot);
        if (!Directory.Exists(dir)) dir = BasePath;
        EditorUtility.RevealInFinder(dir);
    }

    void ShowFile()
    {
        if (content == null) return;
        content.Clear();
        var dir = SnapshotDir(slot, snapshot);
        if (file == null || dir == null)
        {
            header.text = "No save files in this slot";
            status.text = BasePath;
            return;
        }

        var path = Path.Combine(dir, file);
        header.text = $"Slot {slot}  ›  {snapshot}  ›  {file}";
        status.text = path;

        Dictionary<string, string> current;
        JToken json = null;
        try
        {
            if (file.EndsWith(".json"))
            {
                json = JToken.Parse(File.ReadAllText(path));
                current = Flatten(json);
            }
            else current = FlattenXml(XDocument.Load(path).Root);
        }
        catch (Exception e)
        {
            content.Add(Message("Can't read the file: " + e.Message, RemovedColor));
            return;
        }
        shownKeys = current;

        Dictionary<string, string> previous = null;
        var parent = ParentSnapshot();
        if (showDiff && parent != null)
        {
            var parentPath = Path.Combine(SnapshotDir(slot, parent), file);
            try
            {
                if (File.Exists(parentPath))
                    previous = file.EndsWith(".json") ? Flatten(JToken.Parse(File.ReadAllText(parentPath))) : FlattenXml(XDocument.Load(parentPath).Root);
                else
                    previous = new Dictionary<string, string>();
            }
            catch
            {
                previous = null;
            }
        }

        if (previous != null)
        {
            var added = current.Keys.Count(k => !previous.ContainsKey(k));
            var changed = current.Keys.Count(k => previous.TryGetValue(k, out var v) && v != current[k]);
            var removed = previous.Keys.Where(k => !current.ContainsKey(k)).ToList();
            var summary = new Label(added + changed + removed.Count == 0
                ? $"No changes since {parent}"
                : $"Since {parent}:  +{added} added   ~{changed} changed   −{removed.Count} removed");
            summary.style.color = Dim;
            summary.style.marginBottom = 4;
            content.Add(summary);
            if (removed.Count > 0)
            {
                var box = new Foldout { text = $"Removed ({removed.Count})", value = true };
                foreach (var key in removed.Where(Matches))
                    box.Add(Line(key, previous[key], Change.None, RemovedColor));
                content.Add(box);
            }
        }

        Change ChangeOf(string key)
        {
            if (previous == null) return Change.None;
            if (!previous.TryGetValue(key, out var old)) return Change.Added;
            return old != current[key] ? Change.Changed : Change.None;
        }

        if (!string.IsNullOrEmpty(search))
        {
            foreach (var pair in current.Where(p => Matches(p.Key) || p.Value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                content.Add(Line(pair.Key, pair.Value, ChangeOf(pair.Key), null));
            return;
        }

        if (json != null) BuildTree(json, "", content, ChangeOf, 0);
        else foreach (var pair in current) content.Add(Line(pair.Key, pair.Value, ChangeOf(pair.Key), null));
    }

    bool Matches(string key) => string.IsNullOrEmpty(search) || key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    static VisualElement Message(string text, Color color)
    {
        var label = new Label(text);
        label.style.backgroundColor = color;
        label.style.paddingLeft = 6;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    void BuildTree(JToken token, string path, VisualElement parent, Func<string, Change> changeOf, int depth)
    {
        if (token is JObject obj)
        {
            if (path == "" && obj.Properties().All(p => p.Value is JValue))
            {
                foreach (var group in obj.Properties().GroupBy(p => SceneOf(p.Name)).OrderBy(g => g.Key))
                {
                    var target = parent;
                    if (obj.Count > 12)
                    {
                        var fold = new Foldout { text = $"{group.Key}  ({group.Count()})", value = true };
                        parent.Add(fold);
                        target = fold;
                    }
                    foreach (var p in group) target.Add(Line(p.Name, Value(p.Value), changeOf(p.Name), null));
                }
                return;
            }
            foreach (var p in obj.Properties())
            {
                var childPath = path == "" ? p.Name : path + "." + p.Name;
                if (p.Value is JValue v) parent.Add(Line(DisplayName(p.Name), Value(v), changeOf(childPath), null));
                else
                {
                    var fold = new Foldout { text = $"{DisplayName(p.Name)}  {Count(p.Value)}", value = depth < 1 };
                    MarkFoldout(fold, childPath, changeOf);
                    parent.Add(fold);
                    BuildTree(p.Value, childPath, fold, changeOf, depth + 1);
                }
            }
            return;
        }

        if (token is JArray array)
        {
            if (array.All(x => x is JValue))
            {
                var values = array.Select(Value).ToList();
                foreach (var group in values.GroupBy(SceneOf).OrderBy(g => g.Key))
                {
                    var target = parent;
                    if (values.Count > 12)
                    {
                        var fold = new Foldout { text = $"{group.Key}  ({group.Count()})", value = true };
                        parent.Add(fold);
                        target = fold;
                    }
                    foreach (var value in group) target.Add(Line(value, "", changeOf($"{path}/{value}"), null));
                }
                return;
            }
            for (int i = 0; i < array.Count; i++)
            {
                var childPath = $"{path}[{i}]";
                var item = array[i];
                if (item is JValue v) parent.Add(Line($"[{i}]", Value(v), changeOf(childPath), null));
                else
                {
                    var fold = new Foldout { text = $"[{i}] {Summary(item)}", value = false };
                    MarkFoldout(fold, childPath, changeOf);
                    parent.Add(fold);
                    BuildTree(item, childPath, fold, changeOf, depth + 1);
                }
            }
        }
    }

    void MarkFoldout(Foldout fold, string path, Func<string, Change> changeOf)
    {
        if (!showDiff) return;
        foreach (var key in shownKeys.Keys)
        {
            if (!key.StartsWith(path, StringComparison.Ordinal) || key.Length == path.Length) continue;
            var next = key[path.Length];
            if (next != '.' && next != '[' && next != '/') continue;
            var change = changeOf(key);
            if (change == Change.None) continue;
            fold.style.backgroundColor = change == Change.Added ? AddedColor : ChangedColor;
            return;
        }
    }

    static string DisplayName(string name)
    {
        var comma = name.IndexOf(", Assembly-CSharp", StringComparison.Ordinal);
        return comma > 0 ? name.Substring(0, comma) : name;
    }

    static string Count(JToken token) => token is JArray a ? $"[{a.Count}]" : token is JObject o ? $"{{{o.Count}}}" : "";

    static string Summary(JToken token)
    {
        if (token is JObject o)
        {
            foreach (var key in new[] { "itemName", "name", "id", "Id", "key" })
                if (o[key] is JValue v) return Value(v);
            return $"{{{o.Count}}}";
        }
        return Count(token);
    }

    static string SceneOf(string key)
    {
        var slash = key.IndexOf('/');
        return slash > 0 ? key.Substring(0, slash) : "(global)";
    }

    static string Value(JToken token) => token.Type == JTokenType.Null ? "null" : token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Newtonsoft.Json.Formatting.None);

    static VisualElement Line(string key, string value, Change change, Color? forced)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.paddingLeft = 4;
        row.style.paddingTop = 1;
        row.style.paddingBottom = 1;
        if (forced.HasValue) row.style.backgroundColor = forced.Value;
        else if (change == Change.Added) row.style.backgroundColor = AddedColor;
        else if (change == Change.Changed) row.style.backgroundColor = ChangedColor;

        var keyLabel = new Label(key);
        keyLabel.style.color = KeyColor;
        keyLabel.style.whiteSpace = WhiteSpace.Normal;
        keyLabel.style.flexShrink = 1;
        row.Add(keyLabel);

        if (!string.IsNullOrEmpty(value))
        {
            var valueLabel = new Label(value);
            valueLabel.style.marginLeft = 10;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            valueLabel.style.flexShrink = 1;
            valueLabel.style.color = value == "true" || value == "false" ? BoolColor
                : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? NumberColor
                : StringColor;
            row.Add(valueLabel);
        }
        row.RegisterCallback<ContextClickEvent>(e => EditorGUIUtility.systemCopyBuffer = string.IsNullOrEmpty(value) ? key : key + " = " + value);
        row.tooltip = "Right click: copy";
        return row;
    }

    static Dictionary<string, string> Flatten(JToken token)
    {
        var result = new Dictionary<string, string>();
        void Walk(JToken t, string path)
        {
            switch (t)
            {
                case JObject o:
                    foreach (var p in o.Properties()) Walk(p.Value, path == "" ? p.Name : path + "." + p.Name);
                    break;
                case JArray a when a.All(x => x is JValue):
                    foreach (var v in a) result[$"{path}/{Value(v)}"] = "";
                    break;
                case JArray a:
                    for (int i = 0; i < a.Count; i++) Walk(a[i], $"{path}[{i}]");
                    break;
                default:
                    result[path] = Value(t);
                    break;
            }
        }
        Walk(token, "");
        return result;
    }

    static Dictionary<string, string> FlattenXml(XElement root)
    {
        var result = new Dictionary<string, string>();
        if (root == null) return result;
        void Walk(XElement e, string path)
        {
            if (!e.HasElements)
            {
                result[path] = e.Value;
                return;
            }
            foreach (var child in e.Elements()) Walk(child, path == "" ? child.Name.LocalName : path + "." + child.Name.LocalName);
        }
        Walk(root, "");
        return result;
    }

    string Signature()
    {
        var root = SlotRoot(slot);
        var index = root + "history.json";
        var stamp = File.Exists(index) ? File.GetLastWriteTimeUtc(index).Ticks : 0;
        return $"{slot}:{stamp}:{(EditorApplication.isPlaying ? SaveManager.Instance.CurrSlot : -1)}";
    }

    void Poll()
    {
        if (!autoRefresh || slotList == null || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        var signature = Signature();
        if (signature == pollSignature) return;
        var followActive = snapshot == null || snapshot == SaveHistory.ReadIndex(SlotRoot(slot)).Active;
        if (EditorApplication.isPlaying && SaveManager.Instance.CurrSlot != slot)
        {
            slot = SaveManager.Instance.CurrSlot;
            followActive = true;
        }
        Reload(followActive);
    }
}
