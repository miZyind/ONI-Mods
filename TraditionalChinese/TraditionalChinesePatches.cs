using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;
using PeterHan.PLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace miZyind.TraditionalChinese
{
    public static class TraditionalChinesePatches
    {
        private static readonly string ns = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
        private static readonly string fn = "NotoSansCJKtc-Regular";

        private static Stream GetResourceStream(string name)
        {
            return Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{ns}.Assets.{name}");
        }

        public static class Mod_OnLoad
        {
            public static void OnLoad()
            {
                PUtil.InitLibrary();

                using (var stream = GetResourceStream("font"))
                {
                    var font = AssetBundle.LoadFromStream(stream).LoadAsset<TMP_FontAsset>(fn);
                    TMP_Settings.fallbackFontAssets.Add(font);
                }

                AssetBundle.UnloadAllAssetBundles(false);
            }
        }

        [HarmonyPatch(typeof(Localization))]
        [HarmonyPatch(nameof(Localization.Initialize))]
        public class Localization_Initialize_Patch
        {
            public static bool Prefix()
            {
                var lines = new List<string>();

                using (var stream = GetResourceStream("strings.po"))
                using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                    while (!streamReader.EndOfStream) lines.Add(streamReader.ReadLine());

                Localization.OverloadStrings(Localization.ExtractTranslatedStrings(lines.ToArray(), false));
                Localization.SwapToLocalizedFont(fn);

                return false;
            }
        }

        [HarmonyPatch(typeof(LanguageOptionsScreen))]
        [HarmonyPatch("RebuildPreinstalledButtons")]
        public class LanguageOptionsScreen_RebuildPreinstalledButtons_Patch
        {
            public static bool Prefix(LanguageOptionsScreen __instance, ref List<GameObject> ___buttons)
            {
                var sprite = PUtil.LoadSprite($"{ns}.Assets.preview.png");
                var gameObject = Util.KInstantiateUI(
                    __instance.languageButtonPrefab,
                    __instance.preinstalledLanguagesContainer,
                    false
                );
                var component = gameObject.GetComponent<HierarchyReferences>();
                var reference = component.GetReference<LocText>("Title");

                reference.text = "正體中文"; component.GetReference<Image>("Image").sprite = sprite; ___buttons.Add(gameObject);

                return false;
            }
        }

        [HarmonyPatch(typeof(Game))]
        [HarmonyPatch("OnPrefabInit")]
        public class Game_OnSpawn_Patch
        {
            public static void Prefix()
            {
                Resources
                    .FindObjectsOfTypeAll<TextMeshProUGUI>()
                    .DoIf(
                        tmpg => tmpg != null && tmpg.font != null,
                        tmpg => tmpg.font = TMP_Settings.fallbackFontAssets.Last()
                    );
            }
        }
    }
}
