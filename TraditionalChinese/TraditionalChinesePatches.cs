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
    using CB = Action<MotdServerClient.MotdResponse, string>;
    using Resp = MotdServerClient.MotdResponse;

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

        [HarmonyPatch(typeof(MotdServerClient))]
        [HarmonyPatch(nameof(MotdServerClient.GetMotd))]
        public static class MotdServerClient_GetMotd_Patch
        {
            private static Type Resp = typeof(Action<MotdServerClient.MotdResponse, string>);

            private static MethodInfo GetLocalMotd = typeof(MotdServerClient).GetMethod(
                "GetLocalMotd",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            private static PropertyInfo MotdLocalPath = typeof(MotdServerClient).GetProperty(
                "MotdLocalPath",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            public static bool Prefix(MotdServerClient __instance, CB cb)
            {
                var path = MotdLocalPath.GetValue(__instance, null);
                var localMotd = GetLocalMotd.Invoke(__instance, new object[] { path }) as Resp;

                localMotd.image_header_text = "資料片開發計畫！";
                localMotd.news_header_text = "參與討論";
                localMotd.news_body_text = "訂閱我們的通知郵件\n以隨時掌握最新資訊\n或到論壇直接參與討論！";
                localMotd.patch_notes_summary =
                    "<b>2020 年 11 月之雲端存檔更新</b>\n\n" +
                    "• 新增雲端存檔功能，打開存檔選單以管理你的本機與雲端存檔！\n" +
                    "• 大量的遊戲效能提升，尤其是對於有著很多複製人的殖民地\n" +
                    "• 將 Unity 遊戲引擎升级到最新的 LTS 版本\n" +
                    "請查看完整更新說明來獲得進一步的訊息！";
                localMotd.update_text_override = "喔耶！更新了！";

                cb(localMotd, null);

                return false;
            }
        }

        [HarmonyPatch(typeof(PatchNotesScreen))]
        [HarmonyPatch("OnSpawn")]
        public static class PatchNotesScreen_OnSpawn_Patch
        {
            public static void Postfix(PatchNotesScreen __instance)
            {
                __instance
                    .GetComponentsInChildren<TextMeshProUGUI>()
                    .DoIf(
                        txt => txt != null && txt.name == "Title",
                        txt => txt.text = "更新說明"
                    );
            }
        }
    }
}
