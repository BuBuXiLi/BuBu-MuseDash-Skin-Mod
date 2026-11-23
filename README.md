# BuBu 喵斯快跑换肤模组 / Muse Dash Skin Mod

**BuBu 1.6.0** · 布布西里 / BuBuXiLi

## 简体中文

BuBu 为《喵斯快跑》提供五套内置预设，并支持为原版角色安装玩家制作的皮肤。大厅右上角的「角色换肤」菜单支持按角色选择原版、预设或自定义皮肤。

- 安装时将 `bubu.dll` 放入 `Mods`。已验证环境为 Windows Steam 版《喵斯快跑》6.8.0、MelonLoader 0.7.3 和 .NET 6。
- 自定义皮肤使用 `Custom Skin/皮肤目录/skin.json` 和同目录的 PNG。启动游戏后选择皮肤并点击「应用」。更新 DLL 不会覆盖这个目录。
- 内置资源包含 14 张预设 PNG。首次升级时，若 DLL 同目录有旧版 `bubu.pak`，BuBu 会读取该文件并自动迁移玩家改过的图集。原 PAK 保留。
- 换肤只在运行时替换 Unity 材质中的贴图，沿用原角色、骨架、动画和玩法。

## English

BuBu is a skin mod for Muse Dash. It includes five built-in presets and supports community-made skins for existing characters. Open **Character Skins** from the lobby to choose the original look, a preset, or a custom skin for each character.

- Place `bubu.dll` in `Mods`. The verified setup is Muse Dash 6.8.0 on Windows Steam, MelonLoader 0.7.3, and .NET 6.
- Put each custom skin in its own folder under `Custom Skin`, with a `skin.json` file and its PNG atlases. Select the skin after starting the game and apply it. DLL updates leave the custom skin folder intact.
- The DLL embeds 14 preset PNGs. During an upgrade, BuBu checks for the exact file `bubu.pak` alongside the DLL and imports modified textures. The original PAK is retained.
- The mod swaps Unity material textures at runtime and keeps the original characters, rigs, animations, and gameplay.
