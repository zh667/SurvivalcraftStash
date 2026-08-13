<?xml version="1.0" encoding="utf-8"?>
<!--
  行囊的三条合成配方。**两个平台各一份**，因为衣物索引不一样
  （联机版 38/39/40，插件版 160/161/162），理由见同目录的 StashClothes.clo。

  拆成单独的 .cr 是可以的：两版都是按扩展名扫目录（GetFiles(".cr", …)），
  一个 Mod 里放几个 .cr 都会被加载。
-->
<Mod xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <!-- 行囊（穿在躯干最外层）。结果写成 ClothingBlock:<data>，data 低 8 位就是衣物索引 -->
  <!-- 行囊三档，和箱子一样按铜/铁/钻石分级；升级直接用配方（行囊穿在身上，没法拿升级件去点） -->
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

</Mod>
