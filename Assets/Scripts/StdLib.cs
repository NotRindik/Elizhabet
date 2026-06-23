using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

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
        
    }

[System.Serializable]
public class ObservableList<T>
{
    [SerializeField] private List<T> _list = new List<T>();

    public Action<T> OnItemAdded;
    public Action<T> OnItemRemoved;
    public Action<T> OnItemChanged;

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
        _list[i]   = item;
        OnItemAdded?.Invoke(item);
        OnItemChanged?.Invoke(item);

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

        //�������� � ��� ��� ��� ��������� ��������� � ����� � ���������� ��� ���������� ������ 5 ��
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

    public unsafe static class ReflectionRef
    {
        public static ref T GetRef<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                throw new Exception($"Field {fieldName} not found");

            return ref GetRef<T>(obj, field);
        }

        public static ref T GetRef<T>(object obj, FieldInfo field)
        {
            // адрес объекта
            TypedReference tr = __makeref(obj);

            // pointer на объект в памяти
            IntPtr objPtr = **(IntPtr**)(&tr);

            // узнаем смещение поля внутри объекта
            int offset = Unsafe.OffsetOf<object>(field);

            // адрес поля = адрес объекта + смещение
            byte* fieldPtr = (byte*)objPtr + offset;

            // превращаем в ref T
            return ref Unsafe.AsRef<T>(fieldPtr);
        }
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