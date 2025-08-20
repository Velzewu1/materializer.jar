using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LevelSwitcher : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;
    public LevelData[] levels;

    [Header("Cinemachine")]
    public CinemachineLevelCameraApplier cm;   // висит на том же GO, что и vcam
    public bool applyCameraPerLevel = true;
    public float fallbackCameraDistance = 1.7f;

    [Header("Transition mode")]
    public bool  reloadSceneOnTransition = true;   // <-- теперь реально используется
    public bool  autoAdvanceOnWin = true;
    [Min(0f)] public float materializeDuration = 1.0f;
    [Min(0f)] public float cameraFocusDuration  = 1.5f;
    public bool  loop = true;
    public bool  coldRestartTwice = true;

    [Header("Lose behaviour")]
    public bool  restartOnLose   = true;
    [Min(0f)] public float loseRestartDelay = 0.0f;

    [Header("Hooks")]
    public UnityEvent OnMaterializeBegin, OnMaterializeEnd;
    public UnityEvent OnCameraFocusBegin,  OnCameraFocusEnd;
    public UnityEvent OnLevelAboutToChange;

    int index = -1;
    Coroutine winRoutine, loseRoutine, _applyCamCo;
    bool transitioning;

    public int LevelCount   => levels?.Length ?? 0;
    public int CurrentIndex => index;

    void Awake()
    {
        if (!controller) controller = GameObject.FindAnyObjectByType<PuzzleController>();
        if (!cm)         cm         = GameObject.FindAnyObjectByType<CinemachineLevelCameraApplier>();
    }

    void OnEnable()
    {
        if (controller)
        {
            controller.OnWinEvent  += HandleWin;
            controller.OnLoseEvent += HandleLose;
        }
    }

    void Start()
    {
        if (!IsReady()) return;

        int startIdx = SceneReloader.HasPending
            ? Mathf.Clamp(SceneReloader.ConsumePendingIndex(), 0, LevelCount - 1)
            : Mathf.Clamp(index < 0 ? 0 : index, 0, LevelCount - 1);

        ApplyIndexLocally(startIdx);
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
        if (_applyCamCo != null) StopCoroutine(_applyCamCo);
        transitioning = false;
    }

    // ---------- PUBLIC API ----------
    public void LoadIndex(int i)
    {
        if (!IsReady() || i < 0 || i >= levels.Length) return;
        // Уважаем режим перехода
        if (reloadSceneOnTransition)
            RequestSceneReloadToIndex(i);
        else
            ApplyIndexLocally(i);
    }

    public void RestartCurrent()
    {
        if (!IsReady()) return;
        if (index < 0) index = 0;
        if (reloadSceneOnTransition)
            RequestSceneReloadToIndex(index);
        else
            ApplyIndexLocally(index);
    }

    [ContextMenu("Next Level")]
    public void Next()
    {
        if (!IsReady()) return;
        int next = NextIndex(index);
        if (reloadSceneOnTransition)
            RequestSceneReloadToIndex(next);
        else
            ApplyIndexLocally(next);
    }

    [ContextMenu("Prev Level")]
    public void Prev()
    {
        if (!IsReady()) return;
        int prev = index - 1;
        if (prev < 0) prev = loop ? levels.Length - 1 : 0;

        if (reloadSceneOnTransition)
            RequestSceneReloadToIndex(prev);
        else
            ApplyIndexLocally(prev);
    }

    // ---------- CORE ----------
    void ApplyIndexLocally(int i)
    {
        index = Mathf.Clamp(i, 0, Mathf.Max(0, LevelCount - 1));

        if (controller != null && levels != null && index >= 0 && index < levels.Length)
        {
            var ld = levels[index];
            controller.SetLevel(ld);

            DialogueSystem.I?.SetCurrentLevelIndex(index);

            // CAM switcher
            var camSw = FindAnyObjectByType<CinemachineLevelCameraSwitcher>();
            if (camSw)
            {
                camSw.SetCurrentLevelIndex(index);
                camSw.HideFocus();
            }

            // применить камеру сейчас и на следующий кадр
            if (_applyCamCo != null) StopCoroutine(_applyCamCo);
            _applyCamCo = StartCoroutine(ApplyPerLevelCameraNowAndNextFrame(ld));
        }

        if (winRoutine  != null) { StopCoroutine(winRoutine);  winRoutine  = null; }
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
        transitioning = false;
    }

    IEnumerator ApplyPerLevelCameraNowAndNextFrame(LevelData ld)
    {
        ApplyPerLevelCamera(ld);
        yield return null;
        ApplyPerLevelCamera(ld);
        _applyCamCo = null;
    }

    void RequestSceneReloadToIndex(int i)
    {
        int clamped = Mathf.Clamp(i, 0, Mathf.Max(0, LevelCount - 1));
        // Через SceneReloader (жёсткий ресет сцены)
        SceneReloader.ReloadWithIndex(clamped, coldRestartTwice);
    }

    void ApplyPerLevelCamera(LevelData ld)
    {
        if (!applyCameraPerLevel || cm == null || ld == null) return;
        if (!ld.cameraOverride) return;

        var composer = cm.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>()
                   ?? cm.GetComponentInChildren<Unity.Cinemachine.CinemachinePositionComposer>(true);
        if (!composer) return;

        float toDistance = ld.cameraDistance > 0f ? ld.cameraDistance : fallbackCameraDistance;
        float blendTime  = Mathf.Max(0f, ld.cameraBlendTime);

        if (blendTime <= 0f)
        {
            composer.CameraDistance = toDistance;
        }
        else
        {
            StartCoroutine(LerpComposerDistance(composer, composer.CameraDistance, toDistance, blendTime));
        }
    }

    IEnumerator LerpComposerDistance(Unity.Cinemachine.CinemachinePositionComposer comp, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration && comp)
        {
            float k = t / duration;
            comp.CameraDistance = Mathf.Lerp(from, to, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (comp) comp.CameraDistance = to;
    }

    int  NextIndex(int from) => (from + 1 < LevelCount) ? from + 1 : (loop ? 0 : Mathf.Max(0, LevelCount - 1));
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

        if (materializeDuration > 0f)
        {
            OnMaterializeBegin?.Invoke();
            yield return new WaitForSecondsRealtime(materializeDuration);
            OnMaterializeEnd?.Invoke();
        }

        if (cameraFocusDuration > 0f)
        {
            OnCameraFocusBegin?.Invoke();
            yield return new WaitForSecondsRealtime(cameraFocusDuration);
            OnCameraFocusEnd?.Invoke();
        }

        OnLevelAboutToChange?.Invoke();

        // Не перезагружаем здесь — даём закончиться победной сценке (VictorySequenceController вызовет Next()).
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
            yield return new WaitForSecondsRealtime(loseRestartDelay);

        if (reloadSceneOnTransition)
            RequestSceneReloadToIndex(Mathf.Clamp(index, 0, LevelCount - 1));
        else
            ApplyIndexLocally(Mathf.Clamp(index, 0, LevelCount - 1));

        loseRoutine = null;
        transitioning = false;
    }
}
