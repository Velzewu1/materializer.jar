using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictorySequenceController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;          // слушаем OnWinEvent

    [Header("Roots")]
    public GameObject  puzzleRoot;               // корень UI пазла (LevelRoot)
    public CanvasGroup screenCanvas;             // основной Canvas (ScreenCanvas)
    public CanvasGroup victoryCanvas;            // Canvas победной сценки (обычно скрыт)
    public bool deactivateVictoryCanvasGO = true;

    [Header("Item (первая фаза)")]
    public Image     itemImage;                       // картинка предмета из LevelData
    [Min(0.1f)] public float itemHoldTime = 1.5f;    // сколько держать предмет до начала фейда
    [Min(0.05f)] public float itemFadeTime = 0.6f;   // длительность фейд-аута предмета

    [Header("Mascot (идёт ПАРАЛЛЕЛЬНО с Item)")]
    public Image     mascotImage;                     // маскот (стат/спрайтшит)
    public Sprite[]  mascotFrames;                    // кадры спрайтшита (если заданы)
    public float     mascotFps = 12f;                 // FPS анимации маскота
    [Min(0f)] public float mascotStartDelay = 0.0f;   // задержка перед проявлением маскота
    [Min(0.05f)] public float mascotFadeIn = 0.5f;    // мягкое появление маскота

    [Header("Texts")]
    public TMP_Text  materializationText;             // “Materialisation…”
    public TMP_Text  completedText;                   // “MATERIALISATION COMPLETED”
    public bool      showCompletedAtEnd = true;
    public string    completedPhrase = "MATERIALISATION COMPLETED";
    [Min(0.1f)] public float completedHold = 1.2f;
    [Min(0.05f)] public float completedFadeOut = 0.45f; // плавный fade-out после удержания

    [Header("Timing (общая длительность)")]
    [Min(0.5f)] public float sequenceDuration = 10f;  // вся сценка (таймер для шумов/шейка)

    [Header("CRT Noise (опция)")]
    public Material  crtMaterial;                     // CRT_Fullscreen_Mat (ваш шейдер)
    public string    noisePropName = "_NoiseAmount";
    public float     noiseMax = 0.35f;
    public float     noiseRampStart = 8.5f;           // с какой секунды наращивать шум
    public AnimationCurve noiseCurve = AnimationCurve.Linear(0,0, 1,1);

    [Header("World screen shake (финальные секунды)")]
    public Transform mainCamera;                      // Transform мировой/экранной камеры
    public bool      enableWorldShake = true;
    public float     shakeLastSeconds = 2.0f;
    public float     shakeIntensity   = 1.0f;
    public float     shakePosAmplitude = 0.08f;
    public float     shakeRotAmplitude = 0.8f;
    public float     shakeFrequency    = 14f;
    public AnimationCurve shakeCurve   = AnimationCurve.EaseInOut(0,0, 1,1);

    [Header("Item FX (optional)")]
    public bool   enableItemFX = false;          // вкл/выкл «дыхание»
    public float  itemPulseFrequency = 1.5f;     // Гц
    public float  itemPulseScaleAmp = 0.06f;     // амплитуда масштаба (0.06 = ±6%)
    public bool   itemPulseTint = false;         // пульсировать цветом?
    public Color  itemPulseColor = Color.white;  // целевой цвет пульса
    [Range(0f,1f)]
    public float  itemPulseTintAmount = 0.2f;    // насколько сильный тинт

    [Header("Flow")]
    public bool      advanceLevelWhenDone = true;

    // runtime
    Coroutine _seq;
    Coroutine _dotsCo;
    Coroutine _mascotAnimCo;
    float     _noiseStart;
    Vector3   _camBasePos;
    Quaternion _camBaseRot;

    // Базы для отката FX
    Vector3   _itemBaseScale = Vector3.one;
    Color     _itemBaseColor = Color.white;

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
        // подготовка
        if (!mainCamera && Camera.main) mainCamera = Camera.main.transform;
        if (mainCamera) { _camBasePos = mainCamera.localPosition; _camBaseRot = mainCamera.localRotation; }

        if (screenCanvas) ToggleCanvas(screenCanvas, false);
        if (controller && controller.ui) controller.ui.gameObject.SetActive(false);
        if (puzzleRoot) puzzleRoot.SetActive(false);

        ToggleCanvas(victoryCanvas, true, deactivateVictoryCanvasGO);

        // подтянуть предмет из LevelData
        var ld = controller ? controller.level : null;
        if (itemImage)
        {
            if (ld && ld.itemSprite)
            {
                itemImage.sprite = ld.itemSprite;
                itemImage.enabled = true;
                itemImage.type = Image.Type.Simple;
                itemImage.preserveAspect = true; // вписываем в контейнер Image
            }
            else
            {
                itemImage.enabled = false;
            }
            SetAlpha(itemImage, itemImage.enabled ? 1f : 0f);

            // базы FX
            if (itemImage)
            {
                _itemBaseScale = itemImage.rectTransform.localScale;
                _itemBaseColor = itemImage.color;
            }
        }

        // маскот готовим к параллельному показу
        if (mascotImage)
        {
            SetAlpha(mascotImage, 0f);
            mascotImage.enabled = true; // проявим после задержки
            // спрайтшит можно крутить сразу — альфа пока 0
            if (_mascotAnimCo != null) StopCoroutine(_mascotAnimCo);
            if (mascotFrames != null && mascotFrames.Length > 0 && mascotFps > 0f)
                _mascotAnimCo = StartCoroutine(AnimateMascot());
        }

        // "Materialisation..." с бегущими точками — идёт до Completed
        if (materializationText)
        {
            materializationText.gameObject.SetActive(true);
            if (_dotsCo != null) StopCoroutine(_dotsCo);
            _dotsCo = StartCoroutine(Dots(materializationText, "Materialisation"));
        }

        // Параллельная петля
        float t = 0f;

        // состояния параллельных фейдов
        bool  itemFading = false; float itemFadeElapsed = 0f; float itemStartAlpha = itemImage ? itemImage.color.a : 0f;
        bool  mascotFadingIn = false; float mascotFadeElapsed = 0f; float mascotStartAlpha = mascotImage ? mascotImage.color.a : 0f;

        while (t < sequenceDuration)
        {
            // 1) Запуск фейда предмета по таймеру удержания
            if (!itemFading && itemImage && itemImage.enabled && t >= itemHoldTime)
            {
                itemFading = true;
                itemFadeElapsed = 0f;
                itemStartAlpha = itemImage.color.a;
            }
            if (itemFading && itemImage && itemImage.enabled)
            {
                float p = Mathf.Clamp01(itemFadeElapsed / Mathf.Max(0.001f, itemFadeTime));
                SetAlpha(itemImage, Mathf.Lerp(itemStartAlpha, 0f, p));
                itemFadeElapsed += Time.unscaledDeltaTime;
            }

            // 2) Запуск проявления маскота после задержки
            if (!mascotFadingIn && mascotImage && t >= mascotStartDelay)
            {
                mascotFadingIn = true;
                mascotFadeElapsed = 0f;
                mascotStartAlpha = mascotImage.color.a; // обычно 0
            }
            if (mascotFadingIn && mascotImage)
            {
                float p = Mathf.Clamp01(mascotFadeElapsed / Mathf.Max(0.001f, mascotFadeIn));
                SetAlpha(mascotImage, Mathf.Lerp(mascotStartAlpha, 1f, p));
                mascotFadeElapsed += Time.unscaledDeltaTime;
            }

            // 3) CRT noise & shake (как раньше)
            UpdateCrtNoise(t);
            ApplyWorldShake(t);

            // 4) Item FX (опционально)
            ApplyItemFX(t);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // -------- Итог: скрыть "Materialisation...", скрыть маскота, показать Completed, подержать и погасить --------
        if (showCompletedAtEnd && completedText)
        {
            // выключаем бегущие точки и текст загрузки
            if (_dotsCo != null) { StopCoroutine(_dotsCo); _dotsCo = null; }
            if (materializationText) materializationText.gameObject.SetActive(false);

            // скрываем маскота в момент появления Completed
            if (mascotImage)
            {
                SetAlpha(mascotImage, 0f);
                mascotImage.enabled = false;
            }

            // показать COMPLETED (fade-in), удержать, затем fade-out
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

            // удержание
            float hold = 0f;
            while (hold < completedHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            // fade-out
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

        // -------- Сброс и переход --------
        if (_mascotAnimCo != null) { StopCoroutine(_mascotAnimCo); _mascotAnimCo = null; }

        // откат FX предмета
        ResetItemFX();

        ResetWorldShake();
        ResetCrtNoise();

        ToggleCanvas(victoryCanvas, false, deactivateVictoryCanvasGO);
        if (screenCanvas) ToggleCanvas(screenCanvas, true);
        if (controller && controller.ui) controller.ui.gameObject.SetActive(true);
        if (puzzleRoot) puzzleRoot.SetActive(true);

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

    void ApplyWorldShake(float sceneTime)
    {
        if (!enableWorldShake || !mainCamera || shakeLastSeconds <= 0f || shakeIntensity <= 0f) return;
        float t0 = Mathf.Max(0f, sequenceDuration - shakeLastSeconds);
        if (sceneTime < t0) return;

        float nt = Mathf.InverseLerp(t0, sequenceDuration, sceneTime);
        float k  = shakeCurve.Evaluate(nt) * shakeIntensity;

        float n  = sceneTime * shakeFrequency;
        float ox = (Mathf.PerlinNoise(n,  3.1f) - 0.5f) * 2f * (shakePosAmplitude * k);
        float oy = (Mathf.PerlinNoise(n, 17.9f) - 0.5f) * 2f * (shakePosAmplitude * k);
        float rz = (Mathf.PerlinNoise(n, 42.0f) - 0.5f) * 2f * (shakeRotAmplitude * k);

        mainCamera.localPosition = _camBasePos + new Vector3(ox, oy, 0f);
        mainCamera.localRotation = Quaternion.Euler(0f, 0f, rz) * _camBaseRot;
    }

    // ---- Item FX ----
    void ApplyItemFX(float sceneTime)
    {
        if (!enableItemFX || itemImage == null || !itemImage.enabled) return;

        // scale "breathing"
        float s = 1f + itemPulseScaleAmp * Mathf.Sin(sceneTime * Mathf.PI * 2f * Mathf.Max(0.01f, itemPulseFrequency));
        itemImage.rectTransform.localScale = _itemBaseScale * s;

        // optional tint pulse
        if (itemPulseTint)
        {
            float k = (Mathf.Sin(sceneTime * Mathf.PI * 2f * Mathf.Max(0.01f, itemPulseFrequency)) * 0.5f + 0.5f) * itemPulseTintAmount;
            var c = Color.Lerp(_itemBaseColor, itemPulseColor, k);
            c.a = itemImage.color.a; // уважаем текущую альфу (фейды)
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
        if (!mainCamera) return;
        mainCamera.localPosition = _camBasePos;
        mainCamera.localRotation = _camBaseRot;
    }
}
