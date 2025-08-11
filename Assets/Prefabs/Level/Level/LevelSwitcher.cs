using UnityEngine;

public class LevelSwitcher : MonoBehaviour
{
    public PuzzleController controller; // укажи ссылку на контроллер
    public LevelData[] levels;          // назначь массив уровней в инспекторе
    int index = -1;

    [ContextMenu("Next Level")]
    public void Next()
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        index = (index + 1) % levels.Length;
        controller.SetLevel(levels[index]);
    }

    [ContextMenu("Prev Level")]
    public void Prev()
    {
        if (levels == null || levels.Length == 0 || controller == null) return;
        index = (index - 1 + levels.Length) % levels.Length;
        controller.SetLevel(levels[index]);
    }
}
