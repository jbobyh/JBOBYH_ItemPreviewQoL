using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine.UI;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    public class StretchArea_OnDrag_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StretchArea), "OnDrag");

        [HarmonyPriority(Priority.First)]
        [PatchPrefix]
        private static bool Prefix(StretchArea __instance)
        {
            if (__instance == null) return true;
            ItemInfoWindowLabels itemInfoWindowLabels = __instance.GetComponentInParent<ItemInfoWindowLabels>();
            if (itemInfoWindowLabels == null)
            {
                return true;
            }
            if (ItemPreviewInteractionManager.AllInstanceData.TryGetValue(itemInfoWindowLabels, out PreviewInstanceData data))
            {
                if (data.IsFullscreen)
                {
                    return false;
                }
                ItemPreviewInteractionManager.restoreButtonShouldBeVisibleGlobal = true;
                data.restoreButtonShouldBeVisible = true; 
            }
            return true;
        }
        [HarmonyPriority(Priority.Last)]
        [PatchPostfix]
        private static void Postfix(StretchArea __instance)
        {
            if (__instance == null || !Plugin.TyfonPresent()) return;
            ItemInfoWindowLabels itemInfoWindowLabels = __instance.GetComponentInParent<ItemInfoWindowLabels>();
            if (itemInfoWindowLabels == null)
            {
                return;
            }
            //resizeButton здесь всегда включается другим модом, надо решить когда выключать
            //когда фуллскрин
            //когда кнопка не была включена в окне
            if (ItemPreviewInteractionManager.AllInstanceData.TryGetValue(itemInfoWindowLabels, out PreviewInstanceData data))
            {
                if (data.IsFullscreen && !data.restoreButtonShouldBeVisible && !ItemPreviewInteractionManager.restoreButtonShouldBeVisibleGlobal)
                {
                    Button resizeButton = itemInfoWindowLabels.transform.Find("Inner/Caption Panel/Restore")?.GetComponent<Button>();
                    resizeButton?.gameObject.SetActive(false);
                }
            }
        }
    }
}