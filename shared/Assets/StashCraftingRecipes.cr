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

  <!-- 升级件：钻石→观景（玻璃换面板，看得见里面） -->
  <Recipe Result="StashChestUpgradeBlock:3" ResultCount="1" RequiredHeatLevel="0" a="glass" b="diamond" Description="[0]">
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

  <Recipe Result="StashViewChestBlock" ResultCount="1" RequiredHeatLevel="0" a="glass" b="stashdiamondchest" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>


  <!-- 抽屉：木箱 + 一圈材料；档位越高容量越大 -->
  <Recipe Result="StashWoodDrawerBlock" ResultCount="1" RequiredHeatLevel="0" a="planks" b="chest" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashCopperDrawerBlock" ResultCount="1" RequiredHeatLevel="0" a="copperingot" b="stashwooddrawer" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashIronDrawerBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="stashcopperdrawer" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>

  <Recipe Result="StashDiamondDrawerBlock" ResultCount="1" RequiredHeatLevel="0" a="diamond" b="stashirondrawer" Description="[0]">
    "aaa"
    "aba"
    "aaa"
  </Recipe>


  <!-- 背包（穿在躯干最外层）。结果写成 ClothingBlock:<data>，data 低 8 位就是衣物索引 -->
  <Recipe Result="ClothingBlock:100" ResultCount="1" RequiredHeatLevel="0" a="cotton" b="string" Description="[0]">
    "a a"
    "aaa"
    "aba"
  </Recipe>

  <Recipe Result="ClothingBlock:101" ResultCount="1" RequiredHeatLevel="0" a="leather" b="string" Description="[0]">
    "a a"
    "aaa"
    "aba"
  </Recipe>

  <Recipe Result="ClothingBlock:102" ResultCount="1" RequiredHeatLevel="0" a="leather" b="ironingot" Description="[0]">
    "a a"
    "aaa"
    "aba"
  </Recipe>


  <!-- 存储终端：贴着它的容器连成一片，点它统一检索取放 -->
  <Recipe Result="StashHubBlock" ResultCount="1" RequiredHeatLevel="0" a="ironingot" b="chest" c="diamond" Description="[0]">
    "aca"
    "aba"
    "aaa"
  </Recipe>

</Mod>
