namespace Minesweeper.Core;

/// <summary>格子标记状态</summary>
public enum CellMark
{
    None,     // 无标记
    Flag,     // 旗（不可翻开）
    Question, // 疑问（可翻开）
}

/// <summary>翻开操作结果</summary>
public enum RevealResult
{
    NoOp,     // 无效操作（已翻开/旗格/越界/游戏结束）
    Revealed, // 正常翻开
    Win,      // 翻开后胜利
    Mine,     // 踩雷
}

/// <summary>Chord（双击数字格）操作结果</summary>
public enum ChordResult
{
    NoOp,     // 条件不满足或无效
    Revealed, // 翻开周围格
    Win,
    Mine,     // 周围有错旗，踩雷
}

/// <summary>
/// 棋盘状态机：雷板生成、翻开、洪水展开、chord、标记循环、胜负判定。
/// 纯 C# 逻辑，零 UI 依赖，可单元测试。
/// </summary>
public sealed class Minefield
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int MineCount { get; private set; }
    public int TotalCells => Width * Height;
    public int SafeCount => TotalCells - MineCount;

    /// <summary>雷板是否已生成（首次翻开后生成，保证首击安全）</summary>
    public bool MinesGenerated { get; private set; }

    /// <summary>已翻开格数</summary>
    public int RevealedCount { get; private set; }

    /// <summary>当前旗数</summary>
    public int FlagCount { get; private set; }

    public bool IsWin => MinesGenerated && RevealedCount == SafeCount;

    private bool[] _mines;
    private bool[] _revealed;
    private CellMark[] _marks;
    private List<int>[] _neighbors;
    private Random _random;

    public Minefield(int width, int height, int mineCount, Random? random = null)
    {
        if (mineCount <= 0 || mineCount >= width * height)
            throw new ArgumentOutOfRangeException(nameof(mineCount), "雷数必须大于 0 且小于格子总数");
        Initialize(width, height, mineCount, random);
    }

    /// <summary>直接按指定雷位布局构造（雷板视为已生成，用于测试与棋盘加载）</summary>
    public Minefield(int width, int height, bool[] layout, Random? random = null)
    {
        int mineCount = 0;
        foreach (bool m in layout) if (m) mineCount++;
        if (mineCount <= 0 || mineCount >= width * height)
            throw new ArgumentOutOfRangeException(nameof(layout), "雷数必须大于 0 且小于格子总数");
        Initialize(width, height, mineCount, random);
        Array.Copy(layout, _mines, TotalCells);
        MinesGenerated = true;
    }

    private void Initialize(int width, int height, int mineCount, Random? random)
    {
        Width = width;
        Height = height;
        MineCount = mineCount;
        _mines = new bool[TotalCells];
        _revealed = new bool[TotalCells];
        _marks = new CellMark[TotalCells];
        _random = random ?? new Random();

        // 预计算每个格的邻居索引（洪水展开时复用，避免重复计算）
        _neighbors = new List<int>[TotalCells];
        for (int i = 0; i < TotalCells; i++)
            _neighbors[i] = ComputeNeighbors(i);
    }

    public static int ToIndex(int width, int x, int y) => y * width + x;

    public bool IsMine(int index) => _mines[index];
    public bool IsRevealed(int index) => _revealed[index];
    public CellMark GetMark(int index) => _marks[index];
    public IEnumerable<int> GetNeighborsOf(int index) => _neighbors[index];

    private List<int> ComputeNeighbors(int index)
    {
        int x = index % Width;
        int y = index / Width;
        var list = new List<int>(8);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                    list.Add(ToIndex(Width, nx, ny));
            }
        }
        return list;
    }

    /// <summary>某格周围相邻雷数（0~8）</summary>
    public int AdjacentMines(int index)
    {
        int count = 0;
        foreach (int n in _neighbors[index])
            if (_mines[n]) count++;
        return count;
    }

    /// <summary>某格周围旗数</summary>
    public int AdjacentFlags(int index)
    {
        int count = 0;
        foreach (int n in _neighbors[index])
            if (_marks[n] == CellMark.Flag) count++;
        return count;
    }

    /// <summary>
    /// 生成雷板：保证首击格及其周围 8 格无雷。
    /// 极小棋盘（雷数接近满盘）时缩小排除区，始终保证 safeIndex 本身无雷。
    /// </summary>
    public void GenerateMines(int safeIndex)
    {
        if (MinesGenerated) return;

        var excluded = new List<int> { safeIndex };
        excluded.AddRange(_neighbors[safeIndex]);

        int available = TotalCells - excluded.Count;
        int minExcluded = 1; // 至少要排除 safeIndex 本身
        while (available < MineCount && excluded.Count > minExcluded)
        {
            excluded.RemoveAt(excluded.Count - 1); // 逐个放宽：最后移除的是邻居
            available = TotalCells - excluded.Count;
        }

        var excludedSet = new HashSet<int>(excluded);
        int placed = 0;
        while (placed < MineCount)
        {
            int candidate = _random.Next(TotalCells);
            if (excludedSet.Contains(candidate) || _mines[candidate]) continue;
            _mines[candidate] = true;
            placed++;
        }
        MinesGenerated = true;
    }

    /// <summary>翻开一格（若雷板未生成则先生成）。旗格不可翻开，问号格可翻开。</summary>
    public RevealResult Reveal(int index)
    {
        if (index < 0 || index >= TotalCells) return RevealResult.NoOp;
        if (_revealed[index]) return RevealResult.NoOp;
        if (_marks[index] == CellMark.Flag) return RevealResult.NoOp;

        if (!MinesGenerated) GenerateMines(index);

        if (_mines[index])
        {
            _revealed[index] = true;
            RevealedCount++;
            return RevealResult.Mine;
        }

        FloodReveal(index);
        return IsWin ? RevealResult.Win : RevealResult.Revealed;
    }

    /// <summary>0 格洪水展开连通区域，沿途翻开数字格；跳过旗格</summary>
    private void FloodReveal(int index)
    {
        var stack = new Stack<int>();
        stack.Push(index);
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (_revealed[i]) continue;
            if (_mines[i]) continue; // 防御：洪水不翻雷
            _revealed[i] = true;
            RevealedCount++;
            if (_marks[i] != CellMark.None)
            {
                if (_marks[i] == CellMark.Flag) FlagCount--;
                _marks[i] = CellMark.None;
            }

            if (AdjacentMines(i) == 0)
            {
                foreach (int n in _neighbors[i])
                    if (!_revealed[n] && !_mines[n] && _marks[n] != CellMark.Flag)
                        stack.Push(n);
            }
        }
    }

    /// <summary>
    /// Chord：在已翻开的数字格上，当周围旗数等于数字时翻开周围所有未标记格。
    /// 旗数不匹配则不动作（经典行为，不误炸）。
    /// </summary>
    public ChordResult Chord(int index)
    {
        if (index < 0 || index >= TotalCells) return ChordResult.NoOp;
        if (!MinesGenerated) return ChordResult.NoOp;
        if (!_revealed[index]) return ChordResult.NoOp;

        int number = AdjacentMines(index);
        if (number == 0) return ChordResult.NoOp;
        if (AdjacentFlags(index) != number) return ChordResult.NoOp;

        bool hitMine = false;
        foreach (int n in _neighbors[index])
        {
            if (_marks[n] == CellMark.Flag || _revealed[n]) continue;
            if (_mines[n])
            {
                _revealed[n] = true;
                RevealedCount++;
                hitMine = true;
            }
            else
            {
                FloodReveal(n);
            }
        }

        if (hitMine) return ChordResult.Mine;
        return IsWin ? ChordResult.Win : ChordResult.Revealed;
    }

    /// <summary>标记循环：无 → 旗 → 疑问 → 无。已翻开的格不响应。返回新状态。</summary>
    public CellMark CycleMark(int index)
    {
        if (index < 0 || index >= TotalCells || _revealed[index]) return CellMark.None;
        switch (_marks[index])
        {
            case CellMark.None:
                _marks[index] = CellMark.Flag;
                FlagCount++;
                break;
            case CellMark.Flag:
                _marks[index] = CellMark.Question;
                FlagCount--;
                break;
            case CellMark.Question:
                _marks[index] = CellMark.None;
                break;
        }
        return _marks[index];
    }

    /// <summary>直接设置标记（键盘 F/Q 快捷键用）。返回新状态。</summary>
    public CellMark SetMark(int index, CellMark mark)
    {
        if (index < 0 || index >= TotalCells || _revealed[index]) return CellMark.None;
        if (_marks[index] == CellMark.Flag) FlagCount--;
        _marks[index] = mark;
        if (mark == CellMark.Flag) FlagCount++;
        return _marks[index];
    }

    /// <summary>胜利展示：把所有未翻开的雷格直接标为旗</summary>
    public void MarkAllMinesAsFlagged()
    {
        for (int i = 0; i < TotalCells; i++)
            if (!_revealed[i] && _mines[i])
                SetMark(i, CellMark.Flag);
    }

    /// <summary>清空棋盘（保留尺寸），准备新一局</summary>
    public void Reset()
    {
        Array.Clear(_mines);
        Array.Clear(_revealed);
        Array.Clear(_marks);
        MinesGenerated = false;
        RevealedCount = 0;
        FlagCount = 0;
    }

    // ---------- 撤销快照（供 GameEngine 的撤销栈使用） ----------

    internal sealed class Snapshot
    {
        public bool[] Mines = null!;
        public bool[] Revealed = null!;
        public CellMark[] Marks = null!;
        public bool MinesGenerated;
        public int RevealedCount;
        public int FlagCount;
    }

    /// <summary>捕获当前完整棋盘状态（数组浅拷贝，供操作前快照）</summary>
    internal Snapshot CaptureSnapshot() => new()
    {
        Mines = (bool[])_mines.Clone(),
        Revealed = (bool[])_revealed.Clone(),
        Marks = (CellMark[])_marks.Clone(),
        MinesGenerated = MinesGenerated,
        RevealedCount = RevealedCount,
        FlagCount = FlagCount,
    };

    /// <summary>恢复棋盘状态（撤销用）。<paramref name="snapshot"/> 必须来自本棋盘的 CaptureSnapshot。</summary>
    internal void RestoreSnapshot(Snapshot snapshot)
    {
        _mines = (bool[])snapshot.Mines.Clone();
        _revealed = (bool[])snapshot.Revealed.Clone();
        _marks = (CellMark[])snapshot.Marks.Clone();
        MinesGenerated = snapshot.MinesGenerated;
        RevealedCount = snapshot.RevealedCount;
        FlagCount = snapshot.FlagCount;
    }
}
