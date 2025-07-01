using EFT.UI.WeaponModding;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// ѕатчит метод WeaponPreview.Zoom, чтобы уменьшить скорость масштабировани€ колесом мыши.
    /// </summary>
    internal class WeaponPreview_Zoom_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(WeaponPreview), nameof(WeaponPreview.Zoom));
        }

        private const float MIN_ZOOM_MODIFIER = 0.05f; // ћножитель скорости при макс. приближении (чем меньше, тем медленнее)
        private const float MAX_ZOOM_MODIFIER = 3.0f; // ћножитель скорости при макс. отдалении

        // ƒиапазон Z-координат камеры
        private const float MAX_ZOOM_IN_Z = 0.01f;
        private const float MAX_ZOOM_OUT_Z = -5f;

        [PatchPrefix]
        private static bool Prefix(WeaponPreview __instance, float zoom)
        {
            if (!Plugin.EnablePlugin.Value) return true; // ≈сли плагин отключен, выполн€ем оригинальный метод

            Transform transform = __instance.WeaponPreviewCamera.transform;
            float currentZ = transform.localPosition.z;

            // 1. –ассчитываем, насколько мы "продвинулись" по шкале зума (от 0 до 1)
            float t = Mathf.InverseLerp(MAX_ZOOM_OUT_Z, MAX_ZOOM_IN_Z, currentZ);

            // 2. »спользуем t, чтобы найти соответствующий множитель скорости в нашем диапазоне.
            float dynamicModifier = Mathf.Lerp(MAX_ZOOM_MODIFIER, MIN_ZOOM_MODIFIER, t);

            // 3. ѕримен€ем наш динамический множитель
            zoom *= dynamicModifier;

            transform.Translate(new Vector3(0f, 0f, zoom));

            // ќграничиваем позицию камеры, чтобы она не уходила слишком далеко или близко.
            Vector3 localPosition = transform.localPosition;
            localPosition.z = Mathf.Clamp(localPosition.z, MAX_ZOOM_OUT_Z, MAX_ZOOM_IN_Z);
            transform.localPosition = localPosition;

            // ¬озвращаем false, чтобы оригинальный метод Zoom не выполн€лс€.
            return false;
        }
    }
}
