using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class SaveSnapshotInfo
{
    public string Id;
    public string ParentId;
    public DateTime Time;
}

[Serializable]
public class SaveHistoryIndex
{
    public string Active;
    public List<SaveSnapshotInfo> Snapshots = new();

    public SaveSnapshotInfo Find(string id) => Snapshots.Find(s => s.Id == id);
}

public static class SaveHistory
{
    const string IndexFile = "history.json";
    const string HistoryDir = "history";
    const string TempSuffix = ".tmp";
    const string NoSnapshot = "none";

    public static string HistoryPath(string root) => Path.Combine(root, HistoryDir);

    public static string SnapshotDir(string root, string id) => Path.Combine(root, HistoryDir, id);

    public static string SnapshotPath(string root, string id) => $"{SnapshotDir(root, id ?? NoSnapshot)}/";

    public static string ActivePath(string root) => SnapshotPath(root, ReadIndex(root).Active);

    public static SaveHistoryIndex ReadIndex(string root)
    {
        SaveHistoryIndex index = null;
        var file = Path.Combine(root, IndexFile);

        if (File.Exists(file))
        {
            try
            {
                index = JsonConvert.DeserializeObject<SaveHistoryIndex>(File.ReadAllText(file));
            }
            catch (Exception e)
            {
                Debug.LogError($"Save history index unreadable, rebuilding: {file}\n{e}");
            }
        }

        index ??= new SaveHistoryIndex();
        index.Snapshots ??= new List<SaveSnapshotInfo>();
        Reconcile(root, index);
        return index;
    }

    static void Reconcile(string root, SaveHistoryIndex index)
    {
        var dir = HistoryPath(root);
        var existing = Directory.Exists(dir)
            ? Directory.GetDirectories(dir)
                .Where(d => !d.EndsWith(TempSuffix))
                .OrderBy(Directory.GetCreationTimeUtc)
                .ToList()
            : new List<string>();

        var ids = new HashSet<string>(existing.Select(Path.GetFileName));
        index.Snapshots.RemoveAll(s => s == null || string.IsNullOrEmpty(s.Id) || !ids.Contains(s.Id));

        foreach (var d in existing)
        {
            var id = Path.GetFileName(d);
            if (index.Find(id) != null)
                continue;

            index.Snapshots.Add(new SaveSnapshotInfo
            {
                Id = id,
                ParentId = index.Snapshots.Count > 0 ? index.Snapshots[^1].Id : null,
                Time = Directory.GetCreationTimeUtc(d)
            });
        }

        if (index.Active == null || index.Find(index.Active) == null)
            index.Active = index.Snapshots.Count > 0 ? index.Snapshots[^1].Id : null;
    }

    public static void WriteIndex(string root, SaveHistoryIndex index)
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, IndexFile);
        var tmp = file + TempSuffix;

        File.WriteAllText(tmp, JsonConvert.SerializeObject(index, Formatting.Indented));
        if (File.Exists(file))
            File.Replace(tmp, file, null);
        else
            File.Move(tmp, file);
    }

    static string NewId(string root, SaveHistoryIndex index, DateTime utc)
    {
        var baseId = utc.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
        var id = baseId;
        int n = 1;
        while (index.Find(id) != null || Directory.Exists(SnapshotDir(root, id)))
            id = $"{baseId}_{n++}";
        return id;
    }

    public static bool Write(string root, SaveFile[] files)
    {
        string temp = null;
        try
        {
            var index = ReadIndex(root);
            var now = DateTime.UtcNow;
            var id = NewId(root, index, now);
            var final = SnapshotDir(root, id);
            temp = final + TempSuffix;

            if (Directory.Exists(temp))
                Directory.Delete(temp, true);
            Directory.CreateDirectory(temp);

            var written = new HashSet<string>();
            foreach (var f in files)
            {
                File.WriteAllBytes(Path.Combine(temp, f.Name), f.Data);
                written.Add(f.Name);
            }

            var parent = index.Active;
            if (parent != null)
            {
                var parentDir = SnapshotDir(root, parent);
                if (Directory.Exists(parentDir))
                {
                    foreach (var existing in Directory.GetFiles(parentDir))
                    {
                        var name = Path.GetFileName(existing);
                        if (!written.Contains(name))
                            File.Copy(existing, Path.Combine(temp, name), true);
                    }
                }
            }

            Directory.Move(temp, final);
            temp = null;

            index.Snapshots.Add(new SaveSnapshotInfo { Id = id, ParentId = parent, Time = now });
            index.Active = id;
            WriteIndex(root, index);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save failed: {e}");
            try
            {
                if (temp != null && Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
            catch (Exception cleanup)
            {
                Debug.LogError($"Save cleanup failed: {cleanup}");
            }
            return false;
        }
    }

    public static bool SetActive(string root, string id)
    {
        var index = ReadIndex(root);
        if (index.Find(id) == null)
            return false;

        index.Active = id;
        WriteIndex(root, index);
        return true;
    }

    public static bool Delete(string root, string id)
    {
        var index = ReadIndex(root);
        var target = index.Find(id);
        if (target == null)
            return false;

        Directory.Delete(SnapshotDir(root, id), true);
        index.Snapshots.Remove(target);

        foreach (var s in index.Snapshots)
        {
            if (s.ParentId == id)
                s.ParentId = target.ParentId;
        }

        if (index.Active == id)
            index.Active = target.ParentId != null && index.Find(target.ParentId) != null
                ? target.ParentId
                : index.Snapshots.Count > 0 ? index.Snapshots[^1].Id : null;

        WriteIndex(root, index);
        return true;
    }

    public static void Recover(string root)
    {
        var dir = HistoryPath(root);
        if (Directory.Exists(dir))
        {
            foreach (var d in Directory.GetDirectories(dir).Where(d => d.EndsWith(TempSuffix)))
                Directory.Delete(d, true);
        }

        var tmpIndex = Path.Combine(root, IndexFile) + TempSuffix;
        if (File.Exists(tmpIndex))
            File.Delete(tmpIndex);

        MigrateLegacy(root);
    }

    static void MigrateLegacy(string root)
    {
        if (!Directory.Exists(root) || Directory.Exists(HistoryPath(root)))
            return;

        var legacy = Directory.GetFiles(root)
            .Where(f => Path.GetFileName(f) != IndexFile)
            .Where(f => f.EndsWith(".json") || f.EndsWith(".xml"))
            .ToList();

        if (legacy.Count == 0)
            return;

        var time = legacy.Max(File.GetLastWriteTimeUtc);
        var index = new SaveHistoryIndex();
        var id = NewId(root, index, time);
        var final = SnapshotDir(root, id);

        Directory.CreateDirectory(final);
        foreach (var f in legacy)
            File.Move(f, Path.Combine(final, Path.GetFileName(f)));

        index.Snapshots.Add(new SaveSnapshotInfo { Id = id, Time = time });
        index.Active = id;
        WriteIndex(root, index);
    }
}
