using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using System.Reflection;

namespace ItemPreviewQoL.Patches
{
    /// <summary>
    /// ���� ���� ��������� ��������� ������������ ������� ������ �������� �������� (����� method_4).
    /// ���� ����������� ������ ����������� ����� ItemPreviewInteractionManager. 
    /// </summary>
    internal class ItemInfoWindowLabels_method_4_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemInfoWindowLabels), "method_4");

        [PatchPrefix]
        private static bool Prefix()
        {
            if (!Plugin.EnablePlugin.Value) return true; // ���� ������ ��������, ��������� ������������ �����
            return false; // ���������� false, ����� ������������� ���������� ������������� ������
        } 
    }
}
