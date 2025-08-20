using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem; // NEW INPUT SYSTEM

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem I { get; private set; }
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (heroText) heroText.gameObject.SetActive(false);
    }

    [Header("UI")]
    public TMP_Text mascotText;
    public TMP_Text heroText;

    [System.Serializable]
    public class VoiceProfile
    {
        public AudioSource source;
        public AudioClip blip;
        [Range(0.1f, 3f)] public float basePitch = 1f;
        [Range(0f, 1f)]   public float pitchJitter = 0.15f;
        [Range(0f, 1f)]   public float volume = 0.6f;
        [Min(1)] public int lettersPerBlip = 2;
    }

    [Header("Voices")]
    public VoiceProfile mascotVoice;
    public VoiceProfile heroVoice;

    [Header("Banks")]
    [TextArea] public string[] mascotTutorialLines;
    [TextArea] public string mascotIdlePlaceholder = "…";
    [TextArea] public string[] heroMaterializeLines;   // ONE line per level: index == level index

    [Header("Typing")]
    [Min(1f)]  public float defaultCharsPerSecond = 30f;
    public float pauseAfterComma = 0.05f;
    public float pauseAfterSentence = 0.18f;
    public bool  playBleeps = true;
    public bool  skipWithInput = true;
    public float dotDuration = 0.5f; // each '.' = 0.5 s → '..' = 1 s

    [Header("Hold")]
    [Min(0f)] public float holdAfterTyped = 1.0f; // keep text visible after typing

    [Header("State")]
    public int currentLevelIndex = -1; // pick hero line by level index

    Coroutine _mascotCo, _heroCo;

    // -------- API --------
    public void SetCurrentLevelIndex(int index) => currentLevelIndex = index;

    public void ShowMascotPlaceholder()
    {
        StopMascot();
        if (mascotText) mascotText.text = mascotIdlePlaceholder;
    }

    public void PlayMascotTutorial()
    {
        if (mascotTutorialLines == null || mascotTutorialLines.Length == 0) { ShowMascotPlaceholder(); return; }
        StopMascot();
        _mascotCo = StartCoroutine(RunLines(mascotText, mascotVoice, mascotTutorialLines, defaultCharsPerSecond));
    }

    public void SayMascot(string line, float cps = -1f)
    {
        StopMascot();
        _mascotCo = StartCoroutine(TypeLine(mascotText, mascotVoice, line, cps > 0 ? cps : defaultCharsPerSecond));
    }

    public void SayHero(string line, float cps = -1f)
    {
        StopHero();
        _heroCo = StartCoroutine(SayHeroRoutine(line, cps > 0 ? cps : defaultCharsPerSecond));
    }

    // Use level-indexed line
    public void SayHeroOnMaterialize()
    {
        if (heroMaterializeLines == null || heroMaterializeLines.Length == 0) return;

        string line = null;
        if (currentLevelIndex >= 0 && currentLevelIndex < heroMaterializeLines.Length)
        {
            line = heroMaterializeLines[currentLevelIndex];
            if (string.IsNullOrWhiteSpace(line)) return; // empty = silence
        }
        else
        {
            return; // out of range
        }

        SayHero(line);
    }

    public void StopAllDialogues() { StopMascot(); StopHero(); }

    // -------- Internal --------
    void StopMascot() { if (_mascotCo != null) StopCoroutine(_mascotCo); _mascotCo = null; }
    void StopHero()
    {
        if (_heroCo != null) StopCoroutine(_heroCo);
        _heroCo = null;
        if (heroText) heroText.gameObject.SetActive(false);
    }

    IEnumerator SayHeroRoutine(string line, float cps)
    {
        if (heroText) heroText.gameObject.SetActive(true);
        yield return TypeLine(heroText, heroVoice, line, cps);
        if (holdAfterTyped > 0f)
            yield return new WaitForSecondsRealtime(holdAfterTyped);
        if (heroText) heroText.gameObject.SetActive(false);
    }

    IEnumerator RunLines(TMP_Text target, VoiceProfile voice, string[] lines, float cps)
    {
        foreach (var l in lines)
        {
            yield return TypeLine(target, voice, l, cps);
            // Hold after each finished line (fallback to the old 0.25s if set to 0)
            float wait = holdAfterTyped > 0f ? holdAfterTyped : 0.25f;
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
        }
    }

    IEnumerator TypeLine(TMP_Text target, VoiceProfile voice, string line, float cps)
    {
        if (!target) yield break;
        if (string.IsNullOrEmpty(line)) { target.text = ""; yield break; }

        target.text = "";
        float delay = 1f / Mathf.Max(1f, cps);
        int blipCounter = 0;
        bool skip = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            target.text += ch;

            // Blip: not for whitespace and NOT for dots
            if (playBleeps && voice != null && voice.source && voice.blip && !char.IsWhiteSpace(ch) && ch != '.')
            {
                blipCounter++;
                if (blipCounter >= Mathf.Max(1, voice.lettersPerBlip))
                {
                    voice.source.pitch = voice.basePitch + Random.Range(-voice.pitchJitter, voice.pitchJitter);
                    voice.source.PlayOneShot(voice.blip, voice.volume);
                    blipCounter = 0;
                }
            }

            // Pause
            float p;
            if (ch == '.')          p = dotDuration;
            else if (ch == ',' || ch == ';') p = delay + pauseAfterComma;
            else if (ch == '!' || ch == '?') p = delay + pauseAfterSentence;
            else                              p = delay;

            float t = 0f;
            while (t < p)
            {
                if (skipWithInput && SkipPressedThisFrame()) { skip = true; break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (skip) { target.text = line; break; }
        }
    }

    // New Input System helper
    bool SkipPressedThisFrame()
    {
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool pad   = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        bool touch = Touchscreen.current != null &&
                     Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return mouse || space || pad || touch;
    }
}
