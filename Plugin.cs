using BepInEx;
using BepInEx.Configuration;
using ItemPreviewQoL.Patches;
using System;

namespace JBOBYH_ItemPreviewQoL
{
    [BepInPlugin("jbobyh.itempreviewqol", "Item Preview QoL", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> EnablePlugin;
        private void Awake()
        {
            EnablePlugin = Config.Bind(
                "General",
                "Enable Plugin",
                true,
                "Enables or disables all features of this plugin."
            );

            EnablePlugin.SettingChanged += OnEnablePluginChanged;

            try
            {
                new WeaponPreviewZoomPatch().Enable();
                new ItemInfoWindowLabels_method_4_Patch().Enable();
                new ItemInfoWindowLabels_Show_Patch().Enable();
                new InfoWindow_OnPointerClick_Patch().Enable();
                new InfoWindow_Close_Patch().Enable();

                Logger.LogInfo("[Item Preview QoL] Plugin loaded!");
            }
            catch (Exception e)
            {
                Logger.LogError($"[Item Preview QoL] Loading error: {e.Message}");
                Logger.LogError(e.StackTrace);
            }
        }

        private void OnEnablePluginChanged(object sender, EventArgs e)
        {
            // Вызываем метод в менеджере, чтобы он обновил все кнопки
            ItemPreviewInteractionManager.UpdateButtonsVisibility(EnablePlugin.Value);
        }

        private void OnDestroy()
        {
            // Отписываемся от события при выгрузке плагина
            EnablePlugin.SettingChanged -= OnEnablePluginChanged;
        }
    }
}
