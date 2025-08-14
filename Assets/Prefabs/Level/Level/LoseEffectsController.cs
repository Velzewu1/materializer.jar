using System.Collections;
using UnityEngine;

public class LoseEffectsController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;              // авто-найдём, если не задан
    public LevelSwitcher   switcher;                 // опционально: чтобы знать текущий индекс
    public Material        crtMaterial;              // материал CRT пост-эффекта

    [Header("CRT Properties (names must match the shader)")]
    public string noisePropName     = "_NoiseAmount";
    public string tintPropName      = "_Tint";
    public string intensityPropName = "_Intensity";

    [Header("Lose Target Values")]
    [Range(0f, 1f)] public float noiseLoseValue = 0.8f;                   // сильный шум на проигрыше
    public Color  loseTint       = new Color(1f, 0.2f, 0.2f, 1f);          // красный оттенок
    [Range(0f, 2f)] public float loseIntensity  = 1.0f;

    [Header("Reset After Effect (optional)")]
    public bool  resetNoiseAfterHold = true;
    [Range(0f, 1f)] public float noiseResetValue = 0.04f;                  // вернуть к 0.04
    public bool  resetTintAfterHold  = false;
    public bool  resetIntAfterHold   = false;

    [Header("Timing")]
    [Min(0f)] public float rampDuration  = 0.35f;                          // разгон к пику
    [Min(0f)] public float holdDuration  = 0.00f;                          // удержание пика
    [Min(0f)] public float resetDuration = 0.35f;                          // откат (если включён)

    [Header("Scene Reload")]
    public bool  reloadSceneAfterFX = true;                                 // перезагрузить сцену
    [Min(0f)] public float reloadDelay = 0.0f;                              // пауза перед перезагрузкой
    public bool  saveIndexOnReload = true;                                  // сохранить текущий индекс в SceneReload

    // runtime cache
    float _startNoise, _startIntensity; Color _startTint;
    bool  _hasTint, _hasIntensity, _hasNoise;
    Coroutine _co;

    void Awake()
    {
        if (!controller) controller = FindAnyObjectByType<PuzzleController>();
        if (!switcher)   switcher   = FindAnyObjectByType<LevelSwitcher>();
    }

    void OnEnable()
    {
        if (controller) controller.OnLoseEvent += HandleLose;

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
        if (_co != null) { StopCoroutine(_co); _co = null; }
    }

    void HandleLose()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(PlayLoseEffect());
    }

    IEnumerator PlayLoseEffect()
    {
        // 1) RAMP → lose values
        float t = 0f;
        float fromNoise     = _hasNoise     ? crtMaterial.GetFloat(noisePropName)     : 0f;
        Color fromTint      = _hasTint      ? crtMaterial.GetColor(tintPropName)      : Color.white;
        float fromIntensity = _hasIntensity ? crtMaterial.GetFloat(intensityPropName) : 0f;

        while (t < rampDuration)
        {
            float k = rampDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / rampDuration);
            if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     Mathf.Lerp(fromNoise,     noiseLoseValue, k));
            if (_hasTint)      crtMaterial.SetColor(tintPropName,      Color.Lerp(fromTint,      loseTint,       k));
            if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, Mathf.Lerp(fromIntensity, loseIntensity,  k));
            t += Time.unscaledDeltaTime; yield return null;
        }
        if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     noiseLoseValue);
        if (_hasTint)      crtMaterial.SetColor(tintPropName,      loseTint);
        if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, loseIntensity);

        // 2) HOLD
        if (holdDuration > 0f) { yield return new WaitForSecondsRealtime(holdDuration); }

        // 3) RESET (optional)
        if (resetDuration > 0f && (resetNoiseAfterHold || resetTintAfterHold || resetIntAfterHold))
        {
            float rnFrom = _hasNoise ? crtMaterial.GetFloat(noisePropName) : 0f;
            Color rtFrom = _hasTint  ? crtMaterial.GetColor(tintPropName)  : Color.white;
            float riFrom = _hasIntensity ? crtMaterial.GetFloat(intensityPropName) : 0f;

            float rnTo = resetNoiseAfterHold ? noiseResetValue : rnFrom;
            Color rtTo = resetTintAfterHold  ? _startTint      : rtFrom;
            float riTo = resetIntAfterHold   ? _startIntensity : riFrom;

            t = 0f;
            while (t < resetDuration)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / resetDuration);
                if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     Mathf.Lerp(rnFrom, rnTo, k));
                if (_hasTint)      crtMaterial.SetColor(tintPropName,      Color.Lerp(rtFrom, rtTo, k));
                if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, Mathf.Lerp(riFrom, riTo, k));
                t += Time.unscaledDeltaTime; yield return null;
            }
            if (_hasNoise)     crtMaterial.SetFloat(noisePropName,     rnTo);
            if (_hasTint)      crtMaterial.SetColor(tintPropName,      rtTo);
            if (_hasIntensity) crtMaterial.SetFloat(intensityPropName, riTo);
        }

        // 4) Scene reload (централизовано через SceneReload)
        if (reloadSceneAfterFX)
        {
            if (reloadDelay > 0f) yield return new WaitForSecondsRealtime(reloadDelay);

            // определяем, какой индекс сохранять/перезагружать
            if (switcher && switcher.LevelCount > 0)
            {
                int idx = Mathf.Clamp(switcher.CurrentIndex, 0, switcher.LevelCount - 1);
                if (saveIndexOnReload) SceneReload.SaveIndex(idx);
                SceneReload.ReloadActive(); // рестарт текущего уровня
            }
            else
            {
                // страховка: просто перезагрузим активную сцену (индекс, если был, сохранён ранее)
                SceneReload.ReloadActive();
            }
        }

        _co = null;
    }
}
