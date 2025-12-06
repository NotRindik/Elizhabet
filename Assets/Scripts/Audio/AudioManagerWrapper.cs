using System;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManagerWrapper : MonoBehaviour
{
    public void PlaySFX(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            Debug.LogWarning("PlaySFX: empty string.");
            return;
        }

        // Разбиваем строку по пробелам
        string[] parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return;

        string filepath = parts[0];
        float volume = 1f;
        float pitch = 1f;
        bool loop = false;
        AudioMixerGroup mixer = null; // позже можешь добавить имя миксера

        // Парсим параметры
        for (int i = 1; i < parts.Length; i++)
        {
            string part = parts[i];

            if (part.StartsWith("-v") && float.TryParse(part.Substring(1), out float v))
                volume = v;

            else if (part.StartsWith("-p") && float.TryParse(part.Substring(1), out float p))
                pitch = p;

            else if (part.StartsWith("-l") && int.TryParse(part.Substring(1), out int l))
                loop = l != 0;

            // можно добавить `m<имя>` для миксера, если нужно
        }

        AudioManager.instance.PlaySoundEffect(filepath, mixer, volume, pitch, loop);
    }
}
