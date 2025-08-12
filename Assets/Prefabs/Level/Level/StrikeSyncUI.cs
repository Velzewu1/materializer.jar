using UnityEngine;
using TMPro;

public class StrikeSyncUI : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController controller;          // перетащи из сцены
    public TextMeshProUGUI percentText;          // TMP-вывод

    [Header("Label")]
    [SerializeField] private string label = "SYNCHRONISATION: ";
    [SerializeField] private bool uppercaseLabel = true;   // при необходимости отключи

    [Header("Format")]
    [Tooltip("Округлять до целых процентов.")]
    public bool roundToInt = true;
    [Range(0,3)] public int decimals = 0;                 // если roundToInt=false

    void Awake()
    {
        if (!controller) controller = GameObject.FindAnyObjectByType<PuzzleController>();
    }

    void OnEnable()
    {
        if (!controller) return;

        controller.OnStrike         += HandleStrike;
        controller.OnSyncChanged    += HandleSync;
        controller.OnStrikesChanged += HandleStrikesMirror;

        // первичная отрисовка
        Apply(controller.Sync01);
    }

    void OnDisable()
    {
        if (!controller) return;
        controller.OnStrike         -= HandleStrike;
        controller.OnSyncChanged    -= HandleSync;
        controller.OnStrikesChanged -= HandleStrikesMirror;
    }

    void HandleStrike(int strikes, int maxStrikes, float sync01) => Apply(sync01);
    void HandleSync(float sync01)                                => Apply(sync01);
    void HandleStrikesMirror(int strikes, int maxStrikes)
    {
        float v01 = 1f - (float)strikes / Mathf.Max(1, maxStrikes);
        Apply(v01);
    }

    void Apply(float v01)
    {
        if (!percentText) return;

        float pct = Mathf.Clamp01(v01) * 100f;
        string pctStr = roundToInt ? Mathf.RoundToInt(pct).ToString()
                                   : pct.ToString($"F{decimals}");

        string prefix = uppercaseLabel ? label.ToUpperInvariant() : label;
        percentText.text = $"{prefix}{pctStr}%";
    }
}
