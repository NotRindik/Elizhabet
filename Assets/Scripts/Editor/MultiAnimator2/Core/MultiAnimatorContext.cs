using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MultiAnimator2
{
    public sealed class MultiAnimatorContext : IDisposable
    {
        public readonly MultiAnimatorSession Session = new();
        public readonly TimeAxis Axis = new();
        public readonly List<ClipboardEntry> Clipboard = new();
        public readonly PreviewPlayer Preview;
        public readonly KeyRecorder Recorder;

        public event Action<string> Message;
        public event Action FrameAllRequested;

        public MultiAnimatorContext()
        {
            Preview = new PreviewPlayer(Session);
            Recorder = new KeyRecorder(Session, Preview);
            ClipCache.Invalidated += OnClipsInvalidated;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public void Dispose()
        {
            ClipCache.Invalidated -= OnClipsInvalidated;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Recorder.Dispose();
            Preview.Dispose();
        }

        public void ShowMessage(string text) => Message?.Invoke(text);

        public void RequestFrameAll() => FrameAllRequested?.Invoke();

        public void DeleteSelectedKeys()
        {
            if (Session.Keys.Count == 0)
                return;
            KeyOps.Delete(Session.Keys.ToList());
            Session.Keys.Clear();
            Session.Notify(SessionChange.Data | SessionChange.Selection);
        }

        public void CopySelectedKeys()
        {
            if (Session.Keys.Count == 0)
                return;
            Clipboard.Clear();
            Clipboard.AddRange(KeyOps.Copy(Session.Keys));
            ShowMessage($"Скопировано ключей: {Clipboard.Count}");
        }

        public void Paste() => PasteAt(Session.Time, PasteTarget());

        public void PasteAt(float time, AnimationClip target)
        {
            if (target == null || Clipboard.Count == 0)
                return;
            var pasted = KeyOps.Paste(Clipboard, target, Session.Snap(time));
            Session.Keys.Set(pasted);
            Session.Notify(SessionChange.Data | SessionChange.Selection);
        }

        public void AddKeysAtPlayhead()
        {
            var focused = Session.FocusedEntry;
            var added = new List<KeyRef>();

            if (focused != null && focused.clip != null)
                added.AddRange(KeyOps.AddKeys(focused.clip, ClipCache.Get(focused.clip).Groups, Session.Time));
            else
                foreach (var p in Session.Parts)
                    if (p.clip != null)
                        added.AddRange(KeyOps.AddKeys(p.clip, ClipCache.Get(p.clip).Groups, Session.Time));

            if (added.Count == 0)
                return;
            Session.Keys.Set(added);
            Session.Notify(SessionChange.Data | SessionChange.Selection);
        }

        public void StepFrame(int delta) => Session.SetFrame(Session.Frame + delta);

        public void StepKey(int direction)
        {
            float now = Session.Time;
            bool found = false;
            float best = direction > 0 ? float.MaxValue : float.MinValue;

            foreach (var p in Session.Parts)
            {
                var info = ClipCache.Get(p.clip);
                if (info == null)
                    continue;
                foreach (var t in info.Times)
                {
                    bool candidate = direction > 0 ? t > now + 0.0005f && t < best : t < now - 0.0005f && t > best;
                    if (!candidate)
                        continue;
                    best = t;
                    found = true;
                }
            }

            if (found)
                Session.SetTime(best);
        }

        public void Save()
        {
            if (Session.Config != null)
                EditorUtility.SetDirty(Session.Config);
            if (Session.State != null)
                EditorUtility.SetDirty(Session.State);
            foreach (var p in Session.Parts)
                if (p.clip != null)
                    EditorUtility.SetDirty(p.clip);
            AssetDatabase.SaveAssets();
            ShowMessage("Сохранено");
        }

        private AnimationClip PasteTarget()
        {
            var focused = Session.FocusedEntry;
            if (focused != null && focused.clip != null)
                return focused.clip;

            foreach (var k in Session.Keys)
                return k.Clip;

            foreach (var p in Session.Parts)
                if (p.clip != null)
                    return p.clip;

            return null;
        }

        private void OnClipsInvalidated() => Session.Notify(SessionChange.Data);

        private void OnUndoRedo()
        {
            Session.ValidateState();
            Session.PruneSelection();
            Session.Notify(SessionChange.State | SessionChange.Data | SessionChange.Selection | SessionChange.Layout);
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;
            Recorder.SetRecording(false);
            Preview.SetPreview(false);
        }
    }
}
