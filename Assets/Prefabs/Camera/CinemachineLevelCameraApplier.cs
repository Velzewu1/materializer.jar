using System.Collections;
using System.Reflection;
using UnityEngine;

public class CinemachineLevelCameraApplier : MonoBehaviour
{
    [Tooltip("Cinemachine Virtual Camera / Cinemachine Camera component (NOT Unity Camera).")]
    public Component vcam;

    [Header("Smoothing")]
    [Min(0f)] public float blendTime = 0.6f;

    Coroutine _blendCo;

    void Awake()
    {
        if (!vcam)
        {
            // Ищем без устаревшего FindObjectsOfType(..., true)
            // Возьмём только объекты из загруженных сцен (не из префабов)
            var all = Resources.FindObjectsOfTypeAll<Component>();
            foreach (var c in all)
            {
                if (!c) continue;
                var go = c.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;

                var n = c.GetType().Name;
                if (n == "CinemachineVirtualCamera" || n == "CinemachineCamera")
                {
                    vcam = c;
                    break;
                }
            }
        }
    }

    // ---- Public API ----
    public void ApplyDistance(float distance)
    {
        if (!vcam) return;

        if (blendTime <= 0f)
        {
            SetComposerDistance(distance);
        }
        else
        {
            if (_blendCo != null) StopCoroutine(_blendCo);
            _blendCo = StartCoroutine(BlendDistance(distance, blendTime));
        }
    }

    // ---- Impl ----
    IEnumerator BlendDistance(float target, float time)
    {
        if (!TryGetComposer(out var composer)) yield break;

        float start = GetDistance(composer);
        if (Mathf.Approximately(start, target)) { SetDistance(composer, target); yield break; }

        float t = 0f;
        while (t < time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            SetDistance(composer, Mathf.Lerp(start, target, k));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        SetDistance(composer, target);
        _blendCo = null;
    }

    void SetComposerDistance(float v)
    {
        if (!TryGetComposer(out var composer)) return;
        SetDistance(composer, v);
    }

    bool TryGetComposer(out Component composer)
    {
        composer = null;
        if (!vcam) return false;

        var go = vcam.gameObject;
        foreach (var c in go.GetComponents<Component>())
        {
            if (!c) continue;
            var n = c.GetType().Name;
            if (n == "CinemachinePositionComposer" || n == "CinemachineFramingTransposer")
            {
                composer = c;
                return true;
            }
        }
        return false;
    }

    float GetDistance(Component composer)
    {
        if (!composer) return 0f;
        var t = composer.GetType();

        var pi = t.GetProperty("CameraDistance", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && pi.PropertyType == typeof(float))
            return (float)pi.GetValue(composer);

        var fi = t.GetField("m_CameraDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null && fi.FieldType == typeof(float))
            return (float)fi.GetValue(composer);

        return 0f;
    }

    void SetDistance(Component composer, float v)
    {
        if (!composer) return;
        var t = composer.GetType();

        var pi = t.GetProperty("CameraDistance", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && pi.PropertyType == typeof(float)) { pi.SetValue(composer, v); return; }

        var fi = t.GetField("m_CameraDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null && fi.FieldType == typeof(float)) { fi.SetValue(composer, v); }
    }
}
