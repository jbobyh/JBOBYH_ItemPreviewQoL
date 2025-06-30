using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// ������ ����� Close � ������� ������ ���� InfoWindow.
    /// ��� ��������� ��� ��������� �������� ������ � �������� ��������� ����,
    /// ����� ���� �����������, � �� ������������.
    /// </summary>
    internal class InfoWindow_Close_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InfoWindow), "Close");
        // ���������� Postfix, ����� ��������� ���� ������ ����� �������� ����
        [PatchPostfix]
        private static void Postfix(InfoWindow __instance)
        {
            // �������� �������� ��������� ItemInfoWindowLabels.
            // ������ �� ������������ ��� ���� � ����� �������.
            var labels = __instance.GetComponent<ItemInfoWindowLabels>();
            if (labels == null)
            {
                // ��� �����-�� ������ ����, �� ���� �������������, ����������.
                return;
            }

            // �������� ��� ����� �������
            ItemPreviewInteractionManager.DeregisterInstance(labels);
        }
    }
}
