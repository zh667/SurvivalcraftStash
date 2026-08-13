using Engine;
using GameEntitySystem;
using TemplatesDatabase;
#if !STASH_SCMOD
using Game.NetWork;
using Game.NetWork.Packages;
#endif

namespace Game;

/// <summary>
/// 分级熔炉的组件。只干一件事：**把时间按倍数放快**。
///
/// 冶炼进度和燃料消耗按同一个倍数加快，所以"每件东西耗多少燃料"不变，只是等的时间短了。
/// 这正是"等级越高烧得越快"要的效果，不会顺带变成燃料作弊。
///
/// ─────────────────────────────────────────────────────────────────────────
/// **两个平台的 <c>ComponentFurnace</c> 差别很大，这里必须分开写。**
/// 下面两条都是从各自程序集里实测出来的（联机版看反编译源码，插件版用 ilspycmd 反编译）：
///
/// 插件版（SCAPI）：<c>Update</c> 是 <c>virtual</c>，而且**没有方块替换那段逻辑**——
/// 它把炉火改成了组件自己持有的 <c>FireParticleSystem</c>。所以直接 override、
/// 把放大的 dt 交给 base 就行，一切照旧。
///
/// 联机版：**不能调 <c>base.Update</c>**。原版那份 Update 末尾有这么一段：
/// <code>
/// if (m_heatLevel > 0f) { if (num3 != 65) ChangeCell(..., ReplaceContents(cellValue, 65)); }
/// else if (num3 != 64)  { ChangeCell(..., ReplaceContents(cellValue, 64)); }
/// </code>
/// 64 / 65 是**原版熔炉和点燃熔炉的方块索引，写死的**。
/// 它会无条件把这个格子改成原版熔炉——也就是说我们的钻石熔炉放下去第一帧就变回普通熔炉，
/// 而且 ChangeCell 会触发我们自己的 <c>OnBlockRemoved</c>，把炉里的东西撒一地、连方块实体一起删掉。
/// 所以联机版这边把原版 Update 抄了一份，**只删掉方块替换那一段**，其余逐行照旧。
/// 联机版的字段全是 public，抄得动；插件版反而抄不动（状态藏在属性和虚方法后面），
/// 两边正好各走各的最省路子。
///
/// 代价：联机版的分级熔炉**不会有原版那种"点燃时冒火"的粒子**——那个效果是
/// <c>SubsystemFurnaceBlockBehavior</c> 认方块索引 65 才加的。我们的正面贴图本来就画着火，
/// 视觉上不至于看不出在烧。插件版没这个问题（粒子在组件里）。
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
public class ComponentStashFurnace : ComponentFurnace
#if !STASH_SCMOD
    , IUpdateable
#endif
{
    /// <summary>时间倍率。1 = 原版速度，来自实体模板参数，每档一份模板。</summary>
    public float SpeedMultiplier { get; private set; } = 1f;

    /// <summary>
    /// 冶炼计时用：这一件从进度 0 开始的时刻。
    ///
    /// 存在的理由——"高等级熔炉是不是真的快 N 倍"是这个功能唯一的验收标准，
    /// 而玩家掐表既麻烦又不准（还要减掉开界面的时间）。让炉子自己报时间最省事：
    /// 每烧完一件打一行 `实测 x.x 秒 / 期望 y.y 秒`，对不对一眼就知道。
    /// </summary>
    private double m_smeltStartTime = -1.0;

    /// <summary>档位名，只为日志好看。</summary>
    private string m_tierName = "?";

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);

        // 取到 0 或负数会让熔炉永远烧不完，兜一下底。
        float multiplier = valuesDictionary.GetValue("SpeedMultiplier", 1f);
        SpeedMultiplier = multiplier > 0f ? multiplier : 1f;

        m_tierName = valuesDictionary.GetValue("TierName", $"×{SpeedMultiplier:0.#}");

        // 每种档位只报一次，避免世界里放了 20 个炉子就刷 20 行。
        Stash.Game.StashDiag.Once(
            $"furnace-load-{SpeedMultiplier}",
            $"分级熔炉就位：{m_tierName} 倍率 ×{SpeedMultiplier:0.#}，格子数 {SlotsCount}"
#if STASH_SCMOD
            + "，走 override base.Update 路径（插件版）");
#else
            + $"，走自绘 Update 路径（联机版），WorkType={CommonLib.WorkType}");
#endif
    }

    /// <summary>
    /// 烧完一件时报一次账。<paramref name="dtScaled"/> 是已经乘过倍率的 dt。
    ///
    /// 原版进度是每秒 0.15，也就是**一件 6.67 秒**（乘以配方本身不影响进度速度）。
    /// 期望耗时 = 6.67 / 倍率。实测和期望差得多，就是倍率没生效或者加错了地方。
    /// </summary>
    private void ReportSmelt()
    {
        if (m_smeltStartTime < 0.0)
        {
            return;
        }

        double elapsed = Engine.Time.RealTime - m_smeltStartTime;
        m_smeltStartTime = -1.0;

        // **这是全套诊断里唯一随游戏时间不断发生的事件**，必须限流。
        // 钻石炉 0.8 秒一件，一台每小时就是 630 KB；玩家真跑起一排炉子，
        // 联机版 10 MB 的日志上限两小时就满——而它满了是直接清空不留备份，
        // 会把玩家真正要报的那个 bug 的现场一起冲掉。
        // 每档取 3 条样本足够判断倍率对不对了。
        const int Samples = 3;
        string key = $"smelt-{SpeedMultiplier:0.#}";
        if (!Stash.Game.StashDiag.Sample(key, Samples))
        {
            return;
        }

        const double VanillaSeconds = 1.0 / 0.15;
        double expected = VanillaSeconds / MathUtils.Max(SpeedMultiplier, 0.0001f);

        Stash.Game.StashDiag.Log(
            $"熔炉烧完一件：{m_tierName}（×{SpeedMultiplier:0.#}）实测 {elapsed:0.0} 秒 / 期望 {expected:0.0} 秒"
            + $"，产物 {Stash.Game.StashDiag.Name(m_slots[ResultSlotIndex].Value)}"
            + $"×{m_slots[ResultSlotIndex].Count}，燃料剩 {m_slots[FuelSlotIndex].Count}"
            + (Stash.Game.StashDiag.JustExhausted(key, Samples)
                ? $"（这一档已取满 {Samples} 条样本，后面不再打印）"
                : string.Empty));
    }

#if STASH_SCMOD
    /// <summary>
    /// 插件版：把放大的 dt 交给原版即可。计时靠**观察状态变化**——
    /// 进度从 0 变正 = 开始烧，产物格数量变多 = 烧完一件。
    /// 不用进度归零来判完成：换配方也会让进度归零，会误报。
    /// </summary>
    public override void Update(float dt)
    {
        float progressBefore = m_smeltingProgress;
        int resultBefore = m_slots[ResultSlotIndex].Count;

        base.Update(dt * SpeedMultiplier);

        if (progressBefore <= 0f && m_smeltingProgress > 0f)
        {
            m_smeltStartTime = Engine.Time.RealTime;
        }

        if (m_slots[ResultSlotIndex].Count > resultBefore)
        {
            ReportSmelt();
        }
    }
#else
    /// <summary>原版每秒推进 0.15 的冶炼进度，写死在 Update 里，没有可调参数。</summary>
    private const float SmeltSpeed = 0.15f;

    /// <summary>上一帧是否在烧。由烧 → 不烧的那一下要给客户端补一个同步包（原版就是这么做的）。</summary>
    private bool m_wasSmelting;

    /// <summary>
    /// 显式实现，覆盖派生类的接口映射。不能写成 <c>public new void Update</c>——
    /// 那样 <c>SubsystemUpdate</c> 通过接口调用时（SubsystemUpdate.cs:123
    /// <c>sortedUpdateable.Update(dt)</c>）仍然会走到基类的实现，倍率和上面那个修复就都没了。
    /// </summary>
    void IUpdateable.Update(float dt)
    {
        dt *= SpeedMultiplier;

        if (CommonLib.WorkType == WorkType.Client)
        {
            // 客户端只跑观感：真实进度由服务端用 ComponentFurnacePackage 同步过来。
            if (m_heatLevel > 0f && m_smeltingProgress > 0f && m_smeltingProgress < 1f)
            {
                m_smeltingProgress = MathUtils.Min(m_smeltingProgress + SmeltSpeed * dt, 1f);
                m_fireTimeRemaining = MathUtils.Max(0f, m_fireTimeRemaining - dt);
                if (m_fireTimeRemaining <= 0f)
                {
                    m_heatLevel = 0f;
                }
            }

            return;
        }

        Point3 coordinates = m_componentBlockEntity.Coordinates;

        if (m_heatLevel > 0f)
        {
            m_fireTimeRemaining = MathUtils.Max(0f, m_fireTimeRemaining - dt);
            if (m_fireTimeRemaining == 0f)
            {
                m_heatLevel = 0f;
            }
        }

        if (m_updateSmeltingRecipe)
        {
            m_updateSmeltingRecipe = false;

            float heatLevel = 0f;
            if (m_heatLevel > 0f)
            {
                heatLevel = m_heatLevel;
            }
            else if (m_slots[FuelSlotIndex].Count > 0)
            {
                heatLevel = BlocksManager.Blocks[Terrain.ExtractContents(m_slots[FuelSlotIndex].Value)].FuelHeatLevel;
            }

            CraftingRecipe recipe = FindSmeltingRecipe(heatLevel);
            if (recipe != m_smeltingRecipe)
            {
                m_smeltingRecipe = recipe != null && recipe.ResultValue != 0 ? recipe : null;
                m_smeltingProgress = 0f;
            }
        }

        if (m_smeltingRecipe == null)
        {
            m_heatLevel = 0f;
            m_fireTimeRemaining = 0f;
        }

        if (m_smeltingRecipe != null && m_fireTimeRemaining <= 0f)
        {
            Slot fuel = m_slots[FuelSlotIndex];
            if (fuel.Count > 0)
            {
                Block block = BlocksManager.Blocks[Terrain.ExtractContents(fuel.Value)];
                if (block.GetExplosionPressure(fuel.Value) > 0f)
                {
                    // 往炉子里塞炸药：原版会当场炸掉，这里照旧。
                    fuel.Count = 0;
                    m_subsystemExplosions.TryExplodeBlock(
                        coordinates.X, coordinates.Y, coordinates.Z, fuel.Value, m_componentBlockEntity.OwnPlayerData);
                }
                else if (block.FuelHeatLevel > 0f)
                {
                    fuel.Count--;
                    m_fireTimeRemaining = block.FuelFireDuration;
                    m_heatLevel = block.FuelHeatLevel;
                }

                OnSlotChange(FuelSlotIndex);
            }
        }

        if (m_fireTimeRemaining <= 0f)
        {
            m_smeltingRecipe = null;
            m_smeltingProgress = 0f;
        }

        if (m_smeltingRecipe != null)
        {
            if (m_smeltingProgress <= 0f)
            {
                m_smeltStartTime = Engine.Time.RealTime;
            }

            m_smeltingProgress = MathUtils.Min(m_smeltingProgress + SmeltSpeed * dt, 1f);
            m_wasSmelting = true;

            if (m_smeltingProgress >= 1f)
            {
                for (int i = 0; i < m_furnaceSize; i++)
                {
                    if (m_slots[i].Count > 0)
                    {
                        m_slots[i].Count--;
                        OnSlotChange(i);
                    }
                }

                m_slots[ResultSlotIndex].Value = m_smeltingRecipe.ResultValue;
                m_slots[ResultSlotIndex].Count += m_smeltingRecipe.ResultCount;

                if (m_smeltingRecipe.RemainsValue != 0 && m_smeltingRecipe.RemainsCount > 0)
                {
                    m_slots[RemainsSlotIndex].Value = m_smeltingRecipe.RemainsValue;
                    m_slots[RemainsSlotIndex].Count += m_smeltingRecipe.RemainsCount;
                    OnSlotChange(RemainsSlotIndex);
                }

                OnSlotChange(ResultSlotIndex);
                ReportSmelt();

                // 烧完必须把配方清空并请求重算，否则会拿着旧配方一直烧下去。
                m_smeltingRecipe = null;
                m_smeltingProgress = 0f;
                m_updateSmeltingRecipe = true;
            }
        }

        // ★ 原版在这里把方块强制改成 64 / 65。**故意不抄**，理由见类注释。

        if (m_wasSmelting && m_smeltingRecipe == null && m_fireTimeRemaining == 0f)
        {
            m_wasSmelting = false;
            CommonLib.Net.QueuePackage(new ComponentFurnacePackage(
                Entity.EntityId, m_fireTimeRemaining, m_smeltingProgress, m_heatLevel));
        }
    }
#endif
}
