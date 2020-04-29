using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.UI;
using TMPro;
using UnityEngine;

namespace miZyind.TraditionalChinese
{
    public static class TraditionalChinesePatches
    {
        private const string fn = "NotoSansCJKtc-Regular";
        private static readonly string ns = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
        private static TMP_FontAsset font;

        private static Stream GetResourceStream(string name)
        {
            return Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{ns}.Assets.{name}");
        }

        private static void ReassignFont(IEnumerable<TextMeshProUGUI> sequence)
        {
            sequence.DoIf(
                tmpg => tmpg != null && tmpg.font != null && tmpg.font.name != fn,
                tmpg => tmpg.font = font
            );
        }

        private static void ReassignString(ref string target, string targetString, string newString)
        {
            if (target.Contains(targetString)) target = target.Replace(targetString, newString);
        }

        public static class Mod_OnLoad
        {
            public static void OnLoad()
            {
                PUtil.InitLibrary();

                using (var stream = GetResourceStream("font"))
                {
                    var loadedFont = AssetBundle.LoadFromStream(stream).LoadAsset<TMP_FontAsset>(fn);

                    loadedFont.material.SetFloat(ShaderUtilities.ID_WeightBold, 0.3f);

                    TMP_Settings.fallbackFontAssets.Add(loadedFont);

                    font = loadedFont;
                }

                AssetBundle.UnloadAllAssetBundles(false);
            }
        }

        [HarmonyPatch(typeof(Localization))]
        [HarmonyPatch(nameof(Localization.Initialize))]
        public static class Localization_Initialize_Patch
        {
            public static bool Prefix()
            {
                var lines = new List<string>();

                using (var stream = GetResourceStream("strings.po"))
                using (var streamReader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    while (!streamReader.EndOfStream) lines.Add(streamReader.ReadLine());

                Localization.OverloadStrings(Localization.ExtractTranslatedStrings(lines.ToArray(), false));

                Localization.SwapToLocalizedFont(fn);

                return false;
            }
        }

        [HarmonyPatch(typeof(LanguageOptionsScreen))]
        [HarmonyPatch("RebuildPreinstalledButtons")]
        public static class LanguageOptionsScreen_RebuildPreinstalledButtons_Patch
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

                reference.text = "正體中文";

                component.GetReference<UnityEngine.UI.Image>("Image").sprite = sprite;

                ___buttons.Add(gameObject);

                return false;
            }
        }

        [HarmonyPatch(typeof(Game))]
        [HarmonyPatch("OnPrefabInit")]
        public static class Game_OnPrefabInit_Patch
        {
            public static void Prefix()
            {
                ReassignFont(Resources.FindObjectsOfTypeAll<LocText>());

                Db
                    .Get()
                    .ResourceTable
                    .DoIf(
                        res => res.GetType() == typeof(Klei.AI.Attribute),
                        res =>
                        {
                            ReassignString(ref res.Name, "Minimum", "最小");
                            ReassignString(ref res.Name, "Maximum", "最大");
                        }
                    );
            }
        }

        [HarmonyPatch(typeof(NameDisplayScreen))]
        [HarmonyPatch(nameof(NameDisplayScreen.AddNewEntry))]
        public static class NameDisplayScreen_AddNewEntry_Patch
        {
            public static void Postfix(NameDisplayScreen __instance, GameObject representedObject)
            {
                var targetEntry = __instance.entries.Find(entry => entry.world_go == representedObject);
                if (targetEntry != null && targetEntry.display_go != null)
                {
                    var txt = targetEntry.display_go.GetComponentInChildren<LocText>();
                    if (txt != null && txt.font.name != fn) txt.font = font;
                }
            }
        }

        [HarmonyPatch(typeof(DetailsPanelDrawer))]
        [HarmonyPatch(nameof(DetailsPanelDrawer.NewLabel))]
        [HarmonyPatch(new Type[] { typeof(string) })]
        public static class DetailsPanelDrawer_NewLabel_Patch
        {
            public static void Prefix(ref string text)
            {
                ReassignString(ref text, "Cycles", "歲");
            }
        }

        [HarmonyPatch(typeof(MinionTodoChoreEntry))]
        [HarmonyPatch("TooltipForChore")]
        public static class MinionTodoChoreEntry_TooltipForChore_Patch
        {
            public static void Postfix(ref string __result)
            {
                ReassignString(ref __result, "Idle", "空閒");
            }
        }
    }
}
