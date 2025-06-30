using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using System.Reflection;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// Патчит метод Close в базовом классе окна InfoWindow.
    /// Это позволяет нам корректно очистить данные и сбросить состояние мода,
    /// когда окно закрывается, а не уничтожается.
    /// </summary>
    internal class InfoWindow_Close_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InfoWindow), "Close");
        // Используем Postfix, чтобы выполнить нашу логику после закрытия окна
        [PatchPostfix]
        private static void Postfix(InfoWindow __instance)
        {
            // Пытаемся получить компонент ItemInfoWindowLabels.
            // Именно он используется как ключ в нашем словаре.
            var labels = __instance.GetComponent<ItemInfoWindowLabels>();
            if (labels == null)
            {
                // Это какое-то другое окно, не окно предпросмотра, игнорируем.
                return;
            }

            // Вызываем наш метод очистки
            ItemPreviewInteractionManager.DeregisterInstance(labels);
        }
    }
}
