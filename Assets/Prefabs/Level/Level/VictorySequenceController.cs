using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

using Vcam = Unity.Cinemachine.CinemachineCamera; // CM 3.1.4

public class VictorySequenceController : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnSequenceCompleted; 
    [Header("Refs")]
    public PuzzleController controller;

    [Header("Roots")]
    public GameObject  puzzleRoot;
    public CanvasGroup screenCanvas;
    public CanvasGroup victoryCanvas;
    public bool deactivateVictoryCanvasGO = true;

    [Header("Item (phase 1)")]
    public Image itemImage;
    [Min(0.1f)] public float itemHoldTime = 1.5f;
    [Min(0.05f)] public float itemFadeTime = 0.6f;

    [Header("Mascot (parallel)")]
    public Image    mascotImage;
    public Sprite[] mascotFrames;
    public float    mascotFps = 12f;
    [Min(0f)]    public float mascotStartDelay = 0.0f;
    [Min(0.05f)] public float mascotFadeIn = 0.5f;

    [Header("Texts")]
    public TMP_Text materializationText;
    public TMP_Text completedText;
    public bool     showCompletedAtEnd = true;
    public string   completedPhrase = "MATERIALISATION COMPLETED";
    [Min(0.1f)]  public float completedHold = 1.2f;
    [Min(0.05f)] public float completedFadeOut = 0.45f;

    [Header("Timing")]
    [Min(0.5f)] public float sequenceDuration = 10f;

    [Header("CRT Noise (optional)")]
    public Material crtMaterial;
    public string   noisePropName = "_NoiseAmount";
    public float    noiseMax = 0.35f;
    public float    noiseRampStart = 8.5f;
    public AnimationCurve noiseCurve = AnimationCurve.Linear(0,0,1,1);

    [Header("Shake timing / curve")]
    public bool   enableWorldShake = true;      // используется как флаг в ApplyWorldShake
    public float  shakeLastSeconds = 2.0f;
    public float  shakeIntensity   = 1.0f;
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Item FX (optional)")]
    public bool  enableItemFX = false;
    public float itemPulseFrequency = 1.5f;
    public float itemPulseScaleAmp = 0.06f;
    public bool  itemPulseTint = false;
    public Color itemPulseColor = Color.white;
    [Range(0f,1f)] public float itemPulseTintAmount = 0.2f;

    [Header("Flow")]
    public bool advanceLevelWhenDone = true;

    // ---------- Cinemachine ----------
    [Header("Cinemachine Shake (Perlin)")]
    public Vcam vcam;
    public Unity.Cinemachine.CinemachineBasicMultiChannelPerlin perlin;
    [Tooltip("Брать базовые значения из Perlin на камере? Иначе — из полей ниже.")]
    public bool readBaseFromPerlin = true;

    [Tooltip("Idle-значения, если не читаем с камеры")]
    public float baseAmplitude = 1.2f; // по запросу
    public float baseFrequency = 0.3f; // по запросу

    [Tooltip("Пиковые значения в финале материализации")]
    public float peakAmplitude = 2.0f;
    public float peakFrequency = 1.6f;

    // runtime
    Coroutine _seq, _dotsCo, _mascotAnimCo;
    float _noiseStart;

    Vector3 _itemBaseScale = Vector3.one;
    Color   _itemBaseColor = Color.white;

    float _cmBaseAmp, _cmBaseFreq;

    void Awake()
    {
        ToggleCanvas(victoryCanvas, false, deactivateVictoryCanvasGO);
        if (completedText) completedText.gameObject.SetActive(false);
        if (itemImage)  SetAlpha(itemImage, 0f);
        if (mascotImage) SetAlpha(mascotImage, 0f);
    }

    void OnEnable()
    {
        if (!controller) controller = FindAnyObjectByType<PuzzleController>();
        if (controller) controller.OnWinEvent += OnWin;

        if (crtMaterial && crtMaterial.HasProperty(noisePropName))
            _noiseStart = crtMaterial.GetFloat(noisePropName);

        if (!vcam) vcam = FindAnyObjectByType<Vcam>();
        if (!perlin && vcam)
        {
            perlin = vcam.GetComponent<Unity.Cinemachine.CinemachineBasicMultiChannelPerlin>()
                  ?? vcam.GetComponentInChildren<Unity.Cinemachine.CinemachineBasicMultiChannelPerlin>(true);
        }

        if (perlin)
        {
            if (readBaseFromPerlin)
            {
                _cmBaseAmp  = perlin.AmplitudeGain;
                _cmBaseFreq = perlin.FrequencyGain;
            }
            else
            {
                _cmBaseAmp  = baseAmplitude;
                _cmBaseFreq = baseFrequency;
                perlin.AmplitudeGain = _cmBaseAmp;
                perlin.FrequencyGain = _cmBaseFreq;
            }
        }
    }

    void OnDisable()
    {
        if (controller) controller.OnWinEvent -= OnWin;
    }

    void OnValidate()
    {
        sequenceDuration  = Mathf.Max(0.5f, sequenceDuration);
        itemHoldTime      = Mathf.Clamp(itemHoldTime, 0.1f, sequenceDuration);
        itemFadeTime      = Mathf.Clamp(itemFadeTime, 0.05f, sequenceDuration);
        mascotStartDelay  = Mathf.Max(0f, mascotStartDelay);
        mascotFadeIn      = Mathf.Clamp(mascotFadeIn, 0.05f, sequenceDuration);
        completedHold     = Mathf.Max(0.1f, completedHold);
        completedFadeOut  = Mathf.Max(0.05f, completedFadeOut);
        shakeLastSeconds  = Mathf.Clamp(shakeLastSeconds, 0f, sequenceDuration);
        shakeIntensity    = Mathf.Max(0f, shakeIntensity);
    }

    // ============ ENTRY ============
    void OnWin()
    {
        if (_seq != null) StopCoroutine(_seq);
        _seq = StartCoroutine(PlaySequenceParallel());
    }

    IEnumerator PlaySequenceParallel()
    {
        if (screenCanvas) ToggleCanvas(screenCanvas, false);
        if (controller && controller.ui) controller.ui.gameObject.SetActive(false);
        if (puzzleRoot) puzzleRoot.SetActive(false);

        ToggleCanvas(victoryCanvas, true, deactivateVictoryCanvasGO);

        var ld = controller ? controller.level : null;
        if (itemImage)
        {
            if (ld && ld.itemSprite)
            {
                itemImage.sprite = ld.itemSprite;
                itemImage.enabled = true;
                itemImage.type = Image.Type.Simple;
                itemImage.preserveAspect = true;
            }
            else itemImage.enabled = false;

            SetAlpha(itemImage, itemImage.enabled ? 1f : 0f);
            _itemBaseScale = itemImage.rectTransform.localScale;
            _itemBaseColor = itemImage.color;
        }

        if (mascotImage)
        {
            SetAlpha(mascotImage, 0f);
            mascotImage.enabled = true;
            if (_mascotAnimCo != null) StopCoroutine(_mascotAnimCo);
            if (mascotFrames != null && mascotFrames.Length > 0 && mascotFps > 0f)
                _mascotAnimCo = StartCoroutine(AnimateMascot());
        }

        if (materializationText)
        {
            materializationText.gameObject.SetActive(true);
            if (_dotsCo != null) StopCoroutine(_dotsCo);
            _dotsCo = StartCoroutine(Dots(materializationText, "Materialisation"));
        }

        if (perlin)
        {
            perlin.AmplitudeGain = _cmBaseAmp;
            perlin.FrequencyGain = _cmBaseFreq;
        }

        float t = 0f;
        bool  itemFading = false; float itemFadeElapsed = 0f; float itemStartAlpha = itemImage ? itemImage.color.a : 0f;
        bool  mascotFadingIn = false; float mascotFadeElapsed = 0f; float mascotStartAlpha = mascotImage ? mascotImage.color.a : 0f;

        while (t < sequenceDuration)
        {
            if (!itemFading && itemImage && itemImage.enabled && t >= itemHoldTime)
            {
                itemFading = true; itemFadeElapsed = 0f; itemStartAlpha = itemImage.color.a;
            }
            if (itemFading && itemImage && itemImage.enabled)
            {
                float p = Mathf.Clamp01(itemFadeElapsed / Mathf.Max(0.001f, itemFadeTime));
                SetAlpha(itemImage, Mathf.Lerp(itemStartAlpha, 0f, p));
                itemFadeElapsed += Time.unscaledDeltaTime;
            }

            if (!mascotFadingIn && mascotImage && t >= mascotStartDelay)
            {
                mascotFadingIn = true; mascotFadeElapsed = 0f; mascotStartAlpha = mascotImage.color.a;
            }
            if (mascotFadingIn && mascotImage)
            {
                float p = Mathf.Clamp01(mascotFadeElapsed / Mathf.Max(0.001f, mascotFadeIn));
                SetAlpha(mascotImage, Mathf.Lerp(mascotStartAlpha, 1f, p));
                mascotFadeElapsed += Time.unscaledDeltaTime;
            }

            UpdateCrtNoise(t);
            ApplyWorldShake(t);     // управляет только Perlin
            ApplyItemFX(t);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        StopCinemachineShakeImmediate(); // сброс перед COMPLETED

        if (showCompletedAtEnd && completedText)
        {
            if (_dotsCo != null) { StopCoroutine(_dotsCo); _dotsCo = null; }
            if (materializationText) materializationText.gameObject.SetActive(false);

            if (mascotImage)
            {
                SetAlpha(mascotImage, 0f);
                mascotImage.enabled = false;
            }
            var cs = FindAnyObjectByType<CinemachineLevelCameraSwitcher>();
            cs?.SwitchToLevelAndReveal();

            // >>> Добавлено: реплика героя во время показа COMPLETED
            DialogueSystem.I?.SayHeroOnMaterialize();

            // just before DialogueSystem.I?.SayHeroOnMaterialize();
            var csc = FindAnyObjectByType<CinemachineLevelCameraSwitcher>();
            var sw = FindAnyObjectByType<LevelSwitcher>();
            if (csc)
            {
                if (sw) csc.SwitchToLevelAndReveal(sw.CurrentIndex);
                else    csc.SwitchToLevelAndReveal(); // fallback: uses previously set id
            }

            FindAnyObjectByType<LevelAudioCueSwitcher>()?.PlayForCurrentLevel();

            completedText.text = completedPhrase;
            completedText.gameObject.SetActive(true);
            completedText.alpha = 0f;

            const float fadeIn = 0.35f;
            float ti = 0f;
            while (ti < fadeIn)
            {
                completedText.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ti / fadeIn));
                ti += Time.unscaledDeltaTime;
                yield return null;
            }
            completedText.alpha = 1f;

            float hold = 0f;
            while (hold < completedHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            float to = 0f;
            float a0 = completedText.alpha;
            while (to < completedFadeOut)
            {
                float p = to / Mathf.Max(0.001f, completedFadeOut);
                completedText.alpha = Mathf.Lerp(a0, 0f, p);
                to += Time.unscaledDeltaTime;
                yield return null;
            }
            completedText.alpha = 0f;
            completedText.gameObject.SetActive(false);
        }

        if (_mascotAnimCo != null) { StopCoroutine(_mascotAnimCo); _mascotAnimCo = null; }

        ResetItemFX();
        ResetWorldShake();
        ResetCrtNoise();

        ToggleCanvas(victoryCanvas, false, deactivateVictoryCanvasGO);
        if (screenCanvas) ToggleCanvas(screenCanvas, true);
        if (controller && controller.ui) controller.ui.gameObject.SetActive(true);
        if (puzzleRoot) puzzleRoot.SetActive(true);

        // >>> Сигнал завершения победной последовательности (вешайте SimpleSceneLoader здесь)
        OnSequenceCompleted?.Invoke();

        if (advanceLevelWhenDone)
        {
            var sw = FindAnyObjectByType<LevelSwitcher>();
            if (sw) sw.Next();
        }

        _seq = null;
    }

    // ---------- Helpers ----------
    IEnumerator Dots(TMP_Text txt, string baseText)
    {
        int dots = 0;
        while (true)
        {
            dots = (dots + 1) % 4;
            txt.text = baseText + new string('.', dots);
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    IEnumerator AnimateMascot()
    {
        int i = 0;
        float dt = 1f / Mathf.Max(1f, mascotFps);
        while (true)
        {
            if (mascotFrames != null && mascotFrames.Length > 0 && mascotImage)
                mascotImage.sprite = mascotFrames[i % mascotFrames.Length];
            i++;
            yield return new WaitForSecondsRealtime(dt);
        }
    }

    static void ToggleCanvas(CanvasGroup cg, bool on, bool deactivateGO = false)
    {
        if (!cg) return;
        cg.alpha = on ? 1f : 0f;
        cg.interactable = on;
        cg.blocksRaycasts = on;
        if (deactivateGO) cg.gameObject.SetActive(on);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;
    }

    void UpdateCrtNoise(float sceneTime)
    {
        if (!crtMaterial || !crtMaterial.HasProperty(noisePropName)) return;
        if (sceneTime < noiseRampStart) return;
        float p = Mathf.InverseLerp(noiseRampStart, sequenceDuration, sceneTime);
        float v = Mathf.Lerp(_noiseStart, noiseMax, noiseCurve.Evaluate(p));
        crtMaterial.SetFloat(noisePropName, v);
    }

    void ResetCrtNoise()
    {
        if (crtMaterial && crtMaterial.HasProperty(noisePropName))
            crtMaterial.SetFloat(noisePropName, _noiseStart);
    }

    // ---------- Perlin shake ----------
    void ApplyWorldShake(float sceneTime)
    {
        if (!enableWorldShake || shakeLastSeconds <= 0f || shakeIntensity <= 0f || perlin == null)
            return;

        float t0 = Mathf.Max(0f, sequenceDuration - shakeLastSeconds);
        if (sceneTime < t0) return;

        float k = shakeCurve.Evaluate(Mathf.InverseLerp(t0, sequenceDuration, sceneTime)) * shakeIntensity;

        perlin.AmplitudeGain = Mathf.Lerp(_cmBaseAmp,  peakAmplitude,  k);
        perlin.FrequencyGain = Mathf.Lerp(_cmBaseFreq, peakFrequency, k);
    }

    void StopCinemachineShakeImmediate()
    {
        if (perlin)
        {
            perlin.AmplitudeGain = _cmBaseAmp;
            perlin.FrequencyGain = _cmBaseFreq;
        }
    }

    // ---- Item FX ----
    void ApplyItemFX(float sceneTime)
    {
        if (!enableItemFX || itemImage == null || !itemImage.enabled) return;

        float s = 1f + itemPulseScaleAmp * Mathf.Sin(sceneTime * Mathf.PI * 2f * Mathf.Max(0.01f, itemPulseFrequency));
        itemImage.rectTransform.localScale = _itemBaseScale * s;

        if (itemPulseTint)
        {
            float k = (Mathf.Sin(sceneTime * Mathf.PI * 2f * Mathf.Max(0.01f, itemPulseFrequency)) * 0.5f + 0.5f) * itemPulseTintAmount;
            var c = Color.Lerp(_itemBaseColor, itemPulseColor, k);
            c.a = itemImage.color.a;
            itemImage.color = c;
        }
    }

    void ResetItemFX()
    {
        if (!itemImage) return;
        itemImage.rectTransform.localScale = _itemBaseScale;
        var c = _itemBaseColor; c.a = itemImage.color.a;
        itemImage.color = c;
    }

    void ResetWorldShake()
    {
        if (perlin)
        {
            perlin.AmplitudeGain = _cmBaseAmp;
            perlin.FrequencyGain = _cmBaseFreq;
        }
    }
}
