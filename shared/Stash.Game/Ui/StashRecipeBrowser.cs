using Engine;
using Engine.Graphics;
using Engine.Input;
using Game;
using Stash.Shared.Network;

namespace Stash.Game;

/// <summary>
/// 配方浏览器：列出全部物品、可搜索、可收藏、点开看合成配方，并把材料一键填进合成格。
///
/// ─────────────────────────────────────────────────────────────────────────
/// **为什么是整屏浮层，而不是像 JEI 那样挂在面板旁边**
///
/// SC 的虚拟画布宽度是 <c>850 / UI缩放</c>，缩放最大 1.2 → 画布只有 **708×398**。
/// 原版面板 614×382 摆进去，左右各只剩 **47 单位**，上下各剩 8。
/// 一个原版格子 72 单位——**47 单位塞不下任何东西**。
/// 手机玩家正好常用大缩放，所以"面板边上永远挂一列物品"在 SC 上根本做不到。
/// （详细数字见 <see cref="StashScreenMetrics"/>。）
///
/// 于是改成：按钮唤出一个整屏浮层，打开时把原版面板藏起来，关掉再放回来。
///
/// **配色和分区照原版来**：底板不覆盖 <c>CenterColor</c>（原版默认就是米色 181,172,154），
/// 每个区块用一块内凹的 <c>BevelledRectangleWidget</c> 圈起来 + 左上角一个小标题，
/// 和 ChestWidget / FullInventoryWidget 的做法一致。第一版做成深色底，实机反馈不喜欢。
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class StashRecipeBrowser : CanvasWidget
{
    // ── 设计尺寸。实际会被 StashScreenMetrics 等比压到画布放得下为止 ──
    private const float Pad = 14f;
    private const float TopBarHeight = 36f;
    private const float SectionLabelHeight = 22f;

    private const int BookmarkColumns = 2;
    private const int BookmarkRows = 6;
    private const int ItemColumns = 8;
    private const int ItemRows = 6;

    /// <summary>格子边长。实机反馈"图标有点小"，从 44 提到 60（原版格子是 72）。</summary>
    private const float CellSize = 60f;

    private const float RecipeCellSize = 56f;
    private const float RecipeAreaWidth = 268f;

    /// <summary>缺料格子的红底。半透明，底下的图标还看得见。</summary>
    private static readonly Color ShortCellColor = new(190, 60, 55, 150);

    private readonly ComponentGui m_gui;
    private readonly StashCraftTarget? m_target;
    private readonly List<IInventory> m_sources;

    private readonly TextBoxWidget m_searchBox;
    private readonly BevelledRectangleWidget m_searchFrame;
    private readonly LabelWidget m_searchHint;
    private readonly LabelWidget m_statusLabel;
    private readonly LabelWidget m_recipeName;
    private readonly LabelWidget m_recipeCounter;

    private readonly ButtonWidget m_closeButton;
    private readonly ButtonWidget m_pageUpButton;
    private readonly ButtonWidget m_pageDownButton;
    private readonly ButtonWidget m_prevRecipeButton;
    private readonly ButtonWidget m_nextRecipeButton;
    private readonly ButtonWidget m_bookmarkButton;
    private readonly ButtonWidget m_fillButton;

    private readonly List<StashItemButton> m_cells = new();
    private readonly List<StashItemButton> m_bookmarkCells = new();
    private readonly List<CraftingRecipeSlotWidget> m_recipeSlots = new();
    private readonly List<BevelledRectangleWidget> m_recipeCellBacks = new();
    private readonly List<ClickableWidget> m_recipeCellClicks = new();
    private readonly LabelWidget m_tooltip;
    private readonly ButtonWidget m_backButton;

    /// <summary>
    /// 往回走的路径：每次从配方里点开一样材料，就把"当时看的是谁、第几条配方"压进来。
    /// 用栈而不是记单个"上一个"——玩家可以顺着材料一层层钻下去
    /// （钻石镐 → 钻石 → …），深度不定，栈天然就支持。
    /// </summary>
    private readonly List<(int Value, int RecipeIndex)> m_history = new();

    /// <summary>栈深上限。到顶就丢掉最早的一层，免得一直点一直涨。</summary>
    private const int MaxHistory = 32;
    private readonly CraftingRecipeSlotWidget m_resultSlot;
    private readonly FireWidget m_smeltFire;
    private readonly LabelWidget m_smeltLabel;
    private readonly LabelWidget m_recipeHint;

    private readonly List<int> m_view = new();
    private readonly List<CraftingRecipe> m_recipes = new();

    private string m_lastQuery = string.Empty;
    private int m_page;
    private int m_selected;
    private int m_recipeIndex;
    private bool m_viewDirty = true;

    /// <summary>当前显示的那条配方。<see cref="IngredientValue"/> 要靠它拿配料串。</summary>
    private CraftingRecipe? m_currentRecipe;

    /// <param name="target">当前界面里的合成格，没有就传 null（那时 ＋ 按钮不可用）。</param>
    /// <param name="sources">取料来源，按优先级排。第一个兼作"腾空合成格时退回哪里"。</param>
    public StashRecipeBrowser(ComponentGui gui, StashCraftTarget? target, List<IInventory> sources)
    {
        m_gui = gui;
        m_target = target;
        m_sources = sources;

        float designWidth = Pad * 4f + BookmarkColumns * CellSize + ItemColumns * CellSize + RecipeAreaWidth;
        float designHeight = Pad * 3f + TopBarHeight + SectionLabelHeight + ItemRows * CellSize + 34f;

        float scale = StashScreenMetrics.FitScale(new Vector2(designWidth, designHeight));
        float pad = Pad * scale;
        float cell = CellSize * scale;
        float topBar = TopBarHeight * scale;
        float labelRow = SectionLabelHeight * scale;
        float width = designWidth * scale;
        float height = designHeight * scale;

        Log.Information($"[Stash] 配方浏览器：缩放 {scale:0.00}，面板 {width:0}x{height:0}，"
            + $"画布预算 {StashScreenMetrics.PanelBudget}，"
            + $"目标格 {(target == null ? "无" : (target.IsFurnace ? $"熔炉 {target.Columns} 格" : $"{target.Columns}x{target.Rows}"))}");

        Size = new Vector2(width, height);
        HorizontalAlignment = WidgetAlignment.Center;
        VerticalAlignment = WidgetAlignment.Center;

        // 底板：**不覆盖 CenterColor**，用原版默认的米色（181,172,154）。
        Children.Add(new BevelledRectangleWidget { Size = new Vector2(width, height), BevelSize = 3f });

        // ── 顶栏：标题 + 搜索框 + 关闭 ──
        AddLabel(StashText.BrowserTitle, new Vector2(pad, pad + 8f * scale));

        float closeWidth = 78f * scale;
        float searchX = pad + 92f * scale;
        float searchWidth = width - searchX - pad - closeWidth - 8f * scale;

        m_searchFrame = new BevelledRectangleWidget
        {
            Size = new Vector2(searchWidth, topBar),
            BevelSize = -2f,
            RoundingRadius = 3f,
            CenterColor = new Color(0, 0, 0, 80),
            IsHitTestVisible = false,
        };
        Add(m_searchFrame, new Vector2(searchX, pad));

        m_searchHint = new LabelWidget
        {
            Text = StashText.SearchHint,
            Size = new Vector2(searchWidth - 16f * scale, topBar),
            TextAnchor = TextAnchor.VerticalCenter,
            Color = new Color(255, 255, 255, 110),
        };
        Add(m_searchHint, new Vector2(searchX + 8f * scale, pad));

        m_searchBox = new TextBoxWidget { Size = new Vector2(searchWidth - 16f * scale, topBar) };
        Add(m_searchBox, new Vector2(searchX + 8f * scale, pad));

        m_closeButton = Button(StashText.Close, closeWidth, topBar);
        Add(m_closeButton, new Vector2(width - pad - closeWidth, pad));

        // ── 三个区块：收藏 / 目录 / 配方 ──
        float labelY = pad + topBar + 4f * scale;
        float gridY = labelY + labelRow;

        float bookmarkWidth = BookmarkColumns * cell;
        float itemX = pad * 2f + bookmarkWidth;
        float itemWidth = ItemColumns * cell;
        float recipeX = itemX + itemWidth + pad;
        float recipeWidth = width - recipeX - pad;
        float gridHeight = ItemRows * cell;

        AddLabel(StashText.Bookmarks, new Vector2(pad, labelY));
        AddSectionFrame(new Vector2(pad, gridY), new Vector2(bookmarkWidth, gridHeight));
        BuildGrid(m_bookmarkCells, BookmarkColumns, BookmarkRows, cell, new Vector2(pad, gridY));

        AddLabel(StashText.AllItems, new Vector2(itemX, labelY));
        AddSectionFrame(new Vector2(itemX, gridY), new Vector2(itemWidth, gridHeight));
        BuildGrid(m_cells, ItemColumns, ItemRows, cell, new Vector2(itemX, gridY));

        AddLabel(StashText.ShowRecipes, new Vector2(recipeX, labelY));
        AddSectionFrame(new Vector2(recipeX, gridY), new Vector2(recipeWidth, gridHeight));

        // ── 底部一行：翻页 + 状态 ──
        float bottomY = gridY + gridHeight + 6f * scale;

        m_pageUpButton = Button("▲", 34f * scale, 28f * scale);
        Add(m_pageUpButton, new Vector2(itemX, bottomY));

        m_pageDownButton = Button("▼", 34f * scale, 28f * scale);
        Add(m_pageDownButton, new Vector2(itemX + 38f * scale, bottomY));

        m_statusLabel = new LabelWidget { Color = new Color(255, 255, 255, 170) };
        Add(m_statusLabel, new Vector2(itemX + 82f * scale, bottomY + 5f * scale));

        // ── 配方区内部 ──
        float recipeCell = RecipeCellSize * scale;
        float recipeInnerX = recipeX + 8f * scale;
        float recipeGridY = gridY + 48f * scale;

        m_recipeName = new LabelWidget
        {
            Size = new Vector2(recipeWidth - 16f * scale, 22f * scale),
            Color = new Color(255, 255, 255, 220),
        };
        Add(m_recipeName, new Vector2(recipeInnerX, gridY + 2f * scale));

        m_recipeHint = new LabelWidget
        {
            Size = new Vector2(recipeWidth - 16f * scale, 20f * scale),
            Color = new Color(255, 255, 255, 140),
            FontScale = 0.75f,
        };
        Add(m_recipeHint, new Vector2(recipeInnerX, gridY + 26f * scale));

        // 复用原版的 CraftingRecipeSlotWidget：它自己会把 "craftingid:data" 解析成图标，
        // 同一个 craftingId 对应多个方块时还会轮流显示（原版图鉴就是这个观感）。
        for (int i = 0; i < 9; i++)
        {
            var position = new Vector2(recipeInnerX + i % 3 * recipeCell, recipeGridY + i / 3 * recipeCell);

            // 每格先垫一块底：缺料时把它染红。CraftingRecipeSlotWidget 自己那块底板
            // 在 XML 里没有 Name，够不着，所以自己加一层。
            var back = new BevelledRectangleWidget
            {
                Size = new Vector2(recipeCell, recipeCell),
                BevelSize = 0f,
                CenterColor = new Color(0, 0, 0, 0),
                BevelColor = new Color(0, 0, 0, 0),
                IsHitTestVisible = false,
            };
            m_recipeCellBacks.Add(back);
            Add(back, position);

            CraftingRecipeSlotWidget slot = NewRecipeSlot(recipeCell, out ClickableWidget? click);
            m_recipeSlots.Add(slot);
            m_recipeCellClicks.Add(click!);
            Add(slot, position);
        }

        float resultX = recipeInnerX + recipeCell * 3f + 20f * scale;
        float resultY = recipeGridY + recipeCell;

        m_resultSlot = NewRecipeSlot(recipeCell, out ClickableWidget? _);
        Add(m_resultSlot, new Vector2(resultX, resultY));

        // 冶炼配方在成品底下画一簇火，一眼看出"这个要进熔炉，不是摆合成格"。
        // FireWidget 是原版熔炉界面用的那个控件，自带粒子，两个平台都有。
        m_smeltFire = new FireWidget { Size = new Vector2(recipeCell, 26f * scale), IsVisible = false };
        Add(m_smeltFire, new Vector2(resultX, resultY + recipeCell));

        m_smeltLabel = new LabelWidget
        {
            Text = StashText.NeedsFurnace,
            Color = new Color(255, 190, 120),
            FontScale = 0.7f,
            IsVisible = false,
        };
        Add(m_smeltLabel, new Vector2(resultX - 6f * scale, resultY + recipeCell + 26f * scale));

        // 换配方。**不能用 ◀ ▶**：SC 的位图字体里没有这两个字形，实机显示成两个方块
        // （实机反馈"怎么还有两个按钮里面是方块"）。▲▼ 是有的，翻页那两个照旧。
        float recipeBottom = recipeGridY + recipeCell * 3f + 6f * scale;

        m_prevRecipeButton = Button(StashText.PrevRecipe, 62f * scale, 28f * scale);
        Add(m_prevRecipeButton, new Vector2(recipeInnerX, recipeBottom));

        m_recipeCounter = new LabelWidget
        {
            Size = new Vector2(46f * scale, 28f * scale),
            TextAnchor = TextAnchor.VerticalCenter | TextAnchor.HorizontalCenter,
            Color = new Color(255, 255, 255, 170),
        };
        Add(m_recipeCounter, new Vector2(recipeInnerX + 66f * scale, recipeBottom));

        m_nextRecipeButton = Button(StashText.NextRecipe, 62f * scale, 28f * scale);
        Add(m_nextRecipeButton, new Vector2(recipeInnerX + 116f * scale, recipeBottom));

        float actionY = bottomY - 2f * scale;

        m_backButton = Button(StashText.Back, 58f * scale, 30f * scale);
        Add(m_backButton, new Vector2(recipeInnerX, actionY));

        m_bookmarkButton = Button(StashText.AddBookmark, 92f * scale, 30f * scale);
        Add(m_bookmarkButton, new Vector2(recipeInnerX + 62f * scale, actionY));

        m_fillButton = Button(StashText.Fill, 76f * scale, 30f * scale);
        Add(m_fillButton, new Vector2(recipeInnerX + 158f * scale, actionY));

        // 悬停提示。**最后加**，才画得在所有格子之上。
        //
        // 只用一个带投影的 LabelWidget，**不要**套"背景框 + 文字"：
        // 没写 Size 的 BevelledRectangleWidget 其 DesiredSize 是 Infinity，
        // 外面那层 CanvasWidget 会被撑成整块面板那么大（CanvasWidget.MeasureOverride
        // 取的是所有孩子的最大值），黑框直接糊满屏幕。
        // LabelWidget 会按文字量出有限的尺寸，正好还能用 ActualSize 做边界夹取。
        m_tooltip = new LabelWidget
        {
            Color = new Color(255, 246, 220),
            DropShadow = true,
            FontScale = 0.85f,
            IsVisible = false,
            IsHitTestVisible = false,
        };
        Children.Add(m_tooltip);

        RefreshView();
        RefreshRecipe();
    }

    // ────────────────────────────── 搭控件的小工具 ──────────────────────────────

    private void Add(Widget widget, Vector2 position)
    {
        Children.Add(widget);
        SetWidgetPosition(widget, position);
    }

    private void AddLabel(string text, Vector2 position) =>
        Add(new LabelWidget { Text = text, Color = new Color(255, 255, 255, 192) }, position);

    /// <summary>区块之间的分隔用一块内凹的框，和原版面板分区的做法一致。</summary>
    private void AddSectionFrame(Vector2 position, Vector2 size) =>
        Add(new BevelledRectangleWidget
        {
            Size = size,
            BevelSize = -2f,
            DirectionalLight = 0.1f,
            CenterColor = new Color(0, 0, 0, 40),
            IsHitTestVisible = false,
        }, position);

    private static ButtonWidget Button(string text, float width, float height) =>
        new BevelledButtonWidget { Text = text, Size = new Vector2(width, height) };

    private void BuildGrid(List<StashItemButton> into, int columns, int rows, float cell, Vector2 position)
    {
        var grid = new GridPanelWidget { ColumnsCount = columns, RowsCount = rows };

        for (int i = 0; i < columns * rows; i++)
        {
            var button = new StashItemButton(cell);
            grid.Children.Add(button);
            grid.SetWidgetCell(button, new Point2(i % columns, i / columns));
            into.Add(button);
        }

        Add(grid, position);
    }

    private static CraftingRecipeSlotWidget NewRecipeSlot(float size, out ClickableWidget? clickable)
    {
        var slot = new CraftingRecipeSlotWidget { Size = new Vector2(size, size) };

        // XML 里图标写死 64×64，外框改小了图标会溢出来，得跟着调。
        if (slot.m_blockIconWidget != null)
        {
            slot.m_blockIconWidget.Size = new Vector2(size - 10f, size - 10f);
        }

        // 配料格要能点（跳到这样材料的配方）。CraftingRecipeSlotWidget 是 CanvasWidget，
        // 直接塞一块铺满的点击区进去；放**最后一个孩子**，HitTestGlobal 倒序遍历时最先命中。
        clickable = new ClickableWidget { SoundName = "Audio/UI/ButtonClick" };
        slot.Children.Add(clickable);
        return slot;
    }

    /// <summary>
    /// 配料格 <paramref name="index"/> 对应**哪个物品**（悬停显示名字、点击跳转都用它）。
    /// 返回 0 表示这一格是空的。
    ///
    /// 两处都不能想当然：
    ///
    /// **一、方块索引要看当前画面。** 一个 craftingId 对应多个方块时，原版
    /// <c>CraftingRecipeSlotWidget</c> 会按时间轮流显示
    /// （<c>array[(int)(1.0 * Time.RealTime) % array.Length]</c>），
    /// 所以只能以画面上那个为准，否则玩家点到的和看到的不是一个东西。
    ///
    /// **二、data 位要看配料串，不能抄图标的。** 原版画图标时对"没写 data"的配料
    /// 填的是 <b>4</b>：
    /// <code>
    /// m_blockIconWidget.Value = Terrain.MakeBlockValue(block.BlockIndex, 0, data.HasValue ? data.Value : 4);
    /// </code>
    /// 直接拿这个值去查配方就会查空——实机表现是"从目录点木棒有配方（值 23），
    /// 从配方里点木棒却说没有（值 65559 = 木棒:4）"，木板、石板同理。
    /// 配料没指定 data 就用 0，指定了就照抄。
    ///
    /// **三、空格子要认出来。** <c>SetIngredient(null)</c> **不会清掉图标的 Value**，
    /// 它只是把 <c>IsVisible</c> 关掉（见原版 MeasureOverride）。
    /// 不看 IsVisible 的话，跳到一个没有配方的物品之后，
    /// 鼠标移到空格上还会显示上一条配方的材料名。
    /// </summary>
    private int IngredientValue(int index)
    {
        if (index < 0 || index >= m_recipeSlots.Count)
        {
            return 0;
        }

        if (m_recipeSlots[index].m_blockIconWidget is not { IsVisible: true } icon)
        {
            return 0;
        }

        int contents = Terrain.ExtractContents(icon.Value);
        if (contents <= 0)
        {
            return 0;
        }

        string? ingredient = m_currentRecipe?.Ingredients[index];
        int? data = string.IsNullOrEmpty(ingredient)
            ? null
            : Shared.Crafting.CraftIngredient.Parse(ingredient).Data;

        return Terrain.MakeBlockValue(contents, 0, data ?? 0);
    }

    /// <summary>成品格画的是哪个物品。它是 <c>SetResult</c> 直接给的真实值，不用修 data。</summary>
    private int ResultValue()
    {
        if (m_resultSlot.m_blockIconWidget is not { IsVisible: true } icon)
        {
            return 0;
        }

        return icon.Value == 0 ? 0 : Terrain.ReplaceLight(icon.Value, 0);
    }

    // ────────────────────────────── 数据刷新 ──────────────────────────────

    private void RefreshView()
    {
        m_viewDirty = false;
        m_view.Clear();

        NetworkSearch.Query query = NetworkSearch.Parse(m_lastQuery);

        // 走预先算好的元数据，别每敲一个字就重算一千多个显示名。
        foreach (StashRecipeIndex.SearchableItem item in StashRecipeIndex.Searchable)
        {
            if (query.Matches(item.Name, item.Category, item.English))
            {
                m_view.Add(item.Value);
            }
        }

        int pages = MathUtils.Max(1, (m_view.Count + m_cells.Count - 1) / m_cells.Count);
        m_page = MathUtils.Clamp(m_page, 0, pages - 1);

        for (int i = 0; i < m_cells.Count; i++)
        {
            int index = m_page * m_cells.Count + i;
            m_cells[i].SetValue(index < m_view.Count ? m_view[index] : 0);
            m_cells[i].SetSelected(m_cells[i].Value != 0 && m_cells[i].Value == m_selected);
        }

        m_statusLabel.Text = StashText.BrowserStatus(
            m_view.Count, StashRecipeIndex.AllItems.Count, m_page + 1, pages);

        RefreshBookmarks();
    }

    private void RefreshBookmarks()
    {
        List<int> bookmarks = StashStore.Data.GetOrCreate(CurrentPlayerKey()).Bookmarks;

        for (int i = 0; i < m_bookmarkCells.Count; i++)
        {
            m_bookmarkCells[i].SetValue(i < bookmarks.Count ? bookmarks[i] : 0);
            m_bookmarkCells[i].SetSelected(
                m_bookmarkCells[i].Value != 0 && m_bookmarkCells[i].Value == m_selected);
        }
    }

    private void RefreshRecipe()
    {
        m_recipes.Clear();

        if (m_selected != 0)
        {
            m_recipes.AddRange(StashRecipeIndex.RecipesFor(m_selected));
        }

        m_recipeIndex = MathUtils.Clamp(m_recipeIndex, 0, MathUtils.Max(0, m_recipes.Count - 1));

        CraftingRecipe? recipe = m_recipes.Count > 0 ? m_recipes[m_recipeIndex] : null;
        m_currentRecipe = recipe;

        // **标题永远是"你点的那个物品"的名字**，不是配方结果的名字。
        // 一度写成 DisplayName(recipe.ResultValue)，配上按方块索引的模糊匹配，
        // 实机表现是"所有煮熟的蛋都叫煮熟的海鸥蛋"。没有配方时也要把名字显示出来。
        m_recipeName.Text = m_selected == 0 ? StashText.PickAnItem : DisplayName(m_selected);

        bool isSmelting = recipe != null && recipe.RequiredHeatLevel > 0f;
        HashSet<int> shortCells = recipe != null && m_sources.Count > 0
            ? StashCraftFill.FindShortCells(recipe, m_sources)
            : new HashSet<int>();

        for (int i = 0; i < m_recipeSlots.Count; i++)
        {
            string? ingredient = recipe?.Ingredients[i];
            m_recipeSlots[i].SetIngredient(ingredient);

            // 缺的材料标红底，玩家不用点"填材料"就知道差在哪。
            bool isShort = !string.IsNullOrEmpty(ingredient) && shortCells.Contains(i);
            m_recipeCellBacks[i].CenterColor = isShort ? ShortCellColor : new Color(0, 0, 0, 0);
        }

        m_smeltFire.IsVisible = isSmelting;
        m_smeltFire.ParticlesPerSecond = isSmelting ? 20f : 0f;
        m_smeltLabel.IsVisible = isSmelting;

        if (recipe != null)
        {
            m_resultSlot.SetResult(recipe.ResultValue, recipe.ResultCount);
            m_recipeCounter.Text = m_recipes.Count > 1
                ? StashText.RecipeCounter(m_recipeIndex + 1, m_recipes.Count)
                : string.Empty;
            m_recipeHint.Text = shortCells.Count > 0 ? StashText.SomeIngredientsMissing : string.Empty;
        }
        else
        {
            m_resultSlot.SetResult(0, 0);
            m_recipeCounter.Text = string.Empty;
            m_recipeHint.Text = m_selected == 0 ? string.Empty : StashText.NoRecipe;
        }

        m_prevRecipeButton.IsEnabled = m_recipeIndex > 0;
        m_nextRecipeButton.IsEnabled = m_recipeIndex < m_recipes.Count - 1;

        bool bookmarked = m_selected != 0
            && StashStore.Data.GetOrCreate(CurrentPlayerKey()).Bookmarks.Contains(m_selected);
        m_bookmarkButton.Text = bookmarked ? StashText.RemoveBookmark : StashText.AddBookmark;
        m_bookmarkButton.IsEnabled = m_selected != 0;

        // 深度写在按钮上，玩家才知道自己钻了几层、还要按几次。
        m_backButton.Text = m_history.Count > 1 ? StashText.BackDepth(m_history.Count) : StashText.Back;
        m_backButton.IsEnabled = m_history.Count > 0;

        // 格子塞不下 / 配方类型不对时按钮直接置灰，别让玩家点了才知道不行。
        // 熔炉界面开的浏览器要的正好相反：只有冶炼配方能填。
        m_fillButton.IsEnabled = recipe != null
            && m_target != null
            && m_target.Accepts(recipe)
            && Shared.Crafting.RecipeShape.Fits(recipe.Ingredients, m_target.Columns, m_target.Rows);
    }

    private static string DisplayName(int value)
    {
        try
        {
            return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetDisplayName(null!, value) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string CurrentPlayerKey() =>
        StashPlatform.IsReady ? StashPlatform.Current.CurrentPlayerKey : string.Empty;

    // ────────────────────────────── 每帧 ──────────────────────────────

    public override void Update()
    {
        HandleSearchBox();

        if (m_closeButton.IsClicked)
        {
            StashOverlayHost.Close(m_gui);
            return;
        }

        if (m_viewDirty)
        {
            RefreshView();
        }

        HandlePaging();
        HandleSelection();
        HandleIngredientClicks();
        HandleRecipeButtons();
        HandleFillButton();
        UpdateTooltip();
    }

    /// <summary>
    /// 点配方里的某一样材料 → 跳到它自己的配方。
    /// 跳之前把当前位置压栈，"返回"就能一层层退回来。
    /// </summary>
    private void HandleIngredientClicks()
    {
        for (int i = 0; i < m_recipeCellClicks.Count; i++)
        {
            if (!m_recipeCellClicks[i].IsClicked)
            {
                continue;
            }

            int value = IngredientValue(i);
            if (value == 0 || value == m_selected)
            {
                continue;
            }

            PushHistory();
            Select(value);
            return;
        }
    }

    /// <summary>
    /// 鼠标停在配料格上时显示物品名。触屏没有 <c>MousePosition</c>（是 null），
    /// 那边靠"点一下直接跳过去、标题就是名字"来达到同样目的，不另做长按。
    /// </summary>
    private void UpdateTooltip()
    {
        Vector2? mouse = Input.MousePosition;
        if (!mouse.HasValue)
        {
            m_tooltip.IsVisible = false;
            return;
        }

        string name = string.Empty;
        for (int i = 0; i < m_recipeSlots.Count; i++)
        {
            if (m_recipeSlots[i].HitTest(mouse.Value))
            {
                int value = IngredientValue(i);
                if (value != 0)
                {
                    name = DisplayName(value);
                }

                break;
            }
        }

        if (string.IsNullOrEmpty(name) && m_resultSlot.HitTest(mouse.Value))
        {
            int value = ResultValue();
            if (value != 0)
            {
                name = DisplayName(value);
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            m_tooltip.IsVisible = false;
            return;
        }

        m_tooltip.Text = name;
        m_tooltip.IsVisible = true;

        // 贴着光标右上角，再夹回面板里，免得跑到屏幕外面看不见。
        Vector2 local = ScreenToWidget(mouse.Value);
        Vector2 size = m_tooltip.ActualSize;
        float x = MathUtils.Clamp(local.X + 14f, 0f, MathUtils.Max(Size.X - size.X, 0f));
        float y = MathUtils.Clamp(local.Y - size.Y - 4f, 0f, MathUtils.Max(Size.Y - size.Y, 0f));
        SetWidgetPosition(m_tooltip, new Vector2(x, y));
    }

    private void PushHistory()
    {
        if (m_selected == 0)
        {
            return;
        }

        m_history.Add((m_selected, m_recipeIndex));
        while (m_history.Count > MaxHistory)
        {
            m_history.RemoveAt(0);
        }
    }

    private void GoBack()
    {
        if (m_history.Count == 0)
        {
            return;
        }

        (int value, int recipeIndex) = m_history[^1];
        m_history.RemoveAt(m_history.Count - 1);

        Select(value);

        // 回到原来看的是第几条配方，而不是每次都跳回第 1 条。
        m_recipeIndex = MathUtils.Clamp(recipeIndex, 0, MathUtils.Max(0, m_recipes.Count - 1));
        RefreshRecipe();
    }

    /// <summary>
    /// 搜索框的进出。和终端那边一样的三条退路（Esc / 回车 / 点别处），
    /// 而且必须把 <see cref="StashHotkeys.TypingInProgress"/> 打上，
    /// 否则打字时按到 WASD 会连人物一起走（终端那边踩过这个坑）。
    /// </summary>
    private void HandleSearchBox()
    {
        bool wasFocused = m_searchBox.HasFocus;

        if (wasFocused)
        {
            if (Input.IsKeyDownOnce(Key.Escape))
            {
                m_searchBox.Text = string.Empty;
                m_searchBox.HasFocus = false;

                // 吃掉这一下，否则同一个 Esc 会顺手把浮层也关了。
                Input.Back = false;
                Input.Cancel = false;
            }
            else if (Input.IsKeyDownOnce(Key.Enter))
            {
                m_searchBox.HasFocus = false;
            }
        }
        else if (Input.Back || Input.Cancel)
        {
            // 没在打字时 Esc / 返回 = 关掉浮层。
            // 一定要吃掉这一下：ComponentGui 自己也在看 Back，
            // 不吃的话同一次按键会连底下的箱子/工作台界面一起关了。
            Input.Back = false;
            Input.Cancel = false;
            StashOverlayHost.Close(m_gui);
            return;
        }

        StashHotkeys.TypingInProgress = wasFocused;

        m_searchHint.IsVisible = string.IsNullOrEmpty(m_searchBox.Text);
        m_searchFrame.CenterColor = m_searchBox.HasFocus
            ? new Color(20, 60, 70, 160)
            : new Color(0, 0, 0, 80);

        string query = m_searchBox.Text ?? string.Empty;
        if (query != m_lastQuery)
        {
            m_lastQuery = query;
            m_page = 0;
            m_viewDirty = true;
        }
    }

    private void HandlePaging()
    {
        if (m_pageUpButton.IsClicked && m_page > 0)
        {
            m_page--;
            RefreshView();
        }

        if (m_pageDownButton.IsClicked && (m_page + 1) * m_cells.Count < m_view.Count)
        {
            m_page++;
            RefreshView();
        }
    }

    private void HandleSelection()
    {
        foreach (StashItemButton cell in m_cells)
        {
            if (cell.IsClicked)
            {
                // 从目录里挑一个 = 重新起头，之前钻进去的那条路就没意义了。
                m_history.Clear();
                Select(cell.Value);
                return;
            }
        }

        foreach (StashItemButton cell in m_bookmarkCells)
        {
            if (cell.IsClicked)
            {
                m_history.Clear();
                Select(cell.Value);
                return;
            }
        }
    }

    private void Select(int value)
    {
        m_selected = value;
        m_recipeIndex = 0;

        foreach (StashItemButton cell in m_cells)
        {
            cell.SetSelected(cell.Value != 0 && cell.Value == m_selected);
        }

        RefreshBookmarks();
        RefreshRecipe();

        // "点了没配方" 和 "点了没反应" 在画面上长得一模一样，日志里分得清。
        Log.Information($"[Stash] 配方浏览器：选中 {DisplayName(value)}（值 {value}），"
            + $"找到 {m_recipes.Count} 条配方");
    }

    private void HandleRecipeButtons()
    {
        if (m_prevRecipeButton.IsClicked && m_recipeIndex > 0)
        {
            m_recipeIndex--;
            RefreshRecipe();
        }

        if (m_nextRecipeButton.IsClicked && m_recipeIndex < m_recipes.Count - 1)
        {
            m_recipeIndex++;
            RefreshRecipe();
        }

        if (m_backButton.IsClicked)
        {
            GoBack();
            return;
        }

        if (m_bookmarkButton.IsClicked && m_selected != 0)
        {
            StashStore.Data.GetOrCreate(CurrentPlayerKey()).ToggleBookmark(m_selected);
            StashStore.Save();
            RefreshBookmarks();
            RefreshRecipe();
        }
    }

    private void HandleFillButton()
    {
        if (!m_fillButton.IsClicked || m_target == null || m_recipes.Count == 0)
        {
            return;
        }

        StashFillResult result = StashCraftFill.Fill(
            m_recipes[m_recipeIndex], m_target, m_sources, fillMax: false);

        m_gui.DisplaySmallMessage(result.Message, Color.White, blinking: false, playNotificationSound: false);

        if (result.Ok)
        {
            // 填完就把浮层收起来，玩家正好看见合成格里摆好的材料和结果。
            StashOverlayHost.Close(m_gui);
        }
    }
}
