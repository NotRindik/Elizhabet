using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{

    public static class IconButtons
    {
        public const string Plus = "Toolbar Plus";
        public const string Minus = "Toolbar Minus";
        public const string Duplicate = "TreeEditor.Duplicate";
        public const string Trash = "TreeEditor.Trash";

        public static Texture Load(string name)
        {
            var content = EditorGUIUtility.IconContent(name);
            return content != null ? content.image : null;
        }

        public static Button Create(string icon, string fallback, string tooltip, Action action)
        {
            var button = new Button(action) { tooltip = tooltip, focusable = false };
            button.AddToClassList("ma-icon-button");

            var texture = Load(icon);
            if (texture != null)
                button.Add(new Image { image = texture, scaleMode = ScaleMode.ScaleToFit });
            else
                button.text = fallback;

            return button;
        }
    }
}
