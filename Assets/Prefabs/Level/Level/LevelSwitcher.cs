using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LevelSwitcher : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;     // ссылка на контроллер
    public LevelData[] levels;              // плейлист уровней

    [Header("Win sequence between levels")]
    public bool autoAdvanceOnWin = true;
    [Min(0f)] public float materializeDuration = 1.0f;
    [Min(0f)] public float cameraFocusDuration  = 1.5f;
    public bool loop = true;

    [Header("Lose behaviour")]
    public bool restartOnLose = true;
    [Min(0f)] public float loseRestartDelay = 0.0f; // можно поставить 0.2–0.5с для вспышки/звукa

    [Header("Hooks")]
    public UnityEvent OnMaterializeBegin;
    public UnityEvent OnMaterializeEnd;
    public UnityEvent OnCameraFocusBegin;
    public UnityEvent OnCameraFocusEnd;
    public UnityEvent OnLevelAboutToChange;

    int index = -1;
    Coroutine winRoutine;
    Coroutine loseRoutine;

    void Awake()
    {
        if (!controller) controller = GameObject.FindAnyObjectByType<PuzzleController>();
    }

    void OnEnable()
    {
        if (controller)
        {
            controller.OnWinEvent  += HandleWin;
            controller.OnLoseEvent += HandleLose;
        }
    }

    void OnDisable()
    {
        if (controller)
        {
            controller.OnWinEvent  -= HandleWin;
            controller.OnLoseEvent -= HandleLose;
        }
        if (winRoutine  != null) { StopCoroutine(winRoutine);  winRoutine  = null; }
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
    }

    [ContextMenu("Next Level")]
    public void Next()
    {
        if (!IsReady()) return;
        int next = index + 1;
        if (next >= levels.Length)
        {
            if (!loop) return;
            next = 0;
        }
        SetIndex(next);
    }

    [ContextMenu("Prev Level")]
    public void Prev()
    {
        if (!IsReady()) return;
        int prev = index - 1;
        if (prev < 0) prev = loop ? levels.Length - 1 : 0;
        SetIndex(prev);
    }

    public void LoadIndex(int i)
    {
        if (!IsReady() || i < 0 || i >= levels.Length) return;
        SetIndex(i);
    }

    public void RestartCurrent()
    {
        if (!IsReady())
            return;

        if (index < 0) index = 0; // на всякий случай
        SetIndex(index);
    }

    void SetIndex(int i)
    {
        index = i;
        var data = levels[index];
        controller.SetLevel(data); // сброс состояний и страйков внутри PuzzleController

        // отменяем висящие корутины
        if (winRoutine  != null) { StopCoroutine(winRoutine);  winRoutine  = null; }
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
    }

    bool IsReady() => (levels != null && levels.Length > 0 && controller != null);

    // === Победа → последовательность → Next ===
    void HandleWin()
    {
        if (!autoAdvanceOnWin) return;
        if (winRoutine != null) StopCoroutine(winRoutine);
        winRoutine = StartCoroutine(WinSequence());
    }

    IEnumerator WinSequence()
    {
        if (materializeDuration > 0f)
        {
            OnMaterializeBegin?.Invoke();
            yield return new WaitForSeconds(materializeDuration);
            OnMaterializeEnd?.Invoke();
        }

        if (cameraFocusDuration > 0f)
        {
            OnCameraFocusBegin?.Invoke();
            yield return new WaitForSeconds(cameraFocusDuration);
            OnCameraFocusEnd?.Invoke();
        }

        OnLevelAboutToChange?.Invoke();
        Next();
        winRoutine = null;
    }

    // === Поражение → рестарт текущего уровня ===
    void HandleLose()
    {
        if (!restartOnLose) return;
        if (loseRoutine != null) StopCoroutine(loseRoutine);
        loseRoutine = StartCoroutine(RestartAfterLose());
    }

    IEnumerator RestartAfterLose()
    {
        if (loseRestartDelay > 0f)
            yield return new WaitForSeconds(loseRestartDelay);

        RestartCurrent();
        loseRoutine = null;
    }
}
