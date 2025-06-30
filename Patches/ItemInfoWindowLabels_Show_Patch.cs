using EFT.UI;
using EFT.UI.WeaponModding;
using SPT.Reflection.Patching;
using HarmonyLib;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// Ётот патч срабатывает после открыти€ окна предпросмотра (метод Show).
    /// ≈го задача - зарегистрировать новый экземпл€р окна в нашем менеджере.
    /// </summary>
    internal class ItemInfoWindowLabels_Show_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemInfoWindowLabels), "Show");

        [HarmonyAfter("Tyfon.UIFixes")]
        [PatchPostfix]
        private static void Postfix(ItemInfoWindowLabels __instance, WeaponPreview weaponPreview)
        {
            // –егистрируем только что открывшеес€ окно в нашем менеджере
            ItemPreviewInteractionManager.RegisterInstance(__instance, weaponPreview);
        }
    }
}
