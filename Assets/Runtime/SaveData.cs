using System;
using UnityEngine;

[Serializable]
public class SaveData
{
    public string version = "1.0.0";
    public int stepIndex = 0;          // На какой шаг перейти при старте (0..N-1)
    public int totalStrikes = 0;       // Суммарные ошибки (по желанию)

    // Место для настроек/опций
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.9f;
}

public static class SavePaths
{
    public static string FilePath =>
        System.IO.Path.Combine(Application.persistentDataPath, "save.json");
}
