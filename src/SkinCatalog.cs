using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Bubu
{
    internal sealed record SkinManifest(
        string Id, string Name, string Character, string CharacterName, Dictionary<string, string> Textures);

    internal sealed record SkinTexture(string Target, byte[] EmbeddedPng, string FilePath)
    {
        internal byte[] ReadPng() => EmbeddedPng ?? File.ReadAllBytes(FilePath);
        internal string Source => FilePath ?? Target + ".png";
    }

    internal sealed record SkinOption(string Id, string Name, IReadOnlyList<SkinTexture> Textures)
    {
        internal const string PresetId = "preset";
        internal bool IsPreset => Id == PresetId;
    }

    internal sealed record SkinCharacter(
        string Id, string Name, string LegacyKey, string PresetName, string[] PresetTargets)
    {
        internal List<SkinOption> Skins { get; } = new();
        internal string ImportedId { get; set; }

        internal SkinOption Resolve(string savedId)
        {
            if (savedId.Length > 0)
                return Skins.Find(skin => skin.Id == savedId);
            if (ImportedId != null)
            {
                SkinOption imported = Skins.Find(skin => skin.Id == ImportedId);
                if (imported != null) return imported;
            }
            if (Skins.Count(skin => !skin.IsPreset) == 1)
                return Skins.Find(skin => !skin.IsPreset);
            return Skins.Find(skin => skin.IsPreset);
        }
    }

    internal sealed class SkinCatalog
    {
        internal static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        internal List<SkinCharacter> Characters { get; } = new()
        {
            new("neko", "NEKO", "NekoEnabled", "尾鸟绽", new[] { "char_5_neko" }),
            new("bloodheir", "夜勤血裔布若", "TetoEnabled", "Teto", new[] { "char_2_bloodheir" }),
            new("thief", "怪盗凛", "ThiefEnabled", "白毛凛", new[] {
                "char_1_thief", "char_1_thief_fail", "char_1_thief_victory_show", "card_4", "tail" }),
            new("sleepy", "梦游少女", "SleepyEnabled", "黑白梦游", new[] {
                "char_1_sleepy", "char_1_sleep_fail", "1_sleepy_main_show", "1_sleepy_main_show_hexie",
                "1_sleepy_victory", "1_sleepy_victory_hexie" }),
            new("amiya", "阿米娅", "AmiyaEnabled", "玉米阿米娅", new[] { "char_9_amiya" })
        };

        internal static SkinCatalog Load(string root, string oldPack, Action<string> warn)
        {
            var catalog = new SkinCatalog();
            Dictionary<string, byte[]> baseline = ReadPresetResource("Bubu.Official-1.5.4.pak", warn);
            var presets = ReadPresetResource("Bubu.Presets.pak", warn, optional: true) ?? baseline;
            foreach (SkinCharacter character in catalog.Characters)
            {
                if (presets == null || character.PresetTargets.Any(target => !presets.ContainsKey(target + ".png")))
                {
                    warn($"[Bubu] 内置预设资源缺失：{character.Name}");
                    continue;
                }
                var textures = character.PresetTargets
                    .Select(target => new SkinTexture(target, presets[target + ".png"], null)).ToArray();
                character.Skins.Add(new SkinOption(SkinOption.PresetId, character.PresetName, textures));
            }

            try
            {
                Directory.CreateDirectory(root);
                var imported = LegacySkinImport.Run(root, oldPack, baseline, catalog.Characters, warn);
                foreach (SkinCharacter character in catalog.Characters)
                    character.ImportedId = imported.TryGetValue(character.Id, out string id) ? id : null;
                catalog.LoadCustomSkins(root, warn);
            }
            catch (Exception ex)
            {
                warn($"[Bubu] 无法访问自定义皮肤目录 {root}：{ex.Message}");
            }
            return catalog;
        }

        private static Dictionary<string, byte[]> ReadPresetResource(string name, Action<string> warn, bool optional = false)
        {
            try
            {
                using Stream resource = typeof(SkinCatalog).Assembly.GetManifestResourceStream(name);
                if (resource == null && optional) return null;
                return TexturePack.Read(resource ?? throw new FileNotFoundException(name));
            }
            catch (Exception ex)
            {
                warn($"[Bubu] 无法读取内置资源 {name}：{ex.Message}");
                return null;
            }
        }

        private void LoadCustomSkins(string root, Action<string> warn)
        {
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (SkinCharacter character in Characters)
                foreach (string target in character.PresetTargets)
                    owners.Add(target, character.Id);

            foreach (string folder in Directory.EnumerateDirectories(root).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string folderName = Path.GetFileName(folder);
                if (folderName.StartsWith(".bubu-", StringComparison.OrdinalIgnoreCase)) continue;
                string manifestPath = Path.Combine(folder, "skin.json");
                if (!File.Exists(manifestPath)) continue;
                try
                {
                    var manifest = JsonSerializer.Deserialize<SkinManifest>(File.ReadAllText(manifestPath), Json);
                    if (manifest == null || !IsId(manifest.Id) || manifest.Id == SkinOption.PresetId ||
                        !IsId(manifest.Character) || string.IsNullOrWhiteSpace(manifest.Name) ||
                        manifest.Textures == null || manifest.Textures.Count == 0)
                        throw new InvalidDataException("需要有效的 id、name、character 和 textures。");
                    var character = Characters.Find(item => item.Id == manifest.Character);
                    if (character == null && string.IsNullOrWhiteSpace(manifest.CharacterName))
                        throw new InvalidDataException("新目标角色需要 characterName。");
                    if (character?.Skins.Exists(skin => skin.Id == manifest.Id) == true)
                        throw new InvalidDataException($"皮肤 ID {manifest.Id} 在角色 {manifest.Character} 下重复。");

                    var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var textures = new List<SkinTexture>();
                    foreach (var (target, file) in manifest.Textures)
                    {
                        if (string.IsNullOrWhiteSpace(target) || !targets.Add(target))
                            throw new InvalidDataException($"贴图目标为空或重复：{target}");
                        if (owners.TryGetValue(target, out string owner) && owner != manifest.Character)
                            throw new InvalidDataException($"贴图 {target} 已属于角色 {owner}。");
                        if (string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) ||
                            file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                            !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException($"贴图文件必须是同目录中的 PNG 文件名：{file}");
                        string path = Path.Combine(folder, file);
                        using (var png = File.OpenRead(path))
                        {
                            ValidatePng(png);
                        }
                        textures.Add(new SkinTexture(target, null, path));
                    }

                    if (character == null)
                    {
                        character = new SkinCharacter(manifest.Character, manifest.CharacterName, null, null, Array.Empty<string>());
                        Characters.Add(character);
                    }
                    character.Skins.Add(new SkinOption(manifest.Id, manifest.Name, textures));
                    foreach (string target in targets) owners[target] = character.Id;
                }
                catch (Exception ex)
                {
                    warn($"[Bubu] 跳过皮肤 {folderName}：{ex.Message}");
                }
            }
        }

        private static bool IsId(string value) =>
            !string.IsNullOrEmpty(value) && value.Length <= 64 &&
            value.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');

        private static void ValidatePng(Stream stream)
        {
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt64() != 0x0A1A0A0D474E5089 ||
                reader.ReadUInt32() != 0x0D000000 || reader.ReadUInt32() != 0x52444849 ||
                BinaryPrimitives.ReverseEndianness(reader.ReadUInt32()) == 0 ||
                BinaryPrimitives.ReverseEndianness(reader.ReadUInt32()) == 0)
                throw new InvalidDataException("PNG 文件头无效。");
        }
    }
}
