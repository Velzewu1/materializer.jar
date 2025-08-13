using UnityEngine;

/// Склеивает сохранение прогресса и переключение уровней.
public class ProgressController : MonoBehaviour
{
    [Header("Refs")]
    public LevelSwitcher switcher;       // укажи объект с LevelSwitcher
    public PuzzleController controller;  // укажи PuzzleController (или найдётся автоматически)

    [Header("Behaviour")]
    [Tooltip("Если true — сразу переключаем уровень после победы (без ожидания анимации).")]
    public bool autoAdvanceOnWin = false;

    void Awake()
    {
        if (switcher == null) switcher = GameObject.FindAnyObjectByType<LevelSwitcher>();
        if (controller == null)
        {
            // пробуем взять из свитчера, иначе ищем в сцене
            controller = switcher != null ? switcher.controller : GameObject.FindAnyObjectByType<PuzzleController>();
        }

        SaveManager.Load();

        if (switcher != null && switcher.LevelCount > 0)
        {
            int idx = Mathf.Clamp(SaveManager.Data.stepIndex, 0, switcher.LevelCount - 1);
            switcher.LoadIndex(idx); // теперь есть в LevelSwitcher
        }
    }

    void OnEnable()
    {
        if (controller != null)
        {
            controller.OnWinEvent  += HandleWin;   // <= ВАЖНО: подписываемся на OnWinEvent
            controller.OnLoseEvent += HandleLose;  // опционально: перезапуск/логика поражения
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnWinEvent  -= HandleWin;
            controller.OnLoseEvent -= HandleLose;
        }
    }

    void HandleWin()
    {
        if (switcher == null || switcher.LevelCount == 0) return;

        // сохраняем прогресс: следующий индекс, но не дальше последнего
        int next = Mathf.Min(switcher.CurrentIndex + 1, switcher.LevelCount - 1);
        SaveManager.Data.stepIndex = next;
        SaveManager.Save();

        if (autoAdvanceOnWin)
        {
            switcher.LoadIndex(next); // либо подожди анимацию и дерни AdvanceAfterReveal()
        }
    }

    void HandleLose()
    {
        // Прогресс не меняем. Если хочешь — можно перезапустить после анимации поражения.
        // switcher.RestartCurrent();
    }

    /// Вызывай из анимации материализации, если autoAdvanceOnWin=false
    public void AdvanceAfterReveal()
    {
        if (switcher == null) return;
        int idx = Mathf.Clamp(SaveManager.Data.stepIndex, 0, switcher.LevelCount - 1);
        switcher.LoadIndex(idx);
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        SaveManager.ResetAll();
        if (switcher != null && switcher.LevelCount > 0)
            switcher.LoadIndex(0);
    }
}
