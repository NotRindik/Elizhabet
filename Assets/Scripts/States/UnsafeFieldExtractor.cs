using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static unsafe class UnsafeFieldExtractor
{
    public static Dictionary<string, object> ExtractFields(object obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var result = new Dictionary<string, object>();
        Type current = obj.GetType();
        void* ptr = obj.GetInternalPointer();

        while (current != null && current != typeof(object))
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                int offset = GetFieldOffsetManual(obj, field);
                byte* fieldPtr = (byte*)ptr + offset;

                try
                {
                    object value = ReadRawValue(fieldPtr, field.FieldType);
                    result[field.Name] = value;
                }
                catch
                {
                    result[field.Name] = $"[UNSUPPORTED: {field.FieldType}]";
                }
            }

            current = current.BaseType;
        }

        return result;
    }
    private static int GetFieldOffsetManual(object instance, FieldInfo field)
    {
        TypedReference tr = __makeref(instance);
        IntPtr basePtr = *(IntPtr*)(&tr);

        // Создаём ссылку на поле
        object fieldValue = field.GetValue(instance);
        TypedReference trField = __makeref(fieldValue);
        IntPtr fieldPtr = *(IntPtr*)(&trField);

        return (int)((byte*)fieldPtr - (byte*)basePtr);
    }


    private static object ReadRawValue(byte* fieldPtr, Type fieldType)
    {
        if (fieldType == typeof(int)) return *(int*)fieldPtr;
        if (fieldType == typeof(float)) return *(float*)fieldPtr;
        if (fieldType == typeof(bool)) return *(bool*)fieldPtr;
        if (fieldType == typeof(double)) return *(double*)fieldPtr;
        if (fieldType == typeof(byte)) return *fieldPtr;

        if (!fieldType.IsValueType || typeof(object).IsAssignableFrom(fieldType))
            return Unsafe.Read<object>(fieldPtr);

        throw new NotSupportedException($"Unsupported type: {fieldType}");
    }
}
