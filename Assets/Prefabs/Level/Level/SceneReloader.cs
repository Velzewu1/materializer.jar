using UnityEngine;
using UnityEngine.SceneManagement;

/// Reload only the current scene ("SampleScene"), with optional pending LevelSwitcher index
/// and an optional "double cold pass" to clear glitches.
public class SceneReloader : MonoBehaviour
{
    // Имя единственной игровой сцены
    public static string GameSceneName = "SampleScene";

    // Pending: какой уровень попросили открыть в LevelSwitcher после перезагрузки
    static int  _pendingIndex = -1;
    // Кол-во проходов перезагрузки текущей сцены (2 = "холодный" двойной рестарт)
    static int  _passesLeft   = 0;

    // >>> ВОССТАНОВЛЕНО: API, которое дергает LevelSwitcher <<<
    public static bool HasPending => _pendingIndex >= 0;
    public static int  ConsumePendingIndex() { int x = _pendingIndex; _pendingIndex = -1; return x; }

    /// Если нужно — можно переопределить имя сцены в рантайме
    public static void Configure(string gameScene)
    {
        if (!string.IsNullOrEmpty(gameScene)) GameSceneName = gameScene;
    }

    public static void ReloadNextFrom(int current, int count, bool loop, bool coldRestartTwice = false)
    {
        int next = (current + 1 < count) ? current + 1 : (loop ? 0 : Mathf.Max(0, count - 1));
        ReloadWithIndex(next, coldRestartTwice);
    }

    /// Перезагрузить сцену и после загрузки выставить нужный индекс в LevelSwitcher
    public static void ReloadWithIndex(int index, bool coldRestartTwice = false)
    {
        _pendingIndex = Mathf.Max(0, index);
        StartReload(coldRestartTwice);
    }

    /// Просто перезагрузить сцену (индекс не меняем)
    public static void ReloadActive(bool coldRestartTwice = false)
    {
        // _pendingIndex остаётся как есть (может быть -1)
        StartReload(coldRestartTwice);
    }

    /// Сохранить индекс, который применится при ближайшей перезагрузке
    public static void SaveIndex(int index) => _pendingIndex = Mathf.Max(0, index);

    static void StartReload(bool coldRestartTwice)
    {
        _passesLeft = coldRestartTwice ? 2 : 1;
        SafeLoad(GameSceneName, "reload");
    }

    static bool CanLoad(string sceneName)
        => !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

    static void SafeLoad(string sceneName, string purpose)
    {
        if (CanLoad(sceneName))
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        else
            Debug.LogError($"[SceneReloader] Can't load scene '{sceneName}' for {purpose}. " +
                           $"Add it to Build Settings.");
    }

    // Подписываемся один раз при старте приложения
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Если запрошен двойной «холодный» перезапуск — выполняем ещё один пасс
        if (_passesLeft > 1)
        {
            _passesLeft--;
            SafeLoad(GameSceneName, "second pass (cold restart)");
            return;
        }

        _passesLeft = 0;

        // Применяем pending-индекс к LevelSwitcher (если он есть в сцене)
        if (_pendingIndex >= 0)
        {
            var sw = GameObject.FindAnyObjectByType<LevelSwitcher>();
            if (sw != null && sw.LevelCount > 0)
            {
                int idx = Mathf.Clamp(_pendingIndex, 0, sw.LevelCount - 1);
                _pendingIndex = -1; // consume
                sw.LoadIndex(idx);
            }
            else
            {
                _pendingIndex = -1; // применять не к чему — очищаем
            }
        }
    }
}
