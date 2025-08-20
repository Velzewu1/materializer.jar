using UnityEngine;

[CreateAssetMenu(menuName = "Fill-a-Pix/LevelData")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int size = 16;     // N x N
    public int[] target;               // 0/1, длина N*N
    public int[] locks;                // 0/1, длина N*N
    public int[] clues;                // -1..9, длина N*N (-1 => подсказка скрыта)

    // --- UI/Meta ---
    public Sprite itemSprite;
    public string itemLabel;

    // --- AUDIO ---
    [Header("Audio")]
    public AudioClip musicClip;
    [Range(0f,1f)] public float musicVolume = 0.8f;

    // --- CAMERA (Cinemachine) ---
    [Header("Camera (Cinemachine)")]
    public bool  cameraOverride = true;        // если false — уровень не трогает камеру
    [Tooltip("Для Framing Transposer: m_CameraDistance. Для Transposer: FollowOffset.z")]
    public float cameraDistance = 8f;

    [Tooltip("Если true — переключим камеру в Ortho и анимируем OrthoSize; иначе — FOV")]
    public bool  useOrthographic = false;
    [Min(0.01f)] public float orthoSize = 5f;  // целевой размер, если Orthographic

    [Range(1f,120f)] public float fieldOfView = 50f; // целевой FOV, если Perspective
    [Min(0f)] public float cameraBlendTime = 0.75f;  // длительность плавного перехода

    // --- Индексация ---
    public int Get(int[] a, int r, int c) => a[r * size + c];
    public void Set(int[] a, int r, int c, int v) { a[r * size + c] = v; }

    void OnValidate()
    {
        size = Mathf.Max(1, size);
        EnsureArrays();
        musicVolume = Mathf.Clamp01(musicVolume);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 120f);
        orthoSize   = Mathf.Max(0.01f, orthoSize);
        cameraBlendTime = Mathf.Max(0f, cameraBlendTime);
    }

    public void EnsureArrays()
    {
        int need = size * size;
        if (target == null || target.Length != need) target = new int[need];
        if (locks  == null || locks .Length != need) locks  = new int[need];
        if (clues  == null || clues .Length != need)
        {
            clues = new int[need];
            for (int i = 0; i < need; i++) clues[i] = -1;
        }
    }

    public bool IsValid()
    {
        int need = size * size;
        return target != null && locks != null && clues != null &&
               target.Length == need && locks.Length == need && clues.Length == need;
    }
}
