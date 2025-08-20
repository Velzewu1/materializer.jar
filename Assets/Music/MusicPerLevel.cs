using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class ScreenSoundController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;            // можно не заполнять — найдём
    public AudioSource musicSource;                // источник для фоновой музыки (2D)

    [Header("Loop gaps (sec)")]
    public Vector2 gapSeconds = new Vector2(20f, 40f);
    [Min(0f)] public float musicFadeIn = 0.5f;
    [Min(0f)] public float musicFadeOutOnWin  = 0.6f;
    [Min(0f)] public float musicFadeOutOnLose = 0.6f;

    [Header("Win/Lose SFX")]
    public AudioClip winSfx;
    public AudioClip loseSfx;
    [Range(0f,1f)] public float sfxVolume = 1f;

    [Header("Detached SFX player")]
    public bool makeDetachedSfxPlayer = true;     // отдельный корневой GO для OneShot'ов
    public bool dontDestroySfxPlayer  = true;

    // runtime
    AudioSource _sfx;               // всегда 2D, без фейдов
    Coroutine   _loopCo, _fadeCo;
    LevelData   _lastLevel;
    AudioClip   _currentClip;
    float       _currentVol = 0.9f;
    bool        _sceneChangingOrQuitting = false;

    void Awake()
    {
        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake  = false;
        musicSource.loop         = false;
        musicSource.spatialBlend = 0f;   // 2D

        if (makeDetachedSfxPlayer)
        {
            var go = new GameObject("SFX_Player");
            _sfx = go.AddComponent<AudioSource>();
            _sfx.playOnAwake       = false;
            _sfx.loop              = false;
            _sfx.spatialBlend      = 0f;         // 2D
            _sfx.ignoreListenerPause = true;
            if (dontDestroySfxPlayer) DontDestroyOnLoad(go);
        }
        else
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.loop = false;
            _sfx.spatialBlend = 0f;
            _sfx.ignoreListenerPause = true;
        }

        // нормализуем диапазон пауз
        gapSeconds.x = Mathf.Max(0f, Mathf.Min(gapSeconds.x, gapSeconds.y));
        gapSeconds.y = Mathf.Max(gapSeconds.x, gapSeconds.y);
    }

    void OnEnable()
    {
        _sceneChangingOrQuitting = false;

        if (!controller) controller = FindAnyObjectByType<PuzzleController>();
        if (controller)
        {
            controller.OnWinEvent  += HandleWin;
            controller.OnLoseEvent += HandleLose;
        }

        // стартовый трек
        TryApplyLevel(controller ? controller.level : null, true);
    }

    void OnDisable()
    {
        if (controller)
        {
            controller.OnWinEvent  -= HandleWin;
            controller.OnLoseEvent -= HandleLose;
        }

        // гасим корутины, НО не трогаем Stop() на уничтоженном источнике
        StopFade();
        StopLoop(false); // <- ВАЖНО: не вызываем musicSource.Stop() при disable
    }

    void OnApplicationQuit()
    {
        _sceneChangingOrQuitting = true;
    }

    void Update()
    {
        // Подхватываем смену уровня автоматически
        if (controller && controller.level != _lastLevel)
            TryApplyLevel(controller.level, true);
    }

    void TryApplyLevel(LevelData ld, bool restartMusic)
    {
        _lastLevel   = ld;
        _currentClip = ld ? ld.musicClip : null;
        _currentVol  = ld ? Mathf.Clamp01(ld.musicVolume) : 0.9f;

        if (!restartMusic) return;

        StopFade();
        StopLoop(true); // безопасно: внутри проверим валидность
        if (_currentClip && IsAlive(musicSource))
            _loopCo = StartCoroutine(MusicLoop());
    }

    IEnumerator MusicLoop()
    {
        // петля до смены клипа или уничтожения источника
        while (_currentClip && IsAlive(musicSource))
        {
            musicSource.clip   = _currentClip;
            musicSource.volume = 0f;
            musicSource.Play();

            // fade-in
            float t = 0f;
            if (musicFadeIn <= 0f)
            {
                if (!IsAlive(musicSource)) yield break;
                musicSource.volume = _currentVol;
            }
            else
            {
                while (t < musicFadeIn && IsAlive(musicSource) && musicSource.isPlaying)
                {
                    musicSource.volume = Mathf.Lerp(0f, _currentVol, t / musicFadeIn);
                    t += Time.deltaTime;
                    yield return null;
                }
                if (!IsAlive(musicSource)) yield break;
                musicSource.volume = _currentVol;
            }

            // ждём, пока трек закончится
            if (!IsAlive(musicSource)) yield break;
            yield return new WaitWhile(() => IsAlive(musicSource) && musicSource.isPlaying);

            if (!IsAlive(musicSource)) break;

            // случайная пауза
            float gap = Random.Range(gapSeconds.x, gapSeconds.y);
            if (gap > 0f)
            {
                float g = 0f;
                while (g < gap)
                {
                    if (!IsAlive(musicSource)) yield break;
                    g += Time.deltaTime;
                    yield return null;
                }
            }
        }
        _loopCo = null;
    }

    void HandleWin()
    {
        Debug.Log("[ScreenSound] WIN received");
        FadeOutMusicAndPlay(_currentClip ? musicFadeOutOnWin : 0f, winSfx);
    }

    void HandleLose()
    {
        Debug.Log("[ScreenSound] LOSE received");
        FadeOutMusicAndPlay(_currentClip ? musicFadeOutOnLose : 0f, loseSfx);
    }

    void FadeOutMusicAndPlay(float fadeTime, AudioClip sfx)
    {
        StopLoop(false);    // не рвём проигрывание Stop(), только петлю
        StopFade();
        if (IsAlive(musicSource))
            _fadeCo = StartCoroutine(FadeOutAndOneShot(fadeTime, sfx));
    }

    IEnumerator FadeOutAndOneShot(float fadeTime, AudioClip sfx)
    {
        if (!IsAlive(musicSource))
        {
            PlayOneShotSafe(sfx);
            _fadeCo = null;
            yield break;
        }

        float startVol = musicSource.volume;
        float t = 0f;

        if (musicSource.isPlaying && fadeTime > 0f)
        {
            while (t < fadeTime && IsAlive(musicSource))
            {
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
                t += Time.deltaTime;
                yield return null;
            }
        }

        if (IsAlive(musicSource))
        {
            musicSource.volume = 0f;
            // Во время смены сцены Stop() может кидать MissingReference — проверяем ещё раз
            if (!_sceneChangingOrQuitting) musicSource.Stop();
        }

        PlayOneShotSafe(sfx);
        _fadeCo = null;
    }

    void PlayOneShotSafe(AudioClip sfx)
    {
        if (sfx && _sfx) _sfx.PlayOneShot(sfx, sfxVolume);
    }

    void StopLoop(bool stopAudio)
    {
        if (_loopCo != null) { StopCoroutine(_loopCo); _loopCo = null; }
        if (stopAudio && IsAlive(musicSource) && !_sceneChangingOrQuitting)
        {
            // Доп. защита: только если активен в иерархии
            if (musicSource.gameObject.activeInHierarchy)
                musicSource.Stop();
        }
    }

    void StopFade()
    {
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
    }

    // Безопасная проверка «жив ли» AudioSource (не уничтожен и не фейк-null)
    static bool IsAlive(Object obj) => obj != null; // Unity перегружает == для уничтоженных объектов
}
