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
            _sfx.ignoreListenerPause = true;     // на всякий случай
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
        StopLoop(true);
        StopFade();
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
        StopLoop(true);
        if (_currentClip)
            _loopCo = StartCoroutine(MusicLoop());
    }

    IEnumerator MusicLoop()
    {
        while (_currentClip)
        {
            musicSource.clip   = _currentClip;
            musicSource.volume = 0f;
            musicSource.Play();

            // fade-in
            float t = 0f;
            while (t < musicFadeIn && musicSource.isPlaying)
            {
                musicSource.volume = Mathf.Lerp(0f, _currentVol, musicFadeIn <= 0f ? 1f : t / musicFadeIn);
                t += Time.deltaTime;
                yield return null;
            }
            musicSource.volume = _currentVol;

            // ждём, пока трек закончится
            yield return new WaitWhile(() => musicSource.isPlaying);

            // случайная пауза
            float gap = Random.Range(gapSeconds.x, gapSeconds.y);
            if (gap > 0f) yield return new WaitForSeconds(gap);
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
        StopLoop(false);    // не стопаем сразу аудио, только петлю
        StopFade();
        _fadeCo = StartCoroutine(FadeOutAndOneShot(fadeTime, sfx));
    }

    IEnumerator FadeOutAndOneShot(float fadeTime, AudioClip sfx)
    {
        float startVol = musicSource.volume;
        float t = 0f;

        if (musicSource.isPlaying && fadeTime > 0f)
        {
            while (t < fadeTime)
            {
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
                t += Time.deltaTime;
                yield return null;
            }
        }
        musicSource.volume = 0f;
        musicSource.Stop();

        if (sfx && _sfx)
        {
            _sfx.volume = sfxVolume;
            _sfx.PlayOneShot(sfx);
            Debug.Log("[ScreenSound] SFX started: " + sfx.name);
        }

        _fadeCo = null;
    }

    void StopLoop(bool stopAudio)
    {
        if (_loopCo != null) { StopCoroutine(_loopCo); _loopCo = null; }
        if (stopAudio) musicSource.Stop();
    }
    void StopFade()
    {
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
    }
}
