<?xml version="1.0" encoding="utf-8"?>
<!--
  合成表。写在这里的配方会自动出现在 帮助 → 合成配方 里
  （RecipaediaScreen 按类别列方块，RecipaediaRecipesScreen 按 ResultValue 找配方，不需要额外代码）。

  材料按 SC 实际存在的矿物：铜锭（孔雀石冶炼）、铁锭、钻石。原版没有金。
-->
<Mod xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <!-- 升级件：木→铜 -->
  <Recipe Result="StashChestUpgradeBlock:0" ResultCount="1" RequiredHeatLevel="0" a="copperingot" b="planks" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 升级件：铜→铁 -->
  <Recipe Result="StashChestUpgradeBlock:1" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="planks" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 升级件：铁→钻石 -->
  <Recipe Result="StashChestUpgradeBlock:2" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="ironingot" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 直接合成各档箱子：箱子 + 一圈对应材料 -->
  <Recipe Result="StashCopperChestBlock" ResultCount="1" RequiredHeatLevel="0" a="copperingot" b="chest" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashIronChestBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="stashcopperchest" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashDiamondChestBlock" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="stashironchest" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>


  <!-- 背包（穿在躯干最外层）。结果写成 ClothingBlock:<data>，data 低 8 位就是衣物索引 -->
  <!-- 背包三档，和箱子一样按铜/铁/钻石分级；升级直接用配方（背包穿在身上，没法拿升级件去点） -->
  <Recipe Result="ClothingBlock:38" ResultCount="1" RequiredHeatLevel="0" a="leather" b="copperingot" Description="[0]">
    "a a"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="ClothingBlock:39" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="clothing:38" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="ClothingBlock:40" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="clothing:39" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>


  <!--
    分级熔炉。三档的差别只有"烧得多快"（2/4/8 倍），格子布局和原版熔炉一样。
    配料刻意和箱子那组区分开：箱子升级件中心是**木板**，熔炉升级件中心是**煤块**，
    合成表里一眼能看出这是给炉子用的。
  -->

  <!-- 熔炉升级件：原版炉→铜 -->
  <Recipe Result="StashFurnaceUpgradeBlock:0" ResultCount="1" RequiredHeatLevel="0" a="copperingot" b="coalchunk" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 熔炉升级件：铜→铁 -->
  <Recipe Result="StashFurnaceUpgradeBlock:1" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="coalchunk" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 熔炉升级件：铁→钻石 -->
  <Recipe Result="StashFurnaceUpgradeBlock:2" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="coalblock" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <!-- 直接合成各档熔炉：上一档 + 一圈对应材料 -->
  <Recipe Result="StashCopperFurnaceBlock" ResultCount="1" RequiredHeatLevel="0" a="copperingot" b="furnace" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashIronFurnaceBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="stashcopperfurnace" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashDiamondFurnaceBlock" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="stashironfurnace" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>


  <!-- 存储终端：贴着它的容器连成一片，点它统一检索取放 -->
  <Recipe Result="StashHubBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="chest" c="diamond" Description="[0]">
    "aca"
    "aba"
    "aaa"
  </Recipe>


  <!-- 无线终端：拿它右键一个存储终端就绑定，之后对着空处右键远程打开 -->
  <Recipe Result="StashWirelessTerminalBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="diamond" c="germaniumchunk" Description="[0]">
    "aca"
    "aba"
    "aaa"
  </Recipe>


  <!--
    无线合成终端：无线终端 + 工作台 + 一圈铁。

    这条静态配方**只负责"能被查到"**——合成表和配方浏览器都是按静态表枚举的。
    真正合成时走的是 StashWirelessCraftingTerminalBlock.GetAdHocCraftingRecipe：
    原版会先问每个方块要一遍临时配方，那边能读到格子里**那台终端实际的 data**，
    从而把绑定编号原样带到产物上（静态配方的 Result 是死的，做不到）。
    所以这里显示的产物是未绑定的图标，实际做出来会保留绑定。
  -->
  <Recipe Result="StashWirelessCraftingTerminalBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="stashwirelessterminal" c="craftingtable" Description="[0]">
    "aca"
    "aba"
    "aaa"
  </Recipe>

</Mod>
