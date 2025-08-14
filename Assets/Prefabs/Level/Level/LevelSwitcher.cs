using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LevelSwitcher : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;
    public LevelData[] levels;

    [Header("Transition mode")]
    public bool  reloadSceneOnTransition = true; // TRUE: все переходы через SceneReload
    public bool  autoAdvanceOnWin = true;
    [Min(0f)] public float materializeDuration = 1.0f;
    [Min(0f)] public float cameraFocusDuration  = 1.5f;
    public bool  loop = true;

    [Header("Lose behaviour")]
    public bool  restartOnLose   = true;
    [Min(0f)] public float loseRestartDelay = 0.0f;

    [Header("Hooks")]
    public UnityEvent OnMaterializeBegin, OnMaterializeEnd;
    public UnityEvent OnCameraFocusBegin,  OnCameraFocusEnd;
    public UnityEvent OnLevelAboutToChange;

    int index = -1;
    Coroutine winRoutine, loseRoutine;
    bool transitioning;

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
        if (winRoutine  != null) StopCoroutine(winRoutine);
        if (loseRoutine != null) StopCoroutine(loseRoutine);
        transitioning = false;
    }

    public int LevelCount   => levels?.Length ?? 0;
    public int CurrentIndex => index;

    public void LoadIndex(int i)
    {
        if (!IsReady() || i < 0 || i >= levels.Length) return;
        SetIndex(i);
    }

    public void RestartCurrent()
    {
        if (!IsReady()) return;
        if (index < 0) index = 0;
        SetIndex(index);
    }

    [ContextMenu("Next Level")]
    public void Next()
    {
        if (!IsReady()) return;
        int next = NextIndex(index);
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

    void SetIndex(int i)
    {
        index = i;
        controller.SetLevel(levels[index]);   // Полный сброс/перебилд в PuzzleController
        if (winRoutine  != null) { StopCoroutine(winRoutine);  winRoutine  = null; }
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
        transitioning = false;
    }

    int  NextIndex(int from) => (from + 1 < LevelCount) ? from + 1 : (loop ? 0 : LevelCount - 1);
    bool IsReady() => (levels != null && levels.Length > 0 && controller != null);

    // === WIN ===
    void HandleWin()
    {
        if (!autoAdvanceOnWin || transitioning) return;
        if (winRoutine != null) StopCoroutine(winRoutine);
        winRoutine = StartCoroutine(WinSequence());
    }

    IEnumerator WinSequence()
    {
        transitioning = true;

        if (materializeDuration > 0f) { OnMaterializeBegin?.Invoke(); yield return new WaitForSeconds(materializeDuration); OnMaterializeEnd?.Invoke(); }
        if (cameraFocusDuration  > 0f) { OnCameraFocusBegin ?.Invoke(); yield return new WaitForSeconds(cameraFocusDuration ); OnCameraFocusEnd ?.Invoke(); }

        OnLevelAboutToChange?.Invoke();

        if (reloadSceneOnTransition)
            SceneReload.ReloadNextFrom(index, LevelCount, loop);
        else
            SetIndex(NextIndex(index));

        winRoutine = null;
        transitioning = false;
    }

    // === LOSE ===
    void HandleLose()
    {
        if (!restartOnLose || transitioning) return;
        if (loseRoutine != null) StopCoroutine(loseRoutine);
        loseRoutine = StartCoroutine(RestartAfterLose());
    }

    IEnumerator RestartAfterLose()
    {
        transitioning = true;

        if (loseRestartDelay > 0f)
            yield return new WaitForSeconds(loseRestartDelay);

        if (reloadSceneOnTransition)
            SceneReload.ReloadWithIndex(Mathf.Clamp(index, 0, LevelCount - 1)); // рестарт текущего
        else
            RestartCurrent();

        loseRoutine = null;
        transitioning = false;
    }
}
