using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using UnityEngine.EventSystems;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// Патчит метод OnPointerClick в базовом классе окна InfoWindow.
    /// Это позволяет нам перехватить двойной клик для переключения полноэкранного режима.
    /// </summary>
    internal class InfoWindow_OnPointerClick_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InfoWindow), "OnPointerClick");
        // Используем Postfix, чтобы не сломать оригинальную логику (вывод окна на передний план)
        [PatchPostfix]
        private static void Postfix(InfoWindow __instance, PointerEventData eventData)
        {
            // Если наш мод выключен, ничего не делаем
            if (!Plugin.EnablePlugin.Value) return;

            // Нас интересует только двойной клик левой кнопкой мыши
            if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount != 2)
            {
                return;
            }

            // Пытаемся получить компонент ItemInfoWindowLabels, который связан с этим окном.
            // Именно он является ключом в нашем словаре.
            var labels = __instance.GetComponent<ItemInfoWindowLabels>();
            if (labels == null)
            {
                // Это какое-то другое окно, не окно предпросмотра, игнорируем его. 
                return;
            }

            // Вызываем наш обработчик
            ItemPreviewInteractionManager.HandleDoubleClick(labels);
        }
    }
}
