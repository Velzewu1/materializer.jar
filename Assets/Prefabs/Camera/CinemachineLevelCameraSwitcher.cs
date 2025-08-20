using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class CinemachineLevelCameraSwitcher : MonoBehaviour
{
    // ---------- Data ----------
    [System.Serializable]
    public class LevelBinding
    {
        [Tooltip("ID of level (usually LevelSwitcher index)")]
        public int levelId;

        [Header("Scene Refs")]
        public CinemachineCamera levelVcam;   // CM 3.x camera to focus on
        public GameObject       focusItem;    // object that “materializes”

        // Runtime cache
        [HideInInspector] public Vector3   originalScale = Vector3.one;
        [HideInInspector] public Renderer[] renderers;
        [HideInInspector] public SpriteRenderer[] spriteRenderers;
        [HideInInspector] public Color[]   emissionBackup; // all materials, flattened
        [HideInInspector] public Color[]   colorBackup;    // all materials, flattened
        [HideInInspector] public Color[]   spriteColorBackup;
    }

    // ---------- Inspector ----------
    [Header("Default / Screen VCam")]
    [Tooltip("Gameplay/default vcam to return to")]
    public CinemachineCamera defaultVcam;

    [Header("Bindings (per level)")]
    public LevelBinding[] bindings;

    [Header("Priorities")]
    public int defaultPriority = 10;   // gameplay/base
    public int levelPriority   = 100;  // focused

    [Header("Reveal options")]
    [Tooltip("Hide item before materialization begins")]
    public bool hideFocusBeforeMaterialize = true;
    [Tooltip("Show item when switching to level vcam")]
    public bool revealOnCameraFocusBegin = true;

    [Header("Pop effect")]
    [Min(0f)]   public float popDuration = 0.35f;
    [Range(0.01f, 1.0f)] public float popFrom = 0.60f; // start from 60% of base scale (never 0!)
    [Min(1.0f)] public float popOvershoot = 1.15f;     // overshoot peak
    public AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Green glow (no particles)")]
    public bool  enableGlow        = true;
    [Min(0f)]    public float glowDuration = 0.9f;
    public AnimationCurve glowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public Color glowColor = new Color(0.45f, 1f, 0.45f, 1f);
    [Tooltip("How strong emission gets at peak (HDR friendly).")]
    [Min(0f)]    public float emissionStrength = 2.5f;
    [Tooltip("Keep a small residual emission after fade out.")]
    public bool  keepSmallResidualGlow = true;
    [Range(0f, 1f)] public float residualGlowFactor = 0.2f;

    [Header("Optional halo light")]
    public bool   addPointLight = false;
    [Min(0f)]     public float pointLightIntensity = 2.5f;
    [Min(0.1f)]   public float pointLightRange     = 3.5f;
    [Min(0f)]     public float pointLightFadeOut   = 0.6f;

    // ---------- Runtime ----------
    readonly Dictionary<int, LevelBinding> _byId = new();
    int          _currentLevelId = -1;
    LevelBinding _current;
    Coroutine    _popCo, _glowCo;

    void Awake()
    {
        RebuildIndex();

        // Cache per-binding data and optionally hide focus items
        if (bindings != null)
        {
            foreach (var b in bindings)
            {
                if (b == null || !b.focusItem) continue;

                // safe base scale (avoid zero-scale trap)
                b.originalScale = SafeBaseScale(b.focusItem.transform.localScale);

                // cache renderers
                b.renderers       = b.focusItem.GetComponentsInChildren<Renderer>(true);
                b.spriteRenderers = b.focusItem.GetComponentsInChildren<SpriteRenderer>(true);

                // backup material colors & emission
                BackupMaterials(b);
                BackupSprites(b);

                if (hideFocusBeforeMaterialize)
                    b.focusItem.SetActive(false);
            }
        }

        if (!defaultVcam)
            defaultVcam = GameObject.FindAnyObjectByType<CinemachineCamera>();

        // Default cam on top initially
        SetPrioritySafe(defaultVcam, levelPriority);
    }

    // ---------- Public API ----------
    /// Tell the switcher which level is active (call from LevelSwitcher when index changes).
    public void SetCurrentLevelIndex(int levelId)
    {
        _currentLevelId = levelId;
        _current = Get(levelId);
    }

    public void HideFocus()
    {
        EnsureCurrent();
        if (hideFocusBeforeMaterialize && _current != null && _current.focusItem)
            _current.focusItem.SetActive(false);
    }

    /// Use current level id (or auto-grab from LevelSwitcher) and reveal.
    public void SwitchToLevelAndReveal()
    {
        EnsureCurrent();
        DoSwitchAndReveal();
    }

    /// Explicit id overload.
    public void SwitchToLevelAndReveal(int levelId)
    {
        SetCurrentLevelIndex(levelId);
        SwitchToLevelAndReveal();
    }

    /// Put gameplay/default camera back on top (call before changing level).
    public void SwitchToDefault()
    {
        SetPrioritySafe(_current?.levelVcam, defaultPriority);
        SetPrioritySafe(defaultVcam,          levelPriority);
    }

    // ---------- Internal ----------
    void DoSwitchAndReveal()
    {
        if (_current == null)
        {
            Debug.LogWarning($"[CinemachineLevelCameraSwitcher] No binding for levelId={_currentLevelId}");
            return;
        }

        // switch priorities
        SetPrioritySafe(defaultVcam,         defaultPriority);
        SetPrioritySafe(_current.levelVcam,  levelPriority);

        // force item visible & restore base scale immediately
        var go = _current.focusItem;
        if (go)
        {
            if (!go.activeSelf) go.SetActive(true);
            go.transform.localScale = SafeBaseScale(_current.originalScale);

            // pop
            if (_popCo != null) StopCoroutine(_popCo);
            _popCo = StartCoroutine(PopFocus(go.transform, _current.originalScale, popFrom, popOvershoot, popDuration, popCurve));

            // glow
            if (enableGlow)
            {
                if (_glowCo != null) StopCoroutine(_glowCo);
                _glowCo = StartCoroutine(GlowFocus(_current));
            }
        }
        else
        {
            Debug.LogWarning("[CinemachineLevelCameraSwitcher] Focus item is missing for current level.");
        }
    }

    void RebuildIndex()
    {
        _byId.Clear();
        if (bindings == null) return;
        foreach (var b in bindings)
        {
            if (b == null) continue;
            _byId[b.levelId] = b;
        }
    }

    LevelBinding Get(int id)
    {
        if (_byId.Count == 0) RebuildIndex();
        _byId.TryGetValue(id, out var b);
        return b;
    }

    void EnsureCurrent()
    {
        if (_current != null) return;

        if (_currentLevelId < 0)
        {
            // fallback: try LevelSwitcher if user forgot to call SetCurrentLevelIndex
            var sw = GameObject.FindAnyObjectByType<LevelSwitcher>();
            if (sw) _currentLevelId = sw.CurrentIndex;
        }

        _current = Get(_currentLevelId);
    }

    static void SetPrioritySafe(CinemachineCamera cam, int p)
    {
        if (cam) cam.Priority = p;
    }

    static Vector3 SafeBaseScale(Vector3 s)
    {
        if (Mathf.Approximately(s.x, 0f) &&
            Mathf.Approximately(s.y, 0f) &&
            Mathf.Approximately(s.z, 0f))
            return Vector3.one; // fallback
        return s;
    }

    // ---------- POP ----------
    static IEnumerator PopFocus(Transform tr, Vector3 baseScale, float from, float overMul, float duration, AnimationCurve curve)
    {
        if (!tr) yield break;

        Vector3 start = baseScale * Mathf.Clamp(from, 0.01f, 1f);
        Vector3 over  = baseScale * Mathf.Max(1f, overMul);

        float up = duration * 0.55f;
        float t  = 0f;

        // up to overshoot
        while (t < up)
        {
            float k = up <= 0f ? 1f : curve.Evaluate(t / Mathf.Max(0.0001f, up));
            tr.localScale = Vector3.LerpUnclamped(start, over, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        tr.localScale = over;

        // back to base
        float down = Mathf.Max(0f, duration - up);
        t = 0f;
        while (t < down)
        {
            float k = down <= 0f ? 1f : curve.Evaluate(t / Mathf.Max(0.0001f, down));
            tr.localScale = Vector3.LerpUnclamped(over, baseScale, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        tr.localScale = baseScale;
    }

    // ---------- GLOW ----------
    void BackupMaterials(LevelBinding b)
    {
        if (b.renderers == null) return;

        var emiss = new List<Color>();
        var cols  = new List<Color>();

        foreach (var r in b.renderers)
        {
            if (!r) continue;
            var mats = r.materials; // instance
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) { emiss.Add(Color.black); cols.Add(Color.white); continue; }

                Color c = Color.white;
                if (m.HasProperty("_Color")) c = m.GetColor("_Color");
                cols.Add(c);

                Color e = Color.black;
                if (m.HasProperty("_EmissionColor"))
                {
                    e = m.GetColor("_EmissionColor");
                    m.EnableKeyword("_EMISSION");
                }
                emiss.Add(e);
            }
        }

        b.emissionBackup = emiss.ToArray();
        b.colorBackup     = cols.ToArray();
    }

    void BackupSprites(LevelBinding b)
    {
        if (b.spriteRenderers == null) return;
        var list = new List<Color>(b.spriteRenderers.Length);
        foreach (var sr in b.spriteRenderers)
            list.Add(sr ? sr.color : Color.white);
        b.spriteColorBackup = list.ToArray();
    }

    IEnumerator GlowFocus(LevelBinding b)
    {
        if (b == null || b.renderers == null) yield break;

        // create optional halo light
        Light halo = null;
        if (addPointLight)
        {
            var go = new GameObject("RevealGlowLight");
            go.transform.SetParent(b.focusItem.transform, false);
            go.transform.localPosition = Vector3.zero;
            halo = go.AddComponent<Light>();
            halo.type      = LightType.Point;
            halo.color     = glowColor;
            halo.range     = pointLightRange;
            halo.intensity = 0f;
        }

        float t = 0f;
        while (t < glowDuration)
        {
            float k = glowDuration <= 0f ? 1f : glowCurve.Evaluate(t / glowDuration);

            // tint sprites gently
            if (b.spriteRenderers != null && b.spriteColorBackup != null)
            {
                for (int i = 0; i < b.spriteRenderers.Length && i < b.spriteColorBackup.Length; i++)
                {
                    var sr = b.spriteRenderers[i];
                    if (!sr) continue;
                    var baseCol = b.spriteColorBackup[i];
                    var tinted  = baseCol + glowColor * 0.35f * k;
                    tinted.a    = baseCol.a;
                    sr.color    = Clamp01(tinted);
                }
            }

            // materials emission / color
            int idx = 0;
            foreach (var r in b.renderers)
            {
                if (!r) continue;
                var mats = r.materials;
                for (int m = 0; m < mats.Length; m++, idx++)
                {
                    var mat = mats[m];
                    if (!mat) continue;

                    // Emission
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        var baseE = (b.emissionBackup != null && idx < b.emissionBackup.Length)
                            ? b.emissionBackup[idx]
                            : Color.black;

                        var emiss = baseE + glowColor * (emissionStrength * k);
                        mat.SetColor("_EmissionColor", emiss);
                    }

                    // Base color (gently)
                    if (mat.HasProperty("_Color"))
                    {
                        var baseC = (b.colorBackup != null && idx < b.colorBackup.Length)
                            ? b.colorBackup[idx]
                            : mat.GetColor("_Color");

                        var tinted = baseC + glowColor * 0.25f * k;
                        mat.SetColor("_Color", Clamp01(tinted));
                    }
                }
            }

            if (halo)
                halo.intensity = pointLightIntensity * k;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // fade out halo
        if (halo && pointLightFadeOut > 0f)
        {
            float ft = 0f;
            float startI = halo.intensity;
            while (ft < pointLightFadeOut && halo)
            {
                float k = 1f - (ft / pointLightFadeOut);
                halo.intensity = startI * k;
                ft += Time.unscaledDeltaTime;
                yield return null;
            }
            if (halo) Destroy(halo.gameObject);
        }

        // restore materials, leave residual emission if requested
        float keep = keepSmallResidualGlow ? residualGlowFactor : 0f;
        int idx2 = 0;
        foreach (var r in b.renderers)
        {
            if (!r) continue;
            var mats = r.materials;
            for (int m = 0; m < mats.Length; m++, idx2++)
            {
                var mat = mats[m];
                if (!mat) continue;

                if (mat.HasProperty("_EmissionColor"))
                {
                    var baseE = (b.emissionBackup != null && idx2 < b.emissionBackup.Length)
                        ? b.emissionBackup[idx2]
                        : Color.black;

                    var keepE = baseE + glowColor * (emissionStrength * keep);
                    mat.SetColor("_EmissionColor", keepE);
                }

                if (mat.HasProperty("_Color"))
                {
                    var baseC = (b.colorBackup != null && idx2 < b.colorBackup.Length)
                        ? b.colorBackup[idx2]
                        : mat.GetColor("_Color");
                    mat.SetColor("_Color", baseC);
                }
            }
        }

        // restore sprite colors
        if (b.spriteRenderers != null && b.spriteColorBackup != null)
        {
            for (int i = 0; i < b.spriteRenderers.Length && i < b.spriteColorBackup.Length; i++)
            {
                var sr = b.spriteRenderers[i];
                if (sr) sr.color = b.spriteColorBackup[i];
            }
        }
    }

    // ---------- Utils ----------
    static Color Clamp01(Color c)
    {
        c.r = Mathf.Clamp01(c.r);
        c.g = Mathf.Clamp01(c.g);
        c.b = Mathf.Clamp01(c.b);
        c.a = Mathf.Clamp01(c.a);
        return c;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Reveal (current id)")]
    void _DBG_Reveal() => SwitchToLevelAndReveal();

    [ContextMenu("Debug/To Default")]
    void _DBG_Default() => SwitchToDefault();
#endif
}
