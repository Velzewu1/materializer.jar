using UnityEngine;

[CreateAssetMenu(menuName = "Fill-a-Pix/Level Data", fileName = "Level_16x16")]
public class LevelData : ScriptableObject
{
    public int size = 16;

    // 0/1 — целевая пиксельная картинка
    public int[] target;

    // -1..9 — подсказки (-1 = скрыто)
    public int[] clues;

    // 0/1 — «красные» заблокированные клетки
    public int[] locks;

    public int Get(int[] arr, int r, int c) => arr[r * size + c];
    public void Set(int[] arr, int r, int c, int v) => arr[r * size + c] = v;

    public void Resize(int newSize) {
        size = newSize;
        target = new int[size * size];
        clues  = new int[size * size];
        locks  = new int[size * size];
    }
}
