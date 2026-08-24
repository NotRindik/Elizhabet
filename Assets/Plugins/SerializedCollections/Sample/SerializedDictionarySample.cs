using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AYellowpaper.SerializedCollections
{
    public class SerializedDictionarySample : MonoBehaviour
    {
        [SerializedDictionary("Element Type", "Description")]
        public SerializedDictionary<ElementType, string> ElementDescriptions;
        
        public enum ElementType
        {
            Fire,
            Air,
            Earth,
            Water
        }
    }
}