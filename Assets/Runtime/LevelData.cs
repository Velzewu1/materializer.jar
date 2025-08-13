using UnityEngine;

[CreateAssetMenu(menuName = "Fill-a-Pix/LevelData")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int size = 16;     // N x N
    public int[] target;               // 0/1, длина N*N
    public int[] locks;                // 0/1, длина N*N
    public int[] clues;                // -1..9, длина N*N (-1 => подсказка скрыта)

    [Header("Victory (optional)")]
    public Sprite itemSprite;          // 32x32 и т.п. картинка предмета для сценки
    public string itemLabel;           // подпись предмета (Knife, Key, ...)

    // Индексация
    public int Get(int[] a, int r, int c) => a[r * size + c];
    public void Set(int[] a, int r, int c, int v) { a[r * size + c] = v; }

    void OnValidate()
    {
        size = Mathf.Max(1, size);
        EnsureArrays();
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
