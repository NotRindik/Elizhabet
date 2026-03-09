using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public AudioMixer mixer;

    public const string Music = "Music", Sfx = "SFX", Master = "Master";

    public Slider MusicSlider, SFXSlider, MasterSlider;

    private void Awake()
    {
        MusicSlider.value = PlayerPrefs.GetFloat(Music, 0.5f);
        SFXSlider.value = PlayerPrefs.GetFloat(Sfx, 0.5f);
        MasterSlider.value = PlayerPrefs.GetFloat(Master, 1f);
    }

    public void ChangeMusic(float vol) => ChangeVolume(vol, Music);
    public void ChangeSFX(float vol) => ChangeVolume(vol, Sfx);
    public void ChangeMaster(float vol) => ChangeVolume(vol, Master);

    public void ChangeVolume(float vol, string name)
    {
        float db;

        if (vol <= 0.0001f)
        {
            db = -80f;
        }
        else
        {
            db = Mathf.Log10(vol) * 20f;

            if (db < -30f)
                db = -80f;
        }

        mixer.SetFloat(name, db);
        PlayerPrefs.SetFloat(name, vol);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
