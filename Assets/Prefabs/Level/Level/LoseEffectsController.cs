using System.Collections;
using UnityEngine;

public class LoseEffectsController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;           // авто-найдём, если не задан
    public Material crtMaterial;                  // материал вашего CRT пост-эффекта

    [Header("CRT Properties (names must match the shader)")]
    public string noisePropName     = "_NoiseAmount"; // из шейдера
    public string tintPropName      = "_Tint";        // из шейдера
    public string intensityPropName = "_Intensity";   // из шейдера

    [Header("Lose Target Values")]
    [Tooltip("Цель шума при проигрыше. В шейдере Range(0,0.3) — это лишь ограничение слайдера в инспекторе, не жёсткий кламп.")]
    [Range(0f, 1f)]
    public float noiseLoseValue = 0.8f;               // сильный шум на проигрыше
    public Color loseTint       = new Color(1f, 0.2f, 0.2f, 1f); // красный оттенок
    [Range(0f, 2f)]
    public float loseIntensity  = 1.0f;               // мощность красного тинта

    [Header("Reset After Effect")]
    public bool  resetNoiseAfterHold = true;          // вернуть шум после эффекта
    [Range(0f, 1f)]
    public float noiseResetValue     = 0.04f;         // требуемые 0.04
    public bool  resetTintAfterHold  = false;         // по умолчанию оставляем красный
    public bool  resetIntAfterHold   = false;         // по умолчанию оставляем интенсивность

    [Header("Timing")]
    [Min(0f)] public float rampDuration  = 0.35f;     // разгон к проигрышным значениям
    [Min(0f)] public float holdDuration  = 0.00f;     // удержание пика
    [Min(0f)] public float resetDuration = 0.35f;     // откат значений (если включены флаги reset*)

    // runtime
    float _startNoise;
    Color _startTint;
    float _startIntensity;
    bool  _hasTint, _hasIntensity, _hasNoise;

    Coroutine _co;

    void OnEnable()
    {
        if (!controller) controller = FindAnyObjectByType<PuzzleController>();
        if (controller) controller.OnLoseEvent += HandleLose;

        // Запоминаем исходные значения
        _hasNoise     = crtMaterial && crtMaterial.HasProperty(noisePropName);
        _hasTint      = crtMaterial && crtMaterial.HasProperty(tintPropName);
        _hasIntensity = crtMaterial && crtMaterial.HasProperty(intensityPropName);

        if (_hasNoise)     _startNoise     = crtMaterial.GetFloat(noisePropName);
        if (_hasTint)      _startTint      = crtMaterial.GetColor(tintPropName);
        if (_hasIntensity) _startIntensity = crtMaterial.GetFloat(intensityPropName);
    }

    void OnDisable()
    {
        if (controller) controller.OnLoseEvent -= HandleLose;
    }

    void HandleLose()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(PlayLoseEffect());
    }

    IEnumerator PlayLoseEffect()
    {
        float t = 0f;

        float fromNoise     = _hasNoise     ? crtMaterial.GetFloat(noisePropName) : 0f;
        Color fromTint      = _hasTint      ? crtMaterial.GetColor(tintPropName)  : Color.white;
        float fromIntensity = _hasIntensity ? crtMaterial.GetFloat(intensityPropName) : 0f;

        // 1) Разгон к проигрышным значениям
        while (t < rampDuration)
        {
            float k = rampDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / rampDuration);
            if (_hasNoise)     crtMaterial.SetFloat(noisePropName, Mathf.Lerp(fromNoise,     noiseLoseValue, k));
            if (_hasTint)      crtMaterial.SetColor(tintPropName,  Color.Lerp(fromTint,      loseTint,       k));
            if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, Mathf.Lerp(fromIntensity, loseIntensity, k));

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     noiseLoseValue);
        if (_hasTint)      crtMaterial.SetColor(tintPropName,      loseTint);
        if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, loseIntensity);

        // 2) Удержание
        if (holdDuration > 0f)
        {
            float h = 0f;
            while (h < holdDuration)
            {
                h += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 3) Откат
        float rnFrom = _hasNoise ? crtMaterial.GetFloat(noisePropName) : 0f;
        Color rtFrom = _hasTint  ? crtMaterial.GetColor(tintPropName)  : Color.white;
        float riFrom = _hasIntensity ? crtMaterial.GetFloat(intensityPropName) : 0f;

        float rnTo = resetNoiseAfterHold ? noiseResetValue : rnFrom;         // вернуть к 0.04
        Color rtTo = resetTintAfterHold  ? _startTint      : rtFrom;         // опционально вернуть исходный зелёный
        float riTo = resetIntAfterHold   ? _startIntensity : riFrom;         // опционально вернуть исходную интенсивность

        t = 0f;
        while (t < resetDuration)
        {
            float k = resetDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / resetDuration);
            if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     Mathf.Lerp(rnFrom, rnTo, k));
            if (_hasTint)      crtMaterial.SetColor(tintPropName,      Color.Lerp(rtFrom, rtTo, k));
            if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, Mathf.Lerp(riFrom, riTo, k));

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     rnTo);
        if (_hasTint)      crtMaterial.SetColor(tintPropName,      rtTo);
        if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, riTo);

        _co = null;
    }
}
