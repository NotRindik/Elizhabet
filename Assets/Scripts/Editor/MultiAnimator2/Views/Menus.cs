using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public static class Menus
    {
        public static void ShowClipMenu(MultiAnimatorContext ctx, int partIndex)
        {
            var session = ctx.Session;
            var state = session.State;
            if (state == null || partIndex < 0 || partIndex >= session.Parts.Count)
                return;

            var part = session.Parts[partIndex];
            var menu = new GenericMenu();

            if (!session.TryGetAnimator(part, out var animator))
            {
                menu.AddDisabledItem(new GUIContent("Сначала выбери часть"));
                menu.ShowAsContext();
                return;
            }

            foreach (var clip in StateAssets.ControllerClips(animator))
            {
                var c = clip;
                menu.AddItem(new GUIContent(c.name.Replace('/', '∕')), c == part.clip, () =>
                {
                    StateAssets.SetPartClip(session, state, partIndex, c);
                    session.Notify(SessionChange.Data | SessionChange.Layout);
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Создать новый клип…"), false, () =>
            {
                if (StateAssets.CreateClip(session, state, partIndex) != null)
                    session.Notify(SessionChange.Data | SessionChange.Layout);
            });

            if (part.clip != null)
            {
                var clip = part.clip;
                menu.AddItem(new GUIContent("Показать в Project"), false, () => EditorGUIUtility.PingObject(clip));
                menu.AddItem(new GUIContent("Убрать клип из части"), false, () =>
                {
                    StateAssets.SetPartClip(session, state, partIndex, null);
                    session.Notify(SessionChange.Data | SessionChange.Layout);
                });
                menu.AddItem(new GUIContent($"Удалить файл «{clip.name}»…"), false, () =>
                {
                    StateAssets.DeleteClip(session, state, partIndex);
                    session.Notify(SessionChange.Data | SessionChange.Layout);
                });
            }

            menu.ShowAsContext();
        }

        public static void ShowPartNameMenu(MultiAnimatorContext ctx, int partIndex)
        {
            var session = ctx.Session;
            var state = session.State;
            if (state == null || partIndex < 0 || partIndex >= session.Parts.Count)
                return;

            var current = session.Parts[partIndex].partName;
            var menu = new GenericMenu();

            foreach (var name in session.Animators.Keys)
            {
                var n = name;
                menu.AddItem(new GUIContent(n.Replace('/', '∕')), n == current, () =>
                {
                    StateAssets.SetPartName(session, state, partIndex, n);
                    session.Notify(SessionChange.Data | SessionChange.Layout);
                });
            }

            if (session.Animators.Count == 0)
                menu.AddDisabledItem(new GUIContent("На объекте нет Animator'ов"));

            menu.ShowAsContext();
        }

        public static void ShowAddPartMenu(MultiAnimatorContext ctx)
        {
            var session = ctx.Session;
            var state = session.State;
            if (state == null)
                return;

            var menu = new GenericMenu();
            foreach (var name in session.Animators.Keys)
            {
                var n = name;
                bool used = false;
                foreach (var p in session.Parts)
                    used |= p.partName == n;

                if (used)
                {
                    menu.AddDisabledItem(new GUIContent(n.Replace('/', '∕')), true);
                    continue;
                }

                menu.AddItem(new GUIContent(n.Replace('/', '∕')), false, () =>
                {
                    StateAssets.AddPart(state, n);
                    session.Notify(SessionChange.Data | SessionChange.Layout);
                });
            }

            if (session.Animators.Count == 0)
                menu.AddDisabledItem(new GUIContent("На объекте нет Animator'ов"));

            menu.ShowAsContext();
        }
    }
}
