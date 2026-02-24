using UnityEngine;

public interface ITransitionEndPoint { }
public interface ITransitionStartPoint { }
public class SceneTrans : MonoBehaviour, ITransitionStartPoint, ITransitionEndPoint
{

}
