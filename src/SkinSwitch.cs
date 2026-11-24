using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace Bubu
{
    public partial class Main
    {
        private sealed record Binding(Material Material, Texture Original, Texture2D Replacement, string Target);

        private sealed class SkinSettings
        {
            internal SkinSettings(SkinCharacter character, MelonPreferences_Category category)
            {
                Character = character;
                Enabled = category.CreateEntry(character.LegacyKey ?? $"{character.Id}_Enabled", true, character.Name);
                Selected = category.CreateEntry($"{character.Id}_Skin", "", character.Name + " skin");
            }

            internal SkinCharacter Character { get; }
            internal MelonPreferences_Entry<bool> Enabled { get; }
            internal MelonPreferences_Entry<string> Selected { get; }
            internal bool DraftEnabled;
            internal string DraftSelection;
            internal bool Edited;
            internal SkinOption Active;
            internal SkinOption ResolveDraft() => Character.Resolve(DraftSelection ?? "");
        }

        private readonly Dictionary<int, Binding> _bindings = new();
        private readonly Dictionary<SkinOption, Dictionary<string, Texture2D>> _loadedSkins = new();
        private readonly Dictionary<string, Texture2D> _activeTextures = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _resolved = new();
        private MelonPreferences_Category _category;
        private SkinSettings[] _settings;
        private bool _menuAllowed;
        private bool _menuOpen;
        private bool _applyQueued;
        private float _nextScan;
        private float _scanInterval = 0.8f;
        private string _status = "";

        private void InitializeSkins(SkinCatalog catalog)
        {
            _category = MelonPreferences.CreateCategory("BubuSkins", "BuBu");
            _settings = catalog.Characters.Select(character => new SkinSettings(character, _category)).ToArray();
            foreach (SkinSettings settings in _settings)
            {
                if (!settings.Enabled.Value) continue;
                string saved = settings.Selected.Value ?? "";
                SkinOption skin = settings.Character.Resolve(saved);
                if (TryLoadSkin(skin))
                {
                    settings.Active = skin;
                }
                else if (saved.Length == 0)
                {
                    // A broken automatically selected skin should not disable a working preset.
                    SkinOption preset = settings.Character.Skins.Find(option => option.IsPreset);
                    if (preset != skin && TryLoadSkin(preset)) settings.Active = preset;
                }
            }
            RefreshActiveTextures();
            MelonLogger.Msg("[Bubu] 当前换肤：" + string.Join("，", _settings.Select(row =>
                $"{row.Character.Name}={row.Active?.Name ?? "原版"}")));
            LoadMenuArt();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            switch (sceneName)
            {
                case "UISystem_PC":
                    _menuAllowed = true;
                    break;
                case "Loading":
                case "GameMain":
                    _menuAllowed = false;
                    _menuOpen = false;
                    _applyQueued = false;
                    break;
            }
            _scanInterval = 0.8f;
            _nextScan = 0;
        }

        public override void OnUpdate()
        {
            if (_settings == null) return;
            if (_applyQueued)
            {
                _applyQueued = false;
                if (_menuAllowed) ApplyPreferences();
            }
            if (Time.unscaledTime < _nextScan || !Application.isFocused || Time.timeScale == 0f) return;

            ReconcileBindings();
            int changed = _activeTextures.Count > 0 ? BindLoadedMaterials() : 0;
            _scanInterval = changed > 0 ? 0.8f : Mathf.Min(2.5f, _scanInterval + 0.35f);
            _nextScan = Time.unscaledTime + _scanInterval;
        }

        private void ApplyPreferences()
        {
            SkinSettings[] edited = _settings.Where(row => row.Edited).ToArray();
            var updates = new List<(SkinSettings Row, bool WasEnabled, string OldSelection, SkinOption Active)>();
            foreach (SkinSettings row in edited)
            {
                SkinOption skin = row.DraftEnabled ? row.ResolveDraft() : null;
                if (!TryLoadSkin(skin))
                {
                    _status = "应用失败，请检查皮肤文件";
                    ReleaseUnusedTextures();
                    return;
                }
                updates.Add((row, row.Enabled.Value, row.Selected.Value, skin));
            }

            foreach (var (row, _, _, _) in updates)
            {
                row.Enabled.Value = row.DraftEnabled;
                row.Selected.Value = row.DraftSelection;
            }
            try
            {
                if (updates.Count > 0) _category.SaveToFile(false);
            }
            catch (Exception ex)
            {
                foreach (var (row, enabled, selection, _) in updates)
                {
                    row.Enabled.Value = enabled;
                    row.Selected.Value = selection;
                }
                _status = "保存失败，请检查游戏日志";
                ReleaseUnusedTextures();
                MelonLogger.Warning($"[Bubu] 保存皮肤设置失败：{ex.Message}");
                return;
            }
            foreach (var (row, _, _, active) in updates)
            {
                row.Active = active;
                row.Edited = false;
            }
            RefreshActiveTextures();
            ReconcileBindings();
            _scanInterval = 0.8f;
            _nextScan = 0;
            _status = "已应用并保存";
            if (updates.Count > 0)
                MelonLogger.Msg("[Bubu] 已保存：" + string.Join("，", updates.Select(update =>
                    $"{update.Row.Character.Name}={update.Active?.Name ?? "原版"}")));
        }

        private bool TryLoadSkin(SkinOption skin)
        {
            if (skin == null || _loadedSkins.ContainsKey(skin)) return true;

            var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            string source = "";
            try
            {
                foreach (SkinTexture entry in skin.Textures)
                {
                    source = entry.Source;
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    textures.Add(entry.Target, texture);
                    if (!ImageConversion.LoadImage(texture, entry.ReadPng(), true))
                        throw new InvalidDataException("无法解码 PNG。");
                    texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    texture.name = "__mdsrc_" + entry.Target;
                }
                _loadedSkins.Add(skin, textures);
                return true;
            }
            catch (Exception ex)
            {
                foreach (Texture2D texture in textures.Values) UnityEngine.Object.Destroy(texture);
                MelonLogger.Warning($"[Bubu] 跳过皮肤 {skin.Name} 中的文件 {source}：{ex.Message}");
                return false;
            }
        }

        private void RefreshActiveTextures()
        {
            _activeTextures.Clear();
            foreach (SkinSettings row in _settings)
                if (row.Active != null)
                    foreach (var (target, texture) in _loadedSkins[row.Active])
                        _activeTextures.Add(target, texture);
        }

        private void ReleaseUnusedTextures()
        {
            var activeSkins = new HashSet<SkinOption>(_settings.Where(row => row.Active != null).Select(row => row.Active));
            var boundTextures = new HashSet<Texture2D>(_bindings.Values.Select(binding => binding.Replacement));
            foreach (SkinOption skin in _loadedSkins.Keys.ToArray())
            {
                if (activeSkins.Contains(skin) || _loadedSkins[skin].Values.Any(boundTextures.Contains)) continue;
                foreach (Texture2D texture in _loadedSkins[skin].Values) UnityEngine.Object.Destroy(texture);
                _loadedSkins.Remove(skin);
            }
        }

        private static bool MatchesMaterial(string textureName, string materialName)
        {
            if (!textureName.Equals("tail", StringComparison.OrdinalIgnoreCase) &&
                !textureName.Equals("card_4", StringComparison.OrdinalIgnoreCase)) return true;
            return materialName.Equals(textureName, StringComparison.OrdinalIgnoreCase) ||
                   materialName.Equals(textureName + " (Instance)", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesTarget(Material material, Texture original, Texture2D replacement) =>
            original.width == replacement.width && original.height == replacement.height &&
            MatchesMaterial(original.name, material.name ?? "");

        private int BindLoadedMaterials()
        {
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>());
            if (objects == null) return 0;
            int changed = 0;
            foreach (var item in objects)
            {
                var material = item.TryCast<Material>();
                if (material == null) continue;
                try
                {
                    int id = material.GetInstanceID();
                    if (_bindings.ContainsKey(id)) continue;
                    Texture original = material.mainTexture;
                    if (original == null || string.IsNullOrEmpty(original.name) ||
                        !_activeTextures.TryGetValue(original.name, out Texture2D replacement) || replacement == null ||
                        !MatchesTarget(material, original, replacement)) continue;
                    string target = original.name;
                    material.mainTexture = replacement;
                    _bindings.Add(id, new Binding(material, original, replacement, target));
                    changed++;
                }
                catch (Exception ex) { MelonLogger.Warning($"[Bubu] 替换材质失败：{ex.Message}"); }
            }
            if (changed > 0) MelonLogger.Msg($"[Bubu] 已替换 {changed} 个材质");
            return changed;
        }

        private void ReconcileBindings()
        {
            _resolved.Clear();
            var swapped = new List<(int Id, Binding Binding)>();
            int restored = 0;
            foreach (var (id, binding) in _bindings)
            {
                try
                {
                    if (binding.Material == null || binding.Material.mainTexture != binding.Replacement)
                    {
                        _resolved.Add(id);
                        continue;
                    }
                    _activeTextures.TryGetValue(binding.Target, out Texture2D replacement);
                    if (replacement == binding.Replacement || binding.Original == null) continue;
                    if (replacement != null && MatchesTarget(binding.Material, binding.Original, replacement))
                    {
                        binding.Material.mainTexture = replacement;
                        swapped.Add((id, binding with { Replacement = replacement }));
                    }
                    else
                    {
                        binding.Material.mainTexture = binding.Original;
                        _resolved.Add(id);
                        restored++;
                    }
                }
                catch (Exception ex) { MelonLogger.Warning($"[Bubu] 更新材质失败：{ex.Message}"); }
            }
            foreach (int id in _resolved) _bindings.Remove(id);
            foreach (var (id, binding) in swapped) _bindings[id] = binding;
            if (restored > 0 || swapped.Count > 0)
                MelonLogger.Msg($"[Bubu] 已还原 {restored} 个材质，已切换 {swapped.Count} 个材质");
            if (_loadedSkins.Count > _settings.Count(row => row.Active != null))
                ReleaseUnusedTextures();
        }
    }
}
