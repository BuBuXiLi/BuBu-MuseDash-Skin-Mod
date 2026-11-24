using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bubu
{
    internal static class LegacySkinImport
    {
        internal static Dictionary<string, string> Run(string root, string oldPack, Dictionary<string, byte[]> baseline,
            IReadOnlyList<SkinCharacter> characters, Action<string> warn)
        {
            string receipt = Path.Combine(root, ".bubu-legacy-1.5.4.json");
            var imported = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(receipt))
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(receipt), SkinCatalog.Json)
                        ?? throw new InvalidDataException("旧版迁移记录无效。");
                if (!File.Exists(oldPack) || baseline == null) return imported;

                using var source = File.OpenRead(oldPack);
                using var sha = SHA256.Create();
                string fingerprint = Convert.ToHexString(sha.ComputeHash(source)).ToLowerInvariant();
                source.Position = 0;
                var oldEntries = TexturePack.Read(source);
                var unassigned = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool complete = true;
                foreach (SkinCharacter character in characters)
                {
                    string[] names = character.PresetTargets.Select(target => target + ".png").ToArray();
                    assigned.UnionWith(names);
                    if (!names.Any(name => IsChanged(name, oldEntries, baseline))) continue;
                    try
                    {
                        string stem = "legacy-154-" + character.Id;
                        string id = WriteImport(root, stem, fingerprint, (folder, importId) =>
                        {
                            var textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (string name in names)
                            {
                                byte[] png = oldEntries.TryGetValue(name, out byte[] supplied) ? supplied : baseline[name];
                                File.WriteAllBytes(Path.Combine(folder, name), png);
                                textures.Add(Path.GetFileNameWithoutExtension(name), name);
                            }
                            var manifest = new SkinManifest(importId, "从旧 PAK 导入", character.Id, character.Name, textures);
                            File.WriteAllText(Path.Combine(folder, "skin.json"), JsonSerializer.Serialize(manifest, SkinCatalog.Json));
                        });
                        imported.Add(character.Id, id);
                    }
                    catch (Exception ex)
                    {
                        complete = false;
                        warn($"[Bubu] 下次启动将重试导入 {character.Name}：{ex.Message}");
                    }
                }

                foreach (var (name, png) in oldEntries)
                    if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !assigned.Contains(name) &&
                        IsChanged(name, oldEntries, baseline))
                        unassigned.Add(name, png);
                if (unassigned.Count > 0)
                {
                    string folder = WriteImport(root, "legacy-154-unassigned", fingerprint, (destination, _) =>
                    {
                        var names = new Dictionary<string, string>();
                        foreach (var (name, png) in unassigned.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            string file = $"atlas-{names.Count + 1:D3}.png";
                            File.WriteAllBytes(Path.Combine(destination, file), png);
                            names.Add(file, name);
                        }
                        File.WriteAllText(Path.Combine(destination, "original-names.json"), JsonSerializer.Serialize(names, SkinCatalog.Json));
                    });
                    warn($"[Bubu] 旧 PAK 中 {unassigned.Count} 张归属不明的贴图已保存到 {Path.Combine(root, folder)}。补充 skin.json 后可加载。");
                }
                if (complete)
                {
                    string temporary = Path.Combine(root, $".bubu-receipt-{Guid.NewGuid():N}.tmp");
                    try
                    {
                        File.WriteAllText(temporary, JsonSerializer.Serialize(imported, SkinCatalog.Json));
                        File.Move(temporary, receipt);
                    }
                    finally { File.Delete(temporary); }
                }
            }
            catch (Exception ex)
            {
                warn($"[Bubu] 旧 PAK 导入未完成，下次启动将重试：{ex.Message}");
            }
            return imported;
        }

        private static bool IsChanged(string name, Dictionary<string, byte[]> oldEntries, Dictionary<string, byte[]> baseline) =>
            oldEntries.TryGetValue(name, out byte[] png) &&
            (!baseline.TryGetValue(name, out byte[] official) || !png.AsSpan().SequenceEqual(official));

        private static string WriteImport(string root, string stem, string fingerprint, Action<string, string> write)
        {
            for (int index = 0; ; index++)
            {
                string id = index == 0 ? stem : $"{stem}-{index + 1}";
                string finalPath = Path.Combine(root, id);
                string origin = Path.Combine(finalPath, ".bubu-source");
                if (File.Exists(origin) && File.ReadAllText(origin) == fingerprint)
                    return id;
                if ((Directory.Exists(finalPath) || File.Exists(finalPath))) continue;

                string staging = Path.Combine(root, $".bubu-import-{Guid.NewGuid():N}");
                Directory.CreateDirectory(staging);
                try
                {
                    write(staging, id);
                    File.WriteAllText(Path.Combine(staging, ".bubu-source"), fingerprint);
                    Directory.Move(staging, finalPath);
                    return id;
                }
                catch (IOException) when ((Directory.Exists(finalPath) || File.Exists(finalPath)))
                {
                    if (File.Exists(origin) && File.ReadAllText(origin) == fingerprint)
                        return id;
                }
                finally
                {
                    if (Directory.Exists(staging))
                    {
                        foreach (string file in Directory.EnumerateFiles(staging)) File.Delete(file);
                        Directory.Delete(staging);
                    }
                }
            }
        }
    }
}
