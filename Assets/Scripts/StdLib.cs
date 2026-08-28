using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;

namespace std
{
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using System.Reflection;

    using System;
    using UnityEngine;

    [Serializable]
    public struct Optional<T> where T : struct
    {
        [SerializeField] private bool enabled;
        [SerializeField] private T value;

        public bool Enabled => enabled;

        public T Value
        {
            get
            {
                if (!enabled)
                    throw new InvalidOperationException("Optional has no value");

                return value;
            }
        }

        public bool TryGet(out T result)
        {
            result = value;
            return enabled;
        }

        public static Optional<T> None() =>
            new Optional<T>();

        public static Optional<T> Some(T value) =>
            new Optional<T>(value);

        public Optional(T initialValue)
        {
            enabled = true;
            value = initialValue;
        }
        
        
        public static implicit operator Optional<T>(T value) =>
            new Optional<T>(value);

        public static explicit operator T(Optional<T> optional) =>
            optional.Value;

    }

    namespace UnityUtilities
    {
        public static class Utilities
        {
            public static bool IsInLayerMask(LayerMask collideLayer,GameObject obj)
            {
                return (collideLayer.value & (1 << obj.layer)) != 0;
            }
        }

        public static class ColorExtension
        {
            public static Vector3 ParseToVector3(this Color col)
            {
                return new Vector3(col.r, col.g, col.b);
            }
        }
        
        public static class VectorsExtension
        {
            public static Color ParseToVector3(this Vector3 vec)
            {
                return new Color(vec.x, vec.y, vec.z);
            }
        }
        
    }
    
    public static class CollectionExtension{
        public static string ToStringElements<T>(
            this IEnumerable<T> collection,
            string separator = ", ")
        {
            return string.Join(separator, collection);
        }
    }

[System.Serializable]
    public class ObservableList<T>
{
    [SerializeField] private List<T> _list = new List<T>();

    public Action<T> OnItemAdded;
    public Action<T> OnItemRemoved;
    public Action<T> OnItemChanged;
    public Action<T,T> OnItemSet;

    public void Add(T item)
    {
        _list.Add(item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

        UpdateSerialization();
    }

    public void UpdateSerialization()
    {
/*        _serializedFields.Clear();
        foreach (var item in _list)
        {
            _serializedFields.Add(item);
        }*/
    }

    public void Set(int i, T item)
    {
        var temp = _list[i];
        _list[i]  = item;
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);
        
        OnItemSet?.Invoke(temp,item);

        UpdateSerialization();
    }
    public void Insert(int i, T item)
    {
        _list.Insert(i, item);
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

        UpdateSerialization();
    }
    public bool Remove(T item)
    {
        bool removed = _list.Remove(item);
        if (removed)
        {
            OnItemRemoved?.Invoke(item);
            OnItemChanged?.Invoke(item);
        }
        UpdateSerialization();
        return removed;
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _list.Count || toIndex < 0 || toIndex >= _list.Count || fromIndex == toIndex)
            return;

        T item = _list[fromIndex];

        _list.RemoveAt(fromIndex);

        // ���� ���������� ����� �� ������, ����� ������ �����
        if (toIndex > fromIndex) toIndex--;

        _list.Insert(toIndex, item);
        UpdateSerialization();
    }

    public bool AreEqual(T a, T b)
    {
        return EqualityComparer<T>.Default.Equals(a, b);
    }
    public bool RemoveAndSetDefault(T item)
    {
        var removedIndex = _list.FindIndex(a => AreEqual(a,item));

        if (removedIndex != -1)
        {
            Raw[removedIndex] = default;
            OnItemRemoved?.Invoke(item);
            OnItemChanged?.Invoke(item);
        }
        UpdateSerialization();
        return removedIndex != -1;
    }
    public bool RemoveAndSetDefaultSilent(T item)
    {
        var removedIndex = _list.FindIndex(a => AreEqual(a,item));

        if (removedIndex != -1)
        {
            Raw[removedIndex] = default;
        }
        UpdateSerialization();
        return removedIndex != -1;
    }
    public ObservableList(int defaultSize = 0, T defaultValue = default)
    {
        _list = new List<T>(defaultSize);
        for (int i = 0; i < defaultSize; i++)
            Add(defaultValue);
    }
    
    public ObservableList(List<T> list)
    {
        _list = list;
    }

    public void Swap(int indexA, int indexB)
    {
        (Raw[indexA], Raw[indexB]) = (Raw[indexB], Raw[indexA]);
        UpdateSerialization();
    }
    public T this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    public int Count => _list.Count;
    public List<T> Raw => _list;

    public void Clear()
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            Remove(_list[i]);
        }
        UpdateSerialization();
    }

    public void AssignFrom(List<T> other)
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            if (!other.Contains(_list[i]))
                Remove(_list[i]);
        }
        
        foreach (var item in other)
        {
            if (!_list.Contains(item) && item != null)
                Add(item);
        }

        UpdateSerialization();
    }


    public void SetRawSilently(IEnumerable<T> other)
    {
        _list.Clear();
        _list.AddRange(other);
        UpdateSerialization();
    }
}
    
    [System.Serializable]
    public class BoundedObservableList<T>
    {
        public ObservableList<T> observableList = new ObservableList<T>();
        public int limit = 1;

        public int Count => observableList.Raw.Select(item => item != null).Count();
        public bool IsFull => Count >= limit;
        public IReadOnlyList<T> Raw => observableList.Raw;

        public T this[int index]
        {
            get => observableList[index];
            set => observableList.Set(index, value);
        }

        public void Clear()
        {
            observableList.Raw.Clear();
        }

        public bool TryAdd(T item)
        {
            if (IsFull) return false;
            observableList.Add(item);
            return true;
        }

        public bool RemoveAndSetDefault(T item) => observableList.RemoveAndSetDefault(item);
        public bool RemoveAndSetDefaultSilent(T item) => observableList.RemoveAndSetDefaultSilent(item);
    }
    
    [System.Serializable]
public class ObservableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> _keys = new List<TKey>();
    [SerializeField] private List<TValue> _values = new List<TValue>();

    private Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();

    public Action<TKey, TValue> OnItemAdded;
    public Action<TKey, TValue> OnItemRemoved;
    public Action<TKey, TValue> OnItemChanged;

    public ObservableDictionary() { }

    public ObservableDictionary(Dictionary<TKey, TValue> dict)
    {
        _dict = dict;
    }

    public void Add(TKey key, TValue value)
    {
        _dict.Add(key, value);
        OnItemAdded?.Invoke(key, value);
        OnItemChanged?.Invoke(key, value);

        UpdateSerialization();
    }

    public void UpdateSerialization()
    {
    }

    public void Set(TKey key, TValue value)
    {
        bool isNew = !_dict.ContainsKey(key);
        _dict[key] = value;

        if (isNew)
            OnItemAdded?.Invoke(key, value);
        OnItemChanged?.Invoke(key, value);

        UpdateSerialization();
    }

    public bool Remove(TKey key)
    {
        if (_dict.TryGetValue(key, out var value) && _dict.Remove(key))
        {
            OnItemRemoved?.Invoke(key, value);
            OnItemChanged?.Invoke(key, value);
            UpdateSerialization();
            return true;
        }
        UpdateSerialization();
        return false;
    }

    public bool RemoveAndSetDefault(TKey key)
    {
        if (_dict.TryGetValue(key, out var value))
        {
            _dict[key] = default;
            OnItemRemoved?.Invoke(key, value);
            OnItemChanged?.Invoke(key, value);
            UpdateSerialization();
            return true;
        }
        UpdateSerialization();
        return false;
    }

    public bool RemoveAndSetDefaultSilent(TKey key)
    {
        if (_dict.ContainsKey(key))
        {
            _dict[key] = default;
            UpdateSerialization();
            return true;
        }
        UpdateSerialization();
        return false;
    }

    public bool ContainsKey(TKey key) => _dict.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value);

    public TValue this[TKey key]
    {
        get => _dict[key];
        set => Set(key, value);
    }

    public int Count => _dict.Count;
    public Dictionary<TKey, TValue> Raw => _dict;
    public IEnumerable<TKey> Keys => _dict.Keys;
    public IEnumerable<TValue> Values => _dict.Values;

    public void Clear()
    {
        var keysCopy = new List<TKey>(_dict.Keys);
        for (int i = keysCopy.Count - 1; i >= 0; i--)
        {
            var key = keysCopy[i];
            var value = _dict[key];
            _dict.Remove(key);
            OnItemRemoved?.Invoke(key, value);
            OnItemChanged?.Invoke(key, value);
        }
        UpdateSerialization();
    }

    public void AssignFrom(Dictionary<TKey, TValue> other)
    {
        var keysCopy = new List<TKey>(_dict.Keys);
        foreach (var key in keysCopy)
        {
            if (!other.ContainsKey(key))
                Remove(key);
        }

        foreach (var kvp in other)
        {
            if (!_dict.TryGetValue(kvp.Key, out var existing) || !EqualityComparer<TValue>.Default.Equals(existing, kvp.Value))
                Set(kvp.Key, kvp.Value);
        }

        UpdateSerialization();
    }

    public void SetRawSilently(IEnumerable<KeyValuePair<TKey, TValue>> other)
    {
        _dict.Clear();
        foreach (var kvp in other)
            _dict[kvp.Key] = kvp.Value;

        UpdateSerialization();
    }
    
    public void OnBeforeSerialize()
    {
        _keys.Clear();
        _values.Clear();
        foreach (var kvp in _dict)
        {
            _keys.Add(kvp.Key);
            _values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        _dict = new Dictionary<TKey, TValue>();
        int count = Math.Min(_keys.Count, _values.Count);
        for (int i = 0; i < count; i++)
            _dict[_keys[i]] = _values[i];
    }
    
    
}
    public class CategoryRule<T>
    {
        public string Name;
        public Func<T, bool> Matches;
        public int Limit;

        public CategoryRule(string name, Func<T, bool> matches, int limit)
        {
            Name = name;
            Matches = matches;
            Limit = limit;
        }

        public int CurrentCount(IEnumerable<T> allItems) =>
            allItems.Count(x => x != null && Matches(x));

        public bool IsFull(IEnumerable<T> allItems) =>
            CurrentCount(allItems) >= Limit;
    }

    public class CategorizedObservableList<T>
    {
        public ObservableList<T> observableList = new ObservableList<T>();
        public List<CategoryRule<T>> categoryRules = new List<CategoryRule<T>>();

        public int Count => observableList.Raw.Count(item => item != null);
        
        public int MaxLimit => categoryRules.Sum(el => el.Limit);
        
        public IReadOnlyList<T> Raw => observableList.Raw;

        public CategoryRule<T> GetCategoryRulesByName(string name)
        {
            return  categoryRules.FirstOrDefault(x => x.Name == name);
        }
        

        public CategorizedObservableList(List<CategoryRule<T>> categoryRules)
        {
            this.categoryRules = categoryRules;   
        }
        
        public CategoryRule<T> ResolveCategory(T item)
        {
            foreach (var rule in categoryRules)
                if (rule.Matches(item))
                    return rule;
            return null;
        }

        public T this[int index]
        {
            get => observableList[index];
            set => observableList.Set(index, value);
        }

        public void Clear() => observableList.Clear();

        public bool CanAdd(T item)
        {
            var category = ResolveCategory(item);
            if (category != null && category.IsFull(observableList.Raw))
                return false;
            
            return true;
        }
        
        public bool TryAdd(T item)
        {
            var category = ResolveCategory(item);
            if (category != null && category.IsFull(observableList.Raw))
                return false;

            observableList.Add(item);
            return true;
        }
        
        public (int current, int limit) GetCategoryFill(string name)
        {
            var rule = categoryRules.FirstOrDefault(r => r.Name == name);
            if (rule == null)
                return (Count, MaxLimit);

            return (rule.CurrentCount(observableList.Raw), rule.Limit);
        }

        public bool RemoveAndSetDefault(T item) => observableList.RemoveAndSetDefault(item);
        public bool RemoveAndSetDefaultSilent(T item) => observableList.RemoveAndSetDefaultSilent(item);
    }

    [System.Serializable]
    public class BoundedObservableDictionary<TKey, TValue>
    {
        public ObservableDictionary<TKey, TValue> observableDictionary = new ObservableDictionary<TKey, TValue>();
        public int limit = 1;

        public int Count => observableDictionary.Raw.Count(kvp => kvp.Value != null);
        public bool IsFull => Count >= limit;
        public Dictionary<TKey, TValue> Raw => observableDictionary.Raw;

        public TValue this[TKey key]
        {
            get => observableDictionary[key];
            set => observableDictionary.Set(key, value);
        }

        public void Clear()
        {
            observableDictionary.Clear();
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (IsFull || observableDictionary.ContainsKey(key)) return false;
            observableDictionary.Add(key, value);
            return true;
        }

        public bool RemoveAndSetDefault(TKey key) => observableDictionary.RemoveAndSetDefault(key);
        public bool RemoveAndSetDefaultSilent(TKey key) => observableDictionary.RemoveAndSetDefaultSilent(key);
    }
    
    
    public static class Unsafe
    {
        public static int OffsetOf<T>(FieldInfo field)
        {
            return (int)System.Runtime.InteropServices.Marshal.OffsetOf(typeof(T), field.Name);
        }

        public static unsafe ref T AsRef<T>(void* ptr)
        {
            return ref Unsafe.As<T>(ptr);
        }

        public static unsafe ref T As<T>(void* ptr)
        {
            return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(ptr);
        }

        public static unsafe T* Malloc<T>(int elementsCount = 1) where  T : unmanaged
        {
            var ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * elementsCount, UnsafeUtility.AlignOf<T>(), Unity.Collections.Allocator.Persistent);
            
            if (ptr == null)
                throw new OutOfMemoryException();
                
            return ptr;
        }
        
        public static unsafe T* MallocData<T>(T data) where  T : unmanaged
        {
            var ptr = Malloc<T>();
            *ptr = data;
            
            return ptr;
        }
        
        public static unsafe void Free(void* ptr)
        {
            UnsafeUtility.Free(ptr, Unity.Collections.Allocator.Persistent);
        }
    }

    public unsafe struct @string : IDisposable, IEnumerable<char>, IEquatable<@string>
    {
        private char* data;
        private int length;

        public int Length => length;
        public bool IsEmpty => length == 0;

        public @string(string value)
        {
            if (value == null)
            {
                data = null;
                length = 0;
                return;
            }

            length = value.Length;
            data = Unsafe.Malloc<char>(length + 1);

            for (int i = 0; i < length; i++)
                data[i] = value[i];

            data[length] = '\0';
        }

        public @string(@string value)
        {
            length = value.length;
            data = Unsafe.Malloc<char>(length + 1);

            for (int i = 0; i < length; i++)
                data[i] = value.data[i];

            data[length] = '\0';
        }

        private @string(char* ptr, int len)
        {
            data = ptr;
            length = len;
        }

        public @string Clone()
        {
            return new @string(this);
        }

        public char this[int index]
        {
            get
            {
                if ((uint)index >= (uint)length)
                    throw new IndexOutOfRangeException();

                return data[index];
            }

            set
            {
                if ((uint)index >= (uint)length)
                    throw new IndexOutOfRangeException();

                data[index] = value;
            }
        }

        public Span<char> AsSpan()
        {
            return new Span<char>(data, length);
        }

        public override string ToString()
        {
            if (data == null)
                return string.Empty;

            return new string(data, 0, length);
        }

        public override bool Equals(object obj)
        {
            return obj is @string s && Equals(s);
        }

        public bool Equals(@string other)
        {
            if (length != other.length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (data[i] != other.data[i])
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();

            for (int i = 0; i < length; i++)
                hash.Add(data[i]);

            return hash.ToHashCode();
        }

        public int IndexOf(char c)
        {
            for (int i = 0; i < length; i++)
            {
                if (data[i] == c)
                    return i;
            }

            return -1;
        }

        public bool Contains(char c)
        {
            return IndexOf(c) != -1;
        }

        public bool StartsWith(@string other)
        {
            if (other.length > length)
                return false;

            for (int i = 0; i < other.length; i++)
            {
                if (data[i] != other.data[i])
                    return false;
            }

            return true;
        }

        public bool EndsWith(@string other)
        {
            if (other.length > length)
                return false;

            int start = length - other.length;

            for (int i = 0; i < other.length; i++)
            {
                if (data[start + i] != other.data[i])
                    return false;
            }

            return true;
        }

        public static @string operator +(@string a, @string b)
        {
            char* ptr = Unsafe.Malloc<char>(a.length + b.length + 1);

            for (int i = 0; i < a.length; i++)
                ptr[i] = a.data[i];

            for (int i = 0; i < b.length; i++)
                ptr[a.length + i] = b.data[i];

            ptr[a.length + b.length] = '\0';

            return new @string(ptr, a.length + b.length);
        }

        public static bool operator ==(@string a, @string b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(@string a, @string b)
        {
            return !a.Equals(b);
        }

        public static implicit operator @string(string value)
        {
            return new @string(value);
        }

        public static implicit operator string(@string value)
        {
            return value.ToString();
        }

        public IEnumerator<char> GetEnumerator()
        {
            for (int i = 0; i < length; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            if (data == null)
                return;

            Unsafe.Free(data);

            data = null;
            length = 0;
        }
    }
    
    public static class Utilities
    {
        public static IEnumerator InvokeRepeatedly(Action action, float interval, float delay = 0f)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            while (true)
            {
                action?.Invoke();
                yield return new WaitForSeconds(interval);
            }
        }
        public static IEnumerator InvokeRepeatedly(Action action, float interval, Func<bool> stopCondition, float delay = 0f)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            while (true)
            {
                if (stopCondition != null && stopCondition())
                    yield break;

                action?.Invoke();
                yield return new WaitForSeconds(interval);
            }
        }

        public static IEnumerator Invoke(Action action,float delay = 0f)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            action?.Invoke();
        }
    }
    
    [Obsolete("Dont Use this shit")]
         public static unsafe class Allocator
        {
            public static HashSet<IntPtr> pointers { get; private set; } = new HashSet<IntPtr>();
            public static Vector<GCHandle> fixedManagedMemory = new Vector<GCHandle>();
            public static T* Alloc<T>(T value) where T : unmanaged
            {
                T* ptr = (T*)Marshal.AllocHGlobal(sizeof(T));
                if (ptr == null) throw new OutOfMemoryException("Failed to allocate memory.");
                *ptr = value;
                pointers.Add((IntPtr)ptr);
                return ptr;
            }
            public static T* Alloc<T>() where T : unmanaged
            {
                T* ptr = (T*)Marshal.AllocHGlobal(sizeof(T));
                if (ptr == null) throw new OutOfMemoryException("Failed to allocate memory.");
                pointers.Add((IntPtr)ptr);
                return ptr;
            }
            
            public static GCHandle AllocClass<T>(out IntPtr ptr) where T : class, new()
            {
                T instance = new T();
                GCHandle handle = GCHandle.Alloc(instance, GCHandleType.Pinned);
                ptr = handle.AddrOfPinnedObject();
                fixedManagedMemory.PushBack(handle);
                return handle;
            }
            public static GCHandle AllocClass<T>(T instance, out IntPtr ptr) where T : class
            {
                GCHandle handle = GCHandle.Alloc(instance, GCHandleType.Pinned);
                ptr = handle.AddrOfPinnedObject();
                fixedManagedMemory.PushBack(handle);
                return handle;
            }
            
            public static void Free(GCHandle gcHandle)
            {
                if (!gcHandle.IsAllocated)
                    throw new InvalidOperationException("GCHandle is not allocated.");
                if (!fixedManagedMemory.Contains(gcHandle))
                    throw new InvalidOperationException("Attempted to free unmanaged memory not owned by this allocator.");
                fixedManagedMemory.Remove(gcHandle);
                gcHandle.Free();
            }
            
            public static void Free(void* ptr)
            {
                if (ptr == null) throw new OutOfMemoryException($"null pointer exception with {nameof(ptr)}");
                IntPtr unicTypePointer = (IntPtr)ptr;
                if (!pointers.Remove(unicTypePointer))
                    throw new InvalidOperationException("Attempted to free unmanaged memory not owned by this allocator.");

                Marshal.FreeHGlobal(unicTypePointer);
            }
            
            
            public static T* AllocArray<T>(int count) where T : unmanaged
            {
                T* ptr = (T*)Marshal.AllocHGlobal(sizeof(T) * count);
                if (ptr == null) throw new OutOfMemoryException("Failed to allocate memory.");
                pointers.Add((IntPtr)ptr);
                return ptr;
            }

            public static void CleanAll()
            {
                foreach (var pointer in pointers)
                {
                    Marshal.FreeHGlobal(pointer);
                }
                
                foreach (var gcHandle in fixedManagedMemory)
                {
                    gcHandle.Free();
                }
                fixedManagedMemory.Clear();
                pointers.Clear();
            }
        }
    
        
        public unsafe class Vector<T> : IDisposable, IEnumerable<T> where T : unmanaged
        {
            private T* _buffer;
            private int _capacity;
            private int _size;

            public int Count => _size;
            public int Length => _size;
            public int Size => _size;
            public int Capacity => _capacity;

            public Vector(int initialCapacity = 4)
            {
                if (initialCapacity < 1)
                    initialCapacity = 4;

                _capacity = initialCapacity;
                _size = 0;
                _buffer = (T*)Marshal.AllocHGlobal(sizeof(T) * _capacity);
            }

            public void Clear() => _size = 0;

            public void PushBack(T item)
            {
                if (_size >= _capacity)
                    Grow();

                _buffer[_size++] = item;
            }

            public void Reserve(int newCapacity)
            {
                if (newCapacity <= _capacity) return;

                T* newBuffer = (T*)Marshal.AllocHGlobal(newCapacity * sizeof(T));
                Buffer.MemoryCopy(_buffer, newBuffer, newCapacity * sizeof(T), _size * sizeof(T));
                Marshal.FreeHGlobal((IntPtr)_buffer);
                _buffer = newBuffer;
                _capacity = newCapacity;
            }

            public bool Contains(T item)
            {
                for (int i = 0; i < _size; i++)
                {
                    if (_buffer[i].Equals(item))
                        return true;
                }
                return false;
            }

            public void RemoveAt(int index)
            {
                if ((uint)index >= _size)
                    throw new IndexOutOfRangeException();

                for (int i = index; i < _size - 1; i++)
                    _buffer[i] = _buffer[i + 1];

                _size--;
            }
            public void Remove(T item)
            {
                for (int i = 0; i < _size; i++)
                {
                    if (Equals(_buffer[i], item))
                    {
                        for (int j = i; j < _size - 1; j++)
                        {
                            _buffer[j] = _buffer[j + 1];
                        }

                        _buffer[_size - 1] = default;
                        _size--;
                        return;
                    }
                }
            }

            public void Insert(int index, T item)
            {
                if ((uint)index > _size)
                    throw new IndexOutOfRangeException();

                if (_size >= _capacity)
                    Grow();

                for (int i = _size; i > index; i--)
                    _buffer[i] = _buffer[i - 1];

                _buffer[index] = item;
                _size++;
            }

            public T First => _size > 0 ? _buffer[0] : throw new InvalidOperationException("Vector is empty.");
            public T Last => _size > 0 ? _buffer[_size - 1] : throw new InvalidOperationException("Vector is empty.");

            public T this[int index]
            {
                get
                {
                    if ((uint)index >= _size) throw new IndexOutOfRangeException();
                    return _buffer[index];
                }
                set
                {
                    if ((uint)index >= _size) throw new IndexOutOfRangeException();
                    _buffer[index] = value;
                }
            }

            private void Grow() => Reserve(_capacity > 0 ? _capacity * 2 : 4);


            public void Dispose()
            {
                if (_buffer != null)
                {
                    Marshal.FreeHGlobal((IntPtr)_buffer);
                    _buffer = null;
                    _size = 0;
                    _capacity = 0;
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < _size; i++)
                {
                    yield return GetElement(i);
                }
            }

            public T GetElement(int i)
            {
                return _buffer[i];
            }
        }


   
}