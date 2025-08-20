using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class LevelAudioCueSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class LevelCue
    {
        [Tooltip("ID уровня (обычно индекс LevelSwitcher)")]
        public int levelId;

        [Header("Sound")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 1f)] public float pitchJitter = 0f;

        [Header("Options")]
        public bool usePlayClipAtPoint = false; // если нужно 3D в мире
        public Transform worldAnchor;           // позиция для 3D-воспроизведения (необязательно)
        public float spatialVolume = 1f;        // громкость для PlayClipAtPoint
    }

    [Header("Default 2D AudioSource (для OneShot)")]
    public AudioSource source;                 // можно пустым — создадим автоматически
    public AudioMixerGroup output;             // опционально

    [Header("Global options")]
    public bool stopSourceBeforePlay = false;  // остановить текущий звук у source перед OneShot

    // runtime
    readonly Dictionary<int, LevelCue> _byId = new();
    int _currentLevelId = -1;

    void Awake()
    {
        // Сбор индекса
        RebuildIndex();

        // Автосоздание 2D-источника
        if (!source)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            if (output) source.outputAudioMixerGroup = output;
        }
    }

    void RebuildIndex()
    {
        _byId.Clear();
        var cues = GetComponentsInChildren<LevelAudioCueMarker>(true); // опциональные маркеры в детях
        // индекс по сериализованному массиву с инспектора
        foreach (var c in levelCues)
            if (c != null) _byId[c.levelId] = c;

        // индекс по маркерам (если используете их)
        foreach (var m in cues)
            if (m && m.config != null) _byId[m.config.levelId] = m.config;
    }

    // --- Public API ---

    [Header("Cues (per level)")]
    public LevelCue[] levelCues; // основной способ задать клипы

    /// Сообщить актуальный ID уровня
    public void SetCurrentLevelIndex(int levelId) => _currentLevelId = levelId;

    /// Проиграть звук для текущего уровня
    public void PlayForCurrentLevel()
    {
        if (_currentLevelId < 0)
        {
            var sw = FindFirstObjectByType<LevelSwitcher>();
            if (sw) _currentLevelId = sw.CurrentIndex;
        }
        PlayForLevel(_currentLevelId);
    }

    /// Проиграть звук для конкретного уровня
    public void PlayForLevel(int levelId)
    {
        if (!_byId.TryGetValue(levelId, out var cue) || cue == null || cue.clip == null) return;

        if (cue.usePlayClipAtPoint)
        {
            // 3D-воспроизведение в мире (можно без anchor — в (0,0,0))
            Vector3 pos = cue.worldAnchor ? cue.worldAnchor.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(cue.clip, pos, Mathf.Clamp01(cue.spatialVolume));
            return;
        }

        // 2D OneShot на общем источнике
        if (stopSourceBeforePlay) source.Stop();
        float p = cue.pitch + Random.Range(-cue.pitchJitter, cue.pitchJitter);
        p = Mathf.Clamp(p, 0.1f, 3f);
        float vol = Mathf.Clamp01(cue.volume);

        float oldPitch = source.pitch;
        try
        {
            source.pitch = p;
            source.PlayOneShot(cue.clip, vol);
        }
        finally
        {
            source.pitch = oldPitch; // вернуть базовый питч
        }
    }
}

/// (необязательный) маркер, чтобы положить LevelCue на отдельный GO в сцене
public class LevelAudioCueMarker : MonoBehaviour
{
    public LevelAudioCueSwitcher.LevelCue config;
}
