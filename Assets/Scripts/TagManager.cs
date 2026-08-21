using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class TagManager : SerializedMonoBehaviour
{

    public List<ITag> Tags = new();
    private Dictionary<Type, ITag> _tags = new();

    private void Awake()
    {
        foreach (var tags in Tags)
        {
            _tags.Add(tags.GetType(), tags);
        }
    }


    public T GetTag<T>()
    {
        return (T)_tags[typeof(T)];
    }
    
    public bool TryGetTag<T>( out T target)
    {
        bool result = _tags.TryGetValue(typeof(T),out ITag temp);
        target = (T)temp;
        return result;
    }
}

public interface ITag
{
    
}
