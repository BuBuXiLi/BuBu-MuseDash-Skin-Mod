# BuBu 1.6.0

BuBu 是《喵斯快跑》的角色外观模组。五套预设内置在 DLL 中，玩家可以另外安装自定义皮肤。换肤沿用游戏的原版角色、骨架和动画。

| 项目 | 信息 |
| --- | --- |
| 作者 | 布布西里（BuBuXiLi） |
| 安装文件 | `Mods/bubu.dll` |
| 目标环境 | Windows Steam 版《喵斯快跑》6.8.0、MelonLoader 0.7.3、.NET 6 |
| 玩家皮肤 | 游戏根目录的 `Custom Skin` |
| 游戏内设置 | `UserData/MelonPreferences.cfg` 的 `BubuSkins` 分组 |

## 安装与升级

1. 退出游戏，把压缩包里的 `Mods/bubu.dll` 放进游戏的 `Mods` 目录，替换旧 DLL。
2. 若旧版有 `Mods/bubu.pak`，保留它并打开一次游戏。BuBu 会在这次启动中导入旧 PAK 中修改过的贴图，并继续沿用旧版五个开关。原 PAK 会保留，确认导入后可自行备份移出。
3. 在大厅右上角打开「角色换肤」。点击角色当前皮肤进入选择页，选择原版、预设或自定义皮肤，再点「应用」。关闭换肤开关会还原原版，重新开启会恢复上次选的皮肤。

五套官方预设为尾鸟绽（NEKO，含 Cheems）、Teto（夜勤血裔布若）、白毛凛（怪盗凛）、黑白梦游（梦游少女）和玉米阿米娅。五套预设所在的角色默认开启。对从未手动选择的角色，只有一套自定义皮肤时会自动使用它。有多套时，可在菜单中选择；如果该角色有预设，自动状态会使用预设。旧 PAK 中可识别的改图会作为升级时的初始选择。已保存的选择和原版开关会跨重启保留。

## 安装自定义皮肤

将含有 `skin.json` 和 PNG 的皮肤目录放入游戏根目录的 `Custom Skin`，例如 `Custom Skin/我的白毛凛/skin.json`。该目录在首次启动时会自动创建。游戏运行中安装的皮肤在重启一次后可用。日后更新 BuBu 继续替换 DLL 即可。

旧 PAK 中归属不明的图片会保存到 `Custom Skin/legacy-154-unassigned`（同名目录存在时会追加编号），并附 `original-names.json`。创作者可根据原条目名辨认图集，并按下文填写 `skin.json`。异常导入会写入 MelonLoader 日志，下次启动可以重试。

## 制作自定义皮肤

一套皮肤一个文件夹。示例中的 `my-thief.png` 与 `skin.json` 放在同一目录。

```json
{
  "id": "my-thief-skin",
  "name": "我的白毛凛",
  "character": "thief",
  "textures": {
    "char_1_thief": "my-thief.png"
  }
}
```

- `id` 是同一角色下唯一且稳定的皮肤 ID。ID 可以使用小写英文字母、数字、短横线和下划线，最多 64 个字符。`preset` 由 BuBu 保留。
- `character` 为稳定的角色 ID。同一原版角色的多个皮肤应使用相同 ID。为内置表之外的原版角色制作皮肤时，自行约定角色 ID 并增加 `"characterName": "游戏内角色名"`。
- `textures` 左侧填原版贴图名（不含 `.png`），右侧填当前目录内的 PNG 文件名。尺寸须与原图一致。多图皮肤可以继续添加映射；缺少的图集维持原版。怪盗凛的 `card_4` 与 `tail` 还会校验原版材质名。
- 已属于一个角色的贴图目标不能同时声明给另一个角色。重复皮肤 ID、无效的清单或无法读取的 PNG 会记入日志。保存的皮肤被移出后，对应角色会显示原版；放回同 ID 皮肤后可以恢复。
- 自定义皮肤涉及大厅、关卡或结算等不同场景时，需要提供各场景实际使用的图集。

| 角色 ID | 原版角色 | 内置预设使用的目标 |
| --- | --- | --- |
| `neko` | NEKO | `char_5_neko` |
| `bloodheir` | 夜勤血裔布若 | `char_2_bloodheir` |
| `thief` | 怪盗凛 | `char_1_thief`、`char_1_thief_fail`、`char_1_thief_victory_show`、`card_4`、`tail` |
| `sleepy` | 梦游少女 | `char_1_sleepy`、`char_1_sleep_fail`、`1_sleepy_main_show`、`1_sleepy_main_show_hexie`、`1_sleepy_victory`、`1_sleepy_victory_hexie` |
| `amiya` | 阿米娅 | `char_9_amiya` |

同一角色如同时使用其他换肤模组，可能发生材质覆盖冲突。旧版 HotSkin 请保持停用。

## 说明与署名

BuBu 由布布西里独立开发，完全免费，作者从未对下载收费。原游戏素材、角色形象和第三方素材的权利归各自权利人。BuBu 是非官方同人模组，仅供学习交流，请勿用于商业用途。转载时请保留原作者署名和本说明。部分图集含有 AI 生成内容。

重音 Teto 皮肤的原图集创作者为 Akrswoop，布布西里补全与完善。

[MelonLoader 官方安装与运行要求](https://github.com/LavaGang/MelonLoader#requirements)
