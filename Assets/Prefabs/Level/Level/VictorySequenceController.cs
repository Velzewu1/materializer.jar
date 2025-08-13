using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictorySequenceController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;          // подписка на OnWinEvent
    public ProgressController progress;          // не обязателен

    [Header("Roots")]
    public GameObject  puzzleRoot;               // корень UI пазла (например, LevelRoot)
    public CanvasGroup puzzleCanvasGroup;        // опционально (для плавного скрытия)
    public CanvasGroup screenCanvas;             // CanvasGroup твоего основного ScreenCanvas
    public CanvasGroup victoryCanvas;            // CanvasGroup VictoryCanvas (обычно выключен)
    public bool deactivateVictoryCanvasGO = true;// реально выключать GO, когда скрыт

    [Header("Victory UI (overlay)")]
    public Image   itemImage;                    // спрайт предмета из LevelData
    public TMP_Text itemLabel;                   // подпись предмета
    public TMP_Text materializationText;         // “Materialization…”

    [Header("Item spin")]
    public bool  spinItem = true;
    public float spinRpm  = 30f;

    [Header("Timing")]
    public float sequenceDuration = 10f;         // длительность сценки

    [Header("CRT Noise (optional)")]
    public Material crtMaterial;                 // твой CRT_Fullscreen_Mat
    public string   noisePropName = "_NoiseAmount";
    public float    noiseMax = 0.35f;
    public float    noiseRampStart = 8.5f;       // с какой секунды наращивать шум
    public AnimationCurve noiseCurve = AnimationCurve.Linear(0,0, 1,1);

    [Header("Screen Shake (optional)")]
    public Transform screenCamera;               // Transform твоей ScreenCamera
    public bool  shakeDuringSequence = true;
    public float shakePosAmplitude   = 0.06f;    // смещение (в единицах Transform’а)
    public float shakeRotAmplitude   = 0.6f;     // поворот в градусах
    public float shakeFrequency      = 14f;      // Гц
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0,1, 1,0); // затухание

    [Header("Flow")]
    public bool advanceLevelWhenDone = true;     // переключить уровень по завершении

    Coroutine _seq;
    float _noiseStartValue;

    void Awake()
    {
        // VictoryCanvas должен быть выключен "бОльшую часть времени"
        ToggleCanvas(victoryCanvas, false, deactivateVictoryCanvasGO);
    }

    void OnEnable()
    {
        if (!controller) controller = FindAnyObjectByType<PuzzleController>();
        if (controller) controller.OnWinEvent += HandleWin;

        if (crtMaterial && crtMaterial.HasProperty(noisePropName))
            _noiseStartValue = crtMaterial.GetFloat(noisePropName);
    }

    void OnDisable()
    {
        if (controller) controller.OnWinEvent -= HandleWin;
    }

    void HandleWin()
    {
        if (_seq != null) StopCoroutine(_seq);
        _seq = StartCoroutine(VictorySequence());
    }

    IEnumerator VictorySequence()
    {
        // 0) подготавливаем UI
        if (screenCanvas)         ToggleCanvas(screenCanvas, false);  // спрятать основной экран
        if (puzzleCanvasGroup)    ToggleCanvas(puzzleCanvasGroup, false);
        if (puzzleRoot)           puzzleRoot.SetActive(false);

        ToggleCanvas(victoryCanvas, true, deactivateVictoryCanvasGO); // показать Victory

        // забираем данные по предмету из текущего LevelData
        var ld = controller ? controller.level : null;
        if (itemImage)
        {
            if (ld && ld.itemSprite) { itemImage.sprite = ld.itemSprite; itemImage.enabled = true; }
            else                     { itemImage.enabled = false; }
            itemImage.SetNativeSize();
        }
        if (itemLabel)
            itemLabel.text = (ld && !string.IsNullOrEmpty(ld.itemLabel)) ? ld.itemLabel : string.Empty;

        if (materializationText) StartCoroutine(Dots(materializationText));

        // 1) основная петля сценки
        float t = 0f;
        float rpmToDeg = spinRpm * 360f / 60f;

        // shake — запоминаем базу, чтобы вернуть
        Vector3  camBasePos = Vector3.zero;
        Quaternion camBaseRot = Quaternion.identity;
        if (screenCamera)
        {
            camBasePos = screenCamera.localPosition;
            camBaseRot = screenCamera.localRotation;
        }

        while (t < sequenceDuration)
        {
            // вращение предмета
            if (spinItem && itemImage)
                itemImage.rectTransform.localEulerAngles = new Vector3(0, 0, -t * rpmToDeg);

            // шум CRT к концу сценки
            if (crtMaterial && crtMaterial.HasProperty(noisePropName) && t >= noiseRampStart)
            {
                float np = Mathf.InverseLerp(noiseRampStart, sequenceDuration, t);
                float v  = Mathf.Lerp(_noiseStartValue, noiseMax, noiseCurve.Evaluate(np));
                crtMaterial.SetFloat(noisePropName, v);
            }

            // шейк камеры
            if (shakeDuringSequence && screenCamera)
            {
                float k = shakeCurve.Evaluate(Mathf.Clamp01(t / sequenceDuration));
                float n = t * shakeFrequency;

                // плавный шум на основе Perlin
                float ox = (Mathf.PerlinNoise(n,  3.1f) - 0.5f) * 2f * shakePosAmplitude * k;
                float oy = (Mathf.PerlinNoise(n, 17.9f) - 0.5f) * 2f * shakePosAmplitude * k;
                float rz = (Mathf.PerlinNoise(n, 42.0f) - 0.5f) * 2f * shakeRotAmplitude * k;

                screenCamera.localPosition = camBasePos + new Vector3(ox, oy, 0f);
                screenCamera.localRotation = Quaternion.Euler(0, 0, rz) * camBaseRot;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // вернуть камеру и CRT-шум
        if (screenCamera)
        {
            screenCamera.localPosition = camBasePos;
            screenCamera.localRotation = camBaseRot;
        }
        if (crtMaterial && crtMaterial.HasProperty(noisePropName))
            crtMaterial.SetFloat(noisePropName, _noiseStartValue);

        // спрятать Victory, показать Screen
        ToggleCanvas(victoryCanvas, false, deactivateVictoryCanvasGO);
        if (screenCanvas) ToggleCanvas(screenCanvas, true);

        // вернуть корень пазла (для следующего уровня)
        if (puzzleRoot)        puzzleRoot.SetActive(true);
        if (puzzleCanvasGroup) ToggleCanvas(puzzleCanvasGroup, true);

        // переключить уровень (если нужно)
        if (advanceLevelWhenDone)
        {
            var switcher = FindAnyObjectByType<LevelSwitcher>();
            if (switcher) switcher.Next();
        }

        _seq = null;
    }

    IEnumerator Dots(TMP_Text txt)
    {
        const string baseText = "Materialization";
        int dots = 0;
        while (true)
        {
            dots = (dots + 1) % 4; // 0..3
            txt.text = baseText + new string('.', dots);
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    static void ToggleCanvas(CanvasGroup cg, bool on, bool deactivateGO = false)
    {
        if (!cg) return;
        cg.alpha = on ? 1f : 0f;
        cg.interactable   = on;
        cg.blocksRaycasts = on;
        if (deactivateGO && cg.gameObject.activeSelf != on)
            cg.gameObject.SetActive(on);
    }
}
