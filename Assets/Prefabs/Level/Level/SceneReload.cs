using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneReload
{
    const string PP_KEY = "stepIndex";

    // --- Save/Get/Clear ---
    public static void    SaveIndex(int index) { PlayerPrefs.SetInt(PP_KEY, Mathf.Max(0,index)); PlayerPrefs.Save(); }
    public static int     GetSavedIndex(int def = 0) => PlayerPrefs.GetInt(PP_KEY, def);
    public static void    Clear() => PlayerPrefs.DeleteKey(PP_KEY);

    // --- Reload helpers ---
    public static void ReloadActive()
    {
        var idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);
    }

    // Сохранить ИНДЕКС, затем перезагрузить сцену
    public static void ReloadWithIndex(int index)
    {
        SaveIndex(index);
        ReloadActive();
    }

    // Перезагрузить со следующим индексом (с учётом loop)
    public static void ReloadNextFrom(int current, int levelCount, bool loop)
    {
        if (levelCount <= 0) { ReloadActive(); return; }
        int next = current + 1;
        if (next >= levelCount) next = loop ? 0 : levelCount - 1;
        ReloadWithIndex(next);
    }
}
