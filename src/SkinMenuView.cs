using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace Bubu
{
    public partial class Main
    {
        private const float PanelWidth = 540;
        private const float PanelHeight = 690;
        private const float CreditsHeight = 870;
        private const int RowsPerPage = 5;
        private const int OptionsPerPage = 7;
        private static readonly Color PanelColor = new(0.065f, 0.052f, 0.095f);
        private static readonly Color CardColor = new(0.135f, 0.105f, 0.18f);
        private static readonly Color BorderColor = new(0.33f, 0.25f, 0.38f);
        private static readonly Color TextColor = new(0.97f, 0.93f, 0.99f);
        private static readonly Color MutedColor = new(0.73f, 0.66f, 0.79f);
        private static readonly Color AccentColor = new(0.91f, 0.68f, 0.95f);

        private readonly Dictionary<string, Texture2D> _menuArt = new(StringComparer.Ordinal);
        private Font _menuFont;
        private GUIStyle _label;
        private GUIStyle _centeredLabel;
        private GUIStyle _hitArea;
        private bool _showCredits;
        private int _page;
        private int _optionPage;
        private SkinSettings _picker;
        private float _menuScale;
        private Vector2 _menuOrigin;

        private void LoadMenuArt()
        {
            foreach (string name in new[] { "entry", "entry_open", "panel", "credits" })
            {
                Texture2D texture = null;
                try
                {
                    using Stream resource = typeof(Main).Assembly.GetManifestResourceStream($"SkinMenu.{name}.png")
                        ?? throw new FileNotFoundException(name);
                    using var bytes = new MemoryStream();
                    resource.CopyTo(bytes);
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
                        throw new InvalidDataException(name);
                    texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _menuArt.Add(name, texture);
                }
                catch (Exception ex)
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                    MelonLogger.Warning($"[Bubu] 菜单图片 {name} 无法加载：{ex.Message}");
                }
            }
        }

        public override void OnGUI()
        {
            if (!_menuAllowed || _settings == null) return;
            EnsureMenuStyles();

            float top = Mathf.Max(12, Screen.height * 0.075f);
            float availableHeight = Screen.height - top - 12;
            _menuScale = Mathf.Min(1, Mathf.Min((Screen.width - 24) / PanelWidth, availableHeight / (PanelHeight + 43)));
            if (_menuScale <= 0) return;
            _menuOrigin = new Vector2(Screen.width - 18 * _menuScale - PanelWidth * _menuScale,
                top + 43 * _menuScale);

            if (DrawEntry())
            {
                _menuOpen = !_menuOpen;
                if (_menuOpen) ResetMenuDraft();
            }
            if (!_menuOpen) return;
            if (_showCredits)
            {
                DrawCredits(availableHeight);
                return;
            }

            Fill(0, 0, PanelWidth, PanelHeight, BorderColor);
            Fill(1, 1, PanelWidth - 2, PanelHeight - 2, PanelColor);
            if (_picker != null)
                DrawSkinPicker();
            else
                DrawCharacterList();
        }

        private void EnsureMenuStyles()
        {
            if (_label != null) return;
            try { _menuFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20); }
            catch (Exception ex) { MelonLogger.Warning($"[Bubu] 菜单字体不可用：{ex.Message}"); }
            _label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                richText = false
            };
            if (_menuFont != null) _label.font = _menuFont;
            _centeredLabel = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _hitArea = new GUIStyle();
        }

        private bool DrawEntry()
        {
            const int x = 284;
            const int y = -43;
            if (_menuArt.TryGetValue(_menuOpen ? "entry_open" : "entry", out Texture2D image))
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(MenuRect(x, y, 256, 34), image);
                return Hit(x, y, 256, 34);
            }
            return Button(x, y, 256, 34, _menuOpen ? "关闭换肤" : "角色换肤");
        }

        private void DrawCharacterList()
        {
            Text(25, 18, 445, 44, "角色换肤", 30, TextColor);
            if (Button(474, 24, 42, 35, "×")) { _menuOpen = false; return; }
            Text(25, 65, 490, 28, "选择皮肤，再用开关切换原版", 17, MutedColor);

            for (int i = 0; i < RowsPerPage; i++)
            {
                int index = _page * RowsPerPage + i;
                if (index >= _settings.Length) break;
                DrawCharacterRow(_settings[index], index, 106 + i * 90);
            }
            DrawPagination(_page, _settings.Length, RowsPerPage, 558, page => _page = page);
            Fill(25, 612, 490, 1, BorderColor);
            if (Button(25, 631, 119, 40, "说明与署名") && _menuArt.ContainsKey("credits"))
                _showCredits = true;
            string status = _status;
            if (status.Length == 0 && Array.Exists(_settings, row => row.Edited)) status = "有改动待应用";
            Text(153, 630, 224, 42, status, 16, MutedColor);
            if (Button(388, 631, 128, 40, "应用", true)) _applyQueued = true;
        }

        private void DrawCharacterRow(SkinSettings row, int index, float y)
        {
            Fill(25, y, 490, 82, CardColor);
            _menuArt.TryGetValue("panel", out Texture2D portraitSheet);
            bool hasPortrait = index < 5 && portraitSheet != null;
            float left = 40;
            if (hasPortrait)
            {
                left = 134;
                if (Event.current.type == EventType.Repaint)
                {
                    float cropY = 122 + index * 126;
                    Rect portrait = MenuRect(38, y + 9, 83, 64);
                    float scaleX = portrait.width / 124;
                    float scaleY = portrait.height / 96;
                    GUI.BeginGroup(portrait);
                    try
                    {
                        GUI.DrawTexture(new Rect(-36 * scaleX, -cropY * scaleY, 540 * scaleX, 870 * scaleY), portraitSheet);
                    }
                    finally { GUI.EndGroup(); }
                }
            }

            SkinOption selected = ChoiceToRemember(row);
            string current = !row.DraftEnabled || selected == null ? "原版" : SkinLabel(selected);
            if (!row.Edited && row.Active != selected) current = row.Active == null ? "原版" : SkinLabel(row.Active);
            Text(left, y + 7, 360 - left, 28, row.Character.Name, 20, TextColor);
            if (Button(left, y + 39, 360 - left, 33, current + "  ›", alignLeft: true))
                OpenPicker(row);
            bool on = row.DraftEnabled && selected != null && (row.Edited || row.Active != null);
            if (Button(378, y + 39, 118, 33, on ? "已开启" : "已关闭", on))
            {
                if (on)
                    Edit(row, false, selected.Id);
                else if (selected != null)
                    Edit(row, true, selected.Id);
                else
                    OpenPicker(row);
            }
        }

        private void OpenPicker(SkinSettings row)
        {
            _picker = row;
            _optionPage = 0;
        }

        private void DrawSkinPicker()
        {
            SkinSettings row = _picker;
            Text(25, 18, 384, 43, row.Character.Name, 28, TextColor);
            if (Button(416, 24, 100, 35, "返回")) { _picker = null; return; }
            Text(25, 66, 490, 28, "挑选要使用的皮肤", 17, MutedColor);

            bool original = !row.DraftEnabled || row.ResolveDraft() == null || (!row.Edited && row.Active == null);
            if (Button(25, 108, 490, 45, "原版", original, alignLeft: true))
            {
                Edit(row, false, ChoiceToRemember(row)?.Id ?? row.DraftSelection);
                _picker = null;
                return;
            }

            var skins = row.Character.Skins;
            for (int i = 0; i < OptionsPerPage; i++)
            {
                int index = _optionPage * OptionsPerPage + i;
                if (index >= skins.Count) break;
                SkinOption skin = skins[index];
                bool selected = !original && (row.Edited ? row.ResolveDraft() : row.Active) == skin;
                if (Button(25, 165 + i * 60, 490, 48, SkinLabel(skin), selected, alignLeft: true))
                {
                    Edit(row, true, skin.Id);
                    _picker = null;
                    return;
                }
            }
            DrawPagination(_optionPage, skins.Count, OptionsPerPage, 618, page => _optionPage = page);
        }

        private void DrawPagination(int page, int count, int perPage, float y, Action<int> changePage)
        {
            int pages = (count + perPage - 1) / perPage;
            if (pages <= 1) return;
            if (page > 0 && Button(25, y, 102, 34, "上一页")) changePage(page - 1);
            Text(174, y, 192, 34, $"{page + 1} / {pages}", 16, MutedColor, true);
            if (page + 1 < pages && Button(413, y, 102, 34, "下一页")) changePage(page + 1);
        }

        private void DrawCredits(float availableHeight)
        {
            _menuScale = Mathf.Min(_menuScale, (availableHeight - 43 * _menuScale) / CreditsHeight);
            _menuOrigin.x = Screen.width - (PanelWidth + 18) * _menuScale;
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(MenuRect(0, 0, PanelWidth, CreditsHeight), _menuArt["credits"]);
            if (Hit(335, 795, 180, 45)) _showCredits = false;
            if (Hit(485, 22, 44, 44)) _menuOpen = false;
        }

        private static string SkinLabel(SkinOption skin) => (skin.IsPreset ? "预设 · " : "自定义 · ") + skin.Name;

        private static SkinOption ChoiceToRemember(SkinSettings row) =>
            (!row.Edited ? row.Active : null) ?? row.ResolveDraft();

        private void Edit(SkinSettings row, bool enabled, string selection)
        {
            row.DraftEnabled = enabled;
            row.DraftSelection = selection ?? "";
            row.Edited = true;
            _status = "";
        }

        private void ResetMenuDraft()
        {
            _showCredits = false;
            _picker = null;
            _status = "";
            foreach (SkinSettings row in _settings)
            {
                row.DraftEnabled = row.Enabled.Value;
                row.DraftSelection = row.Selected.Value ?? "";
                row.Edited = false;
            }
        }

        private Rect MenuRect(float x, float y, float width, float height) =>
            new(_menuOrigin.x + x * _menuScale, _menuOrigin.y + y * _menuScale,
                width * _menuScale, height * _menuScale);

        private bool Hit(float x, float y, float width, float height) =>
            GUI.Button(MenuRect(x, y, width, height), "", _hitArea);

        private void Fill(float x, float y, float width, float height, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(MenuRect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void Text(float x, float y, float width, float height, string value, int size, Color color, bool centered = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUIStyle style = centered ? _centeredLabel : _label;
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * _menuScale));
            style.normal.textColor = color;
            GUI.Label(MenuRect(x, y, width, height), value, style);
        }

        private bool Button(float x, float y, float width, float height, string value, bool active = false, bool alignLeft = false)
        {
            bool hovered = MenuRect(x, y, width, height).Contains(Event.current.mousePosition);
            Color background = active ? AccentColor : hovered ? BorderColor : CardColor;
            Fill(x, y, width, height, BorderColor);
            Fill(x + 1, y + 1, width - 2, height - 2, background);
            float inset = alignLeft ? 12 : 0;
            Text(x + inset, y, width - inset, height, value, 17, active ? PanelColor : TextColor, !alignLeft);
            return Hit(x, y, width, height);
        }
    }
}
