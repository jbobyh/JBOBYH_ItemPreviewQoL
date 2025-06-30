using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using UnityEngine.EventSystems;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// ������ ����� OnPointerClick � ������� ������ ���� InfoWindow.
    /// ��� ��������� ��� ����������� ������� ���� ��� ������������ �������������� ������.
    /// </summary>
    internal class InfoWindow_OnPointerClick_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InfoWindow), "OnPointerClick");
        // ���������� Postfix, ����� �� ������� ������������ ������ (����� ���� �� �������� ����)
        [PatchPostfix]
        private static void Postfix(InfoWindow __instance, PointerEventData eventData)
        {
            // ���� ��� ��� ��������, ������ �� ������
            if (!Plugin.EnablePlugin.Value) return;

            // ��� ���������� ������ ������� ���� ����� ������� ����
            if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount != 2)
            {
                return;
            }

            // �������� �������� ��������� ItemInfoWindowLabels, ������� ������ � ���� �����.
            // ������ �� �������� ������ � ����� �������.
            var labels = __instance.GetComponent<ItemInfoWindowLabels>();
            if (labels == null)
            {
                // ��� �����-�� ������ ����, �� ���� �������������, ���������� ���. 
                return;
            }

            // �������� ��� ����������
            ItemPreviewInteractionManager.HandleDoubleClick(labels);
        }
    }
}
