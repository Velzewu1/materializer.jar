using UnityEngine;

public class SceneReloadBootstrapper : MonoBehaviour
{
    public LevelSwitcher switcher;

    void Awake()
    {
        if (!switcher) switcher = GameObject.FindAnyObjectByType<LevelSwitcher>();
        if (switcher && switcher.LevelCount > 0)
        {
            int idx = Mathf.Clamp(SceneReload.GetSavedIndex(0), 0, switcher.LevelCount - 1);
            switcher.LoadIndex(idx);
        }
    }

    [ContextMenu("Reset Progress")]
    void ResetProgress()
    {
        SceneReload.Clear();
        if (switcher && switcher.LevelCount > 0) switcher.LoadIndex(0);
    }
}
