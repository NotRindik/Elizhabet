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
         
        public unsafe sealed class UniquePtr<T> : IDisposable where T : unmanaged
        {
            private T* _ptr;
            private bool _disposed;

            public UniquePtr()
            {
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    throw new InvalidOperationException("T cannot contain references.");
                _ptr = (T*)Marshal.AllocHGlobal(sizeof(T));
                *_ptr = default;
            }


            public UniquePtr(T value) : this() => *_ptr = value;

            public T* Ptr => _disposed ? throw new ObjectDisposedException(nameof(UniquePtr<T>)) : _ptr;

            public ref T Value => ref *_ptr;

            public void Dispose()
            {
                if (!_disposed)
                {
                    Marshal.FreeHGlobal((IntPtr)_ptr);
                    _ptr = null;
                    _disposed = true;
                    GC.SuppressFinalize(this);
                }
            }
            
            public UniquePtr<T> Move()
            {
                var moved = new UniquePtr<T>();
                moved._ptr = _ptr;
                _ptr = null;
                _disposed = true;
                return moved;
            }

            ~UniquePtr() => Dispose();
            
            private UniquePtr(UniquePtr<T> other) { }
            public UniquePtr<T> Clone() => new UniquePtr<T>(*_ptr);
        }
        
        public unsafe sealed class SharedPtr<T> : IDisposable where T : unmanaged
        {
            public sealed class ControlBlock
            {
                public T* Ptr;
                public int StrongRefCount;
                public int WeakRefCount;
            }

            private ControlBlock _control;

            internal  SharedPtr(ControlBlock control)
            {
                _control = control;
            }
            public ControlBlock GetControlBlock() => _control;

            public SharedPtr()
            {
                _control = new ControlBlock
                {
                    Ptr = (T*)Marshal.AllocHGlobal(sizeof(T)),
                    StrongRefCount = 1,
                    WeakRefCount = 0
                };
                *_control.Ptr = default;
            }


            public SharedPtr(T value)
            {
                _control = new ControlBlock
                {
                    Ptr = (T*)Marshal.AllocHGlobal(sizeof(T)),
                    StrongRefCount = 1,WeakRefCount = 0
                };
                *_control.Ptr = value;
            }
            
            public SharedPtr(SharedPtr<T> other)
            {
                _control = other._control;
                Interlocked.Increment(ref _control.StrongRefCount);
            }

            public T* Ptr => _control.Ptr == null ? throw new ObjectDisposedException(nameof(SharedPtr<T>)) : _control.Ptr;
            public ref T Value => ref *_control.Ptr;

            public int UseCount => _control?.StrongRefCount ?? 0;

            public void Dispose()
            {
                if (_control != null)
                {
                    if (Interlocked.Decrement(ref _control.StrongRefCount) == 0)
                    {
                        Marshal.FreeHGlobal((IntPtr)_control.Ptr);
                        _control.Ptr = null;
                    }
                    _control = null;
                }
            }

            ~SharedPtr()
            {
                Dispose();
            }
        }

        public unsafe sealed class WeakPtr<T> : IDisposable where T : unmanaged
        {
            private SharedPtr<T>.ControlBlock _control;


            public WeakPtr(SharedPtr<T> shared)
            {
                if (shared == null) throw new ArgumentNullException(nameof(shared));
                _control = shared.GetControlBlock();
                Interlocked.Increment(ref _control.WeakRefCount);
            }

            public SharedPtr<T>? Lock()
            {
                while (true)
                {
                    int current = _control.StrongRefCount;
                    if (current == 0) return null;

                    if (Interlocked.CompareExchange(ref _control.StrongRefCount, current + 1, current) == current)
                        return new SharedPtr<T>(_control);
                }
            }

            private static SharedPtr<T>.ControlBlock GetControl(SharedPtr<T> shared)
            {
                var field = typeof(SharedPtr<T>).GetField("_control", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (SharedPtr<T>.ControlBlock)field.GetValue(shared);
            }

            ~WeakPtr()
            {
                if (_control != null)
                {
                    if (Interlocked.Decrement(ref _control.WeakRefCount) == 0 && _control.StrongRefCount == 0)
                    {
                        Marshal.FreeHGlobal((IntPtr)_control.Ptr);
                        _control.Ptr = null;
                    }
                }
            }
            public void Reset()
            {
                Dispose();
                _control = null;
            }
            public void Dispose()
            {
                if (_control != null)
                {
                    if (Interlocked.Decrement(ref _control.WeakRefCount) == 0 && _control.StrongRefCount == 0)
                    {
                        Marshal.FreeHGlobal((IntPtr)_control.Ptr);
                        _control.Ptr = null;
                    }
                    _control = null;
                }
                GC.SuppressFinalize(this);
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