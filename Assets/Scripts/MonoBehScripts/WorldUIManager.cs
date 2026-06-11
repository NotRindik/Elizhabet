using System.Collections.Generic;
using UnityEngine;

public class WorldUIManager : MonoBehaviour
{
    public static WorldUIManager Instance { get; private set; }

    [System.Serializable]
    public struct UIElementEntry
    {
        public string key;
        public WorldUIElement prefab;
        public int prewarmCount;
    }

    [SerializeField] private RectTransform container;
    [SerializeField] private RectTransform canvas;
    [SerializeField] private UIElementEntry[] entries;

    private Dictionary<string, WorldUIElement> _prefabMap = new();
    private Dictionary<string, Queue<WorldUIElement>> _pool = new();
    private Dictionary<WorldUIElement, string> _elementKeyMap = new();

    private void Awake()
    {
        Instance = this;

        foreach (var entry in entries)
        {
            _prefabMap[entry.key] = entry.prefab;
            _pool[entry.key] = new Queue<WorldUIElement>();

            for (int i = 0; i < entry.prewarmCount; i++)
                _pool[entry.key].Enqueue(CreateNew(entry.key));
        }
    }

    // Возвращает билдер — вся цепочка заканчивается .Play()
    public WorldUITween Spawn(string key, Transform target, string text = null)
    {
        var el = Get(key);
        if (text != null) el.SetText(text);
        el.SetTarget(target);
        return new WorldUITween(el);
    }

    public WorldUITween Spawn(string key, Vector3 worldPos, string text = null)
    {
        var el = Get(key);
        if (text != null) el.SetText(text);
        el.SetPosition(worldPos);
        return new WorldUITween(el);
    }

    private WorldUIElement Get(string key)
    {
        while (_pool[key].Count > 0)
        {
            var el = _pool[key].Dequeue();

            if (el == null) // Unity destroyed object
                continue;

            el.gameObject.SetActive(true);
            _elementKeyMap[el] = key;
            return el;
        }

        var created = CreateNew(key);
        _elementKeyMap[created] = key;
        return created;
    }

    public void Return(WorldUIElement element)
    {
        if (element == null)
            return;

        element.gameObject.SetActive(false);
        element.transform.SetParent(container);

        if (_elementKeyMap.TryGetValue(element, out string key))
        {
            _pool[key].Enqueue(element);
            _elementKeyMap.Remove(element);
        }
    }

    private WorldUIElement CreateNew(string key)
    {
        var el = Instantiate(_prefabMap[key], container);
        el.gameObject.SetActive(false);
        el.GetComponent<WorldUITracker>()._canvasRect = canvas;
        return el;
    }
}