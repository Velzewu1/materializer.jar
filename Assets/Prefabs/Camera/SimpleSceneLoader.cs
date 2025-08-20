// // SimpleSceneLoader.cs
// using System.Collections;
// using UnityEngine;
// using UnityEngine.SceneManagement;

// public class SimpleSceneLoader : MonoBehaviour
// {
//     [Header("End scene")]
//     [SerializeField] private string endSceneName = "theend";
//     [Min(0f)] [SerializeField] private float loadDelay = 0f;

//     [Header("Last level detection")]
//     [Tooltip("Авто: lastIndex = LevelSwitcher.LevelCount - 1. Если авто не удалось — используем ручной.")]
//     [SerializeField] private bool autoDetectLastIndex = true;
//     [Tooltip("0-based. Используется, если автоопределение недоступно.")]
//     [SerializeField] private int lastLevelIndex = 6;

//     [Header("Flow (optional)")]
//     [Tooltip("Если НЕ финальный уровень — перейти к следующему (когда advanceLevelWhenDone отключён).")]
//     [SerializeField] private bool alsoAdvanceIfNotLast = false;

//     [Header("Debug")]
//     [SerializeField] private bool verboseLog = true;

//     private bool _fired;

//     /// <summary>
//     /// Привяжи к VictorySequenceController.OnSequenceCompleted
//     /// </summary>
//     public void OnVictorySequenceCompleted()
//     {
//     //     if (_fired) { Log("Skip: already fired"); return; }

//     //     var sw = FindAnyObjectByType<LevelSwitcher>();
//     //     int idx = (sw != null) ? sw.CurrentIndex : -1;
//     //     int levelCount = (sw != null) ? sw.LevelCount : -1;

//     //     // Определяем lastIdx устойчиво
//     //     int lastIdx;
//     //     if (autoDetectLastIndex && sw != null && levelCount > 0)
//     //     {
//     //         lastIdx = levelCount - 1;
//     //     }
//     //     else
//     //     {
//     //         // Фолбэк на ручное значение
//     //         lastIdx = lastLevelIndex;
//     //         if (autoDetectLastIndex && sw != null && levelCount <= 0)
//     //             Log($"Auto-detect failed (LevelCount={levelCount}). Fallback to serialized lastLevelIndex={lastIdx}");
//     //         if (sw == null)
//     //             Log("LevelSwitcher not found. Fallback to serialized lastLevelIndex.");
//     //     }

//     //     Log($"idx={idx}, lastIdx={lastIdx}, levelCount={levelCount}, auto={autoDetectLastIndex}");

//     //     if (idx >= 0 && idx >= lastIdx)
//     //     {
//     //         _fired = true;
//     //         if (loadDelay > 0f) StartCoroutine(LoadAfterDelay());
//     //         else SceneManager.LoadScene(endSceneName);
//     //         return;
//     //     }

//     //     if (alsoAdvanceIfNotLast && sw != null)
//     //     {
//     //         Log("Not last: advancing to next level via LevelSwitcher.Next()");
//     //         sw.Next();
//     //     }
//     // }

//     // public void ForceLoadEnd()
//     // {
//     //     _fired = true;
//     //     SceneManager.LoadScene(endSceneName);
//     // }

//     // private IEnumerator LoadAfterDelay()
//     // {
//     //     yield return new WaitForSecondsRealtime(loadDelay);
//     //     SceneManager.LoadScene(endSceneName);
//     // }

//     // private void Log(string msg)
//     // {
//     //     if (verboseLog) Debug.Log($"[SimpleSceneLoader] {msg}");
//     // }
// }
