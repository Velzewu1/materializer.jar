using UnityEngine;

[CreateAssetMenu(menuName = "Fill-a-Pix/LevelData")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int size = 16;     // N x N

    // Базовые поля
    public int[] target;               // 0/1, длина N*N
    public int[] locks;                // 0/1, длина N*N (красные зоны = пусто/некликабельно)
    public int[] clues;                // -1..9, длина N*N (-1 => подсказка скрыта)

    // --- СЕКТОРА ---

    // Для каждой клетки — ID сектора (0..K-1). По умолчанию всё = 0 (единый сектор).
    public int[] sectorId;             // длина N*N

    // Видимость каждого сектора (K штук). Если массив пуст — считаем все видимыми.
    // Размер K управляется полем sectors; при валидации подгоняется под max(sectorId)+1.
    [SerializeField] private int sectors = 1;
    public bool[] sectorVisible;       // длина = sectors

    // ---------- Индексация ----------
    public int Get(int[] a, int r, int c) => a[r * size + c];
    public void Set(int[] a, int r, int c, int v) { a[r * size + c] = v; }

    public int GetSectorId(int r, int c) => sectorId != null && sectorId.Length == size * size
        ? sectorId[r * size + c] : 0;

    public void SetSectorId(int r, int c, int id)
    {
        EnsureArrays();
        int idx = r * size + c;
        sectorId[idx] = Mathf.Max(0, id);
        EnsureSectorTableFromMap(); // вдруг появился новый maxId
    }

    public int SectorCount => Mathf.Max(1, sectors);

    public bool IsSectorVisible(int id)
    {
        if (sectorVisible == null || sectorVisible.Length == 0) return true;
        id = Mathf.Clamp(id, 0, sectorVisible.Length - 1);
        return sectorVisible[id];
    }

    public void RevealSector(int id, bool visible = true)
    {
        EnsureArrays();
        id = Mathf.Clamp(id, 0, sectorVisible.Length - 1);
        sectorVisible[id] = visible;
    }

    // ---------- Жизненный цикл ----------

    void OnValidate()
    {
        size = Mathf.Max(1, size);
        EnsureArrays();
    }

    public void EnsureArrays()
    {
        int need = size * size;

        if (target == null || target.Length != need) target = new int[need];
        if (locks  == null ||  locks.Length != need)  locks = new int[need];

        if (clues == null || clues.Length != need)
        {
            clues = new int[need];
            for (int i = 0; i < need; i++) clues[i] = -1;
        }

        if (sectorId == null || sectorId.Length != need)
        {
            int oldNeed = sectorId != null ? sectorId.Length : -1;
            var newMap = new int[need];

            // если уже был массив другого размера — скопируем пересечение
            if (sectorId != null)
            {
                int copy = Mathf.Min(sectorId.Length, newMap.Length);
                System.Array.Copy(sectorId, newMap, copy);
            }
            // остальное — в сектор 0
            for (int i = Mathf.Max(0, oldNeed); i < need; i++) newMap[i] = 0;

            sectorId = newMap;
        }

        EnsureSectorTableFromMap();
    }

    /// Подгоняет таблицу видимости под фактическое число секторов по карте.
    void EnsureSectorTableFromMap()
    {
        int maxId = 0;
        if (sectorId != null && sectorId.Length == size * size)
        {
            for (int i = 0; i < sectorId.Length; i++)
                if (sectorId[i] > maxId) maxId = sectorId[i];
        }

        int desired = Mathf.Max(1, maxId + 1);      // как минимум 1 сектор
        if (sectors < desired) sectors = desired;

        if (sectorVisible == null || sectorVisible.Length != sectors)
        {
            var newVis = new bool[sectors];
            // по умолчанию — все видимы
            for (int i = 0; i < newVis.Length; i++) newVis[i] = true;

            // сохранить, что было
            if (sectorVisible != null)
            {
                int copy = Mathf.Min(sectorVisible.Length, newVis.Length);
                System.Array.Copy(sectorVisible, newVis, copy);
            }

            sectorVisible = newVis;
        }
    }

    public bool IsValid()
    {
        int need = size * size;
        return target != null && locks != null && clues != null && sectorId != null &&
               target.Length == need && locks.Length == need && clues.Length == need && sectorId.Length == need &&
               sectorVisible != null && sectorVisible.Length == SectorCount;
    }
}
