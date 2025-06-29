using EFT.UI;
using SPT.Reflection.Patching;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using System.Reflection;

namespace ItemPreviewQoL.Patches
{
    /// <summary>
    /// Этот патч полностью отключает оригинальную игровую логику вращения предмета (метод method_4).
    /// Наша собственная логика реализуется через ItemPreviewInteractionManager. 
    /// </summary>
    internal class ItemInfoWindowLabels_method_4_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemInfoWindowLabels), "method_4");

        [PatchPrefix]
        private static bool Prefix()
        {
            if (!Plugin.EnablePlugin.Value) return true; // Если плагин отключен, выполняем оригинальный метод
            return false; // Возвращаем false, чтобы предотвратить выполнение оригинального метода
        } 
    }
}
