using UnityEngine;

using UnityEngine.SceneManagement;
public class SceneManagerWrapper : MonoBehaviour
{
    public void ChangeScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    public void ChangeScene(Scene scene)
    {
        SceneManager.LoadScene(scene.name);
    }
}
