using EFT.UI;
using EFT.UI.WeaponModding;
using SPT.Reflection.Patching;
using HarmonyLib;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// ���� ���� ����������� ����� �������� ���� ������������� (����� Show).
    /// ��� ������ - ���������������� ����� ��������� ���� � ����� ���������.
    /// </summary>
    internal class ItemInfoWindowLabels_Show_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemInfoWindowLabels), "Show");

        [HarmonyAfter("Tyfon.UIFixes")]
        [PatchPostfix]
        private static void Postfix(ItemInfoWindowLabels __instance, WeaponPreview weaponPreview)
        {
            // ������������ ������ ��� ����������� ���� � ����� ���������
            ItemPreviewInteractionManager.RegisterInstance(__instance, weaponPreview);
        }
    }
}
