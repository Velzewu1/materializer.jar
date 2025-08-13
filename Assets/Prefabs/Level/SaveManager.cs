using System.IO;
using UnityEngine;

public static class SaveManager
{
    public static SaveData Data { get; private set; } = new SaveData();

    public static void Load()
    {
        try
        {
            var path = SavePaths.FilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                Data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            else
            {
                Data = new SaveData();
                Save();
            }
            Debug.Log($"[Save] Loaded: {path} (stepIndex={Data.stepIndex})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] Load failed: {e.Message}");
            Data = new SaveData();
        }
    }

    public static void Save()
    {
        try
        {
            var json = JsonUtility.ToJson(Data, prettyPrint: true);
            File.WriteAllText(SavePaths.FilePath, json);
            Debug.Log($"[Save] Saved: {SavePaths.FilePath} (stepIndex={Data.stepIndex})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] Save failed: {e.Message}");
        }
    }

    public static void ResetAll()
    {
        Data = new SaveData();
        Save();
    }
}
