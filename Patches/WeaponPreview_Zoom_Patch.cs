using EFT.UI.WeaponModding;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// ������ ����� WeaponPreview.Zoom, ����� ��������� �������� ��������������� ������� ����.
    /// </summary>
    internal class WeaponPreview_Zoom_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(WeaponPreview), nameof(WeaponPreview.Zoom));
        }

        private const float MIN_ZOOM_MODIFIER = 0.05f; // ��������� �������� ��� ����. ����������� (��� ������, ��� ���������)
        private const float MAX_ZOOM_MODIFIER = 3.0f; // ��������� �������� ��� ����. ���������

        // �������� Z-��������� ������
        private const float MAX_ZOOM_IN_Z = 0.01f;
        private const float MAX_ZOOM_OUT_Z = -5f;

        [PatchPrefix]
        private static bool Prefix(WeaponPreview __instance, float zoom)
        {
            if (!Plugin.EnablePlugin.Value) return true; // ���� ������ ��������, ��������� ������������ �����

            Transform transform = __instance.WeaponPreviewCamera.transform;
            float currentZ = transform.localPosition.z;

            // 1. ������������, ��������� �� "������������" �� ����� ���� (�� 0 �� 1)
            float t = Mathf.InverseLerp(MAX_ZOOM_OUT_Z, MAX_ZOOM_IN_Z, currentZ);

            // 2. ���������� t, ����� ����� ��������������� ��������� �������� � ����� ���������.
            float dynamicModifier = Mathf.Lerp(MAX_ZOOM_MODIFIER, MIN_ZOOM_MODIFIER, t);

            // 3. ��������� ��� ������������ ���������
            zoom *= dynamicModifier;

            transform.Translate(new Vector3(0f, 0f, zoom));

            // ������������ ������� ������, ����� ��� �� ������� ������� ������ ��� ������.
            Vector3 localPosition = transform.localPosition;
            localPosition.z = Mathf.Clamp(localPosition.z, MAX_ZOOM_OUT_Z, MAX_ZOOM_IN_Z);
            transform.localPosition = localPosition;

            // ���������� false, ����� ������������ ����� Zoom �� ����������.
            return false;
        }
    }
}
