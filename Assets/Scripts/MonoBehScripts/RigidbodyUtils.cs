using System;
using UnityEngine;

public class RigidbodyUtils : MonoBehaviour
{
    private Rigidbody2D _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }
    public void BodyType(string s)
    {
        if (!Enum.TryParse<RigidbodyType2D>(s, out var result))
        {
            if (s.ToLower() == "k")
                _rb.bodyType = RigidbodyType2D.Kinematic;
            else if (s.ToLower() == "d")
                _rb.bodyType = RigidbodyType2D.Dynamic;
            else if (s.ToLower() == "s")
                _rb.bodyType = RigidbodyType2D.Static;
        }
        
        
        _rb.bodyType = result;
    }
}
