using UnityEngine;

[CreateAssetMenu(menuName="FillAPix/Level Data 15x15", fileName="Level_15x15")]
public class LevelData : ScriptableObject {
    public int size = 15;

    // 0/1 — целевая картинка (что должно быть закрашено в итоге)
    public int[] target;   // size*size

    // 0..9 — подсказки Fill-a-Pix (центр включён), 0 допустим
    public int[] clues;    // size*size

    // 0/1 — вручную поставленные «красные» клетки (LOCK)
    public int[] locks;    // size*size

    public int Get(int[] a, int r, int c) => a[r*size + c];
    public void Set(int[] a, int r, int c, int v) => a[r*size + c] = v;
}
