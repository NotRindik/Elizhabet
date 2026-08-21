using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponType", menuName = "WeaponsData/WeaponType")]
public class WeaponType : SerializedScriptableObject
{
    public List<PiercingData> piercingDatas;

    public PiercingData GetPiercingByBody(BodyType bodyType)
    {
        return piercingDatas.Find(x => x.bodyType == bodyType);
    }
}

[System.Serializable]
public class PiercingData
{
    public BodyType bodyType;
    public float damageMultiplier = 1f;
}
