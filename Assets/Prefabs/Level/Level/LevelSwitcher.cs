using UnityEngine;

public class LevelSwitcher : MonoBehaviour
{
    public PuzzleController controller; // укажи ссылку на контроллер в сцене
    public LevelData[] levels;          // назначь уровни в инспекторе

    [Min(0)] public int index = 0;

    public int LevelCount  => (levels != null) ? levels.Length : 0;
    public int CurrentIndex => index;

    void Start()
    {
        if (controller && levels != null && levels.Length > 0)
            ApplyIndex();
    }

    public void LoadIndex(int newIndex)   // <-- добавлено под твой ProgressController
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        index = Mathf.Clamp(newIndex, 0, levels.Length - 1);
        ApplyIndex();
    }

    public void SetIndex(int newIndex) => LoadIndex(newIndex); // алиас

    public void RestartCurrent() => ApplyIndex();

    [ContextMenu("Next Level")]
    public void Next()
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        index = (index + 1) % levels.Length;
        ApplyIndex();
    }

    [ContextMenu("Prev Level")]
    public void Prev()
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        index = (index - 1 + levels.Length) % levels.Length;
        ApplyIndex();
    }

    void ApplyIndex()
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        var data = levels[Mathf.Clamp(index, 0, levels.Length - 1)];
        controller.SetLevel(data);
    }
}
