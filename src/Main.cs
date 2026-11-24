using System;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(Bubu.Main), "BuBu", "1.6.0", "BuBuXiLi")]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]

namespace Bubu
{
    public partial class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            string customRoot = Path.Combine(MelonEnvironment.GameRootDirectory, "Custom Skin");
            string modDirectory = Path.GetDirectoryName(typeof(Main).Assembly.Location);
            var catalog = SkinCatalog.Load(customRoot, Path.Combine(modDirectory, "bubu.pak"), MelonLogger.Warning);
            InitializeSkins(catalog);
            MelonLogger.Msg($"[Bubu] 就绪 · {catalog.Characters.Count} 个角色 · {catalog.Characters.Sum(character => character.Skins.Count)} 套皮肤");
        }
    }
}
