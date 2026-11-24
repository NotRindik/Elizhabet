#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PixelUI
{
    static StyleSheet sheet;

    public static void Setup(VisualElement root)
    {
        root.AddToClassList("pd-root");
        if(sheet == null)
        {
            foreach(var guid in AssetDatabase.FindAssets("PixelDrawer t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if(!path.EndsWith("/PixelDrawer.uss")) continue;
                sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                break;
            }
        }
        if(sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
    }

    public static VisualElement Icon(string name)
    {
        var icon = new VisualElement();
        icon.AddToClassList("pd-icon");
        icon.pickingMode = PickingMode.Ignore;
        SetIcon(icon, name);
        return icon;
    }

    public static void SetIcon(VisualElement icon, string name)
    {
        var tex = PixelIcons.Get(name);
        icon.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
    }

    public static Button IconButton(string icon, string tooltip, Action click)
    {
        var button = new Button(click) { tooltip = tooltip, focusable = false };
        button.AddToClassList("pd-btn");
        button.Add(Icon(icon));
        return button;
    }

    public static void SetButtonIcon(Button button, string icon)
    {
        if(button.childCount > 0) SetIcon(button[0], icon);
    }

    public static Button TextButton(string text, string tooltip, Action click)
    {
        var button = new Button(click) { text = text, tooltip = tooltip, focusable = false };
        button.AddToClassList("pd-btn");
        button.AddToClassList("pd-btn--text");
        return button;
    }

    public static void SetOn(VisualElement element, bool on)
    {
        element.EnableInClassList("pd-on", on);
    }

    public static VisualElement Row(params VisualElement[] children)
    {
        var row = new VisualElement();
        row.AddToClassList("pd-row");
        for(var i = 0; i < children.Length; i++) row.Add(children[i]);
        return row;
    }

    public static VisualElement Group(params VisualElement[] children)
    {
        var group = new VisualElement();
        group.AddToClassList("pd-group");
        for(var i = 0; i < children.Length; i++) group.Add(children[i]);
        return group;
    }

    public static VisualElement Separator()
    {
        var sep = new VisualElement();
        sep.AddToClassList("pd-sep");
        return sep;
    }

    public static VisualElement Spacer()
    {
        var spacer = new VisualElement();
        spacer.AddToClassList("pd-spacer");
        return spacer;
    }

    public static Label Label(string text, string className)
    {
        var label = new Label(text);
        if(!string.IsNullOrEmpty(className)) label.AddToClassList(className);
        return label;
    }

    public static VisualElement Section(VisualElement parent, string key, string title, bool defaultOpen, out VisualElement body)
    {
        var prefKey = "pd.section." + key;
        var section = new VisualElement();
        section.AddToClassList("pd-section");
        var header = new VisualElement();
        header.AddToClassList("pd-section__header");
        var chevron = Icon("chevron_down");
        chevron.AddToClassList("pd-icon--dim");
        header.Add(chevron);
        header.Add(Label(title, "pd-section__title"));
        section.Add(header);
        body = new VisualElement();
        body.AddToClassList("pd-section__body");
        section.Add(body);

        Action<bool> apply = open =>
        {
            section.EnableInClassList("pd-section--closed", !open);
            SetIcon(chevron, open ? "chevron_down" : "chevron_right");
        };
        apply(SessionState.GetBool(prefKey, defaultOpen));
        header.RegisterCallback<PointerDownEvent>(e =>
        {
            if(e.button != 0) return;
            var open = !SessionState.GetBool(prefKey, defaultOpen);
            SessionState.SetBool(prefKey, open);
            apply(open);
            e.StopPropagation();
        });
        parent.Add(section);
        return header;
    }

    public static bool HotkeyMatches(PixelHotkey hotkey, KeyDownEvent e)
    {
        if(hotkey == null || hotkey.key == KeyCode.None || e.keyCode != hotkey.key) return false;
        return (e.ctrlKey || e.commandKey) == hotkey.ctrl && e.shiftKey == hotkey.shift && e.altKey == hotkey.alt;
    }

    public static bool IsTextInput(VisualElement element)
    {
        for(var e = element; e != null; e = e.parent)
            if(e is TextField || e.ClassListContains("unity-base-text-field")) return true;
        return false;
    }
}
#endif
