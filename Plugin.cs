using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using JBOBYH_ItemPreviewQoL.Patches;
using System;

namespace JBOBYH_ItemPreviewQoL
{
    [BepInDependency("Tyfon.UIFixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> EnablePlugin;
        public static ConfigEntry<bool> ShowScreenshotButton;
        public static ManualLogSource LogSource;
        private void Awake()
        {
            LogSource = Logger;

            EnablePlugin = Config.Bind(
                "General",
                "Enable Plugin",
                true,
                "Enables or disables all features of this plugin." 
            );
            ShowScreenshotButton = Config.Bind(
                "General",
                "Show Screenshot Button",
                true,
                "Enables or disables the screenshot button in the item preview window."
            );

            EnablePlugin.SettingChanged += OnEnablePluginChanged;

            try
            {
                new WeaponPreview_Zoom_Patch().Enable();
                new ItemInfoWindowLabels_method_4_Patch().Enable();
                new ItemInfoWindowLabels_Show_Patch().Enable();
                new InfoWindow_OnPointerClick_Patch().Enable();
                new InfoWindow_Close_Patch().Enable();
                DragTrigger_Patch.Enable();
                new StretchArea_OnDrag_Patch().Enable();
                new ItemSpecifications_Show_Patch().Enable();

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


        private static bool? IsTyfonPresent;

        public static bool TyfonPresent()
        {
            if (!IsTyfonPresent.HasValue)
            {
                if (Chainloader.PluginInfos.TryGetValue("Tyfon.UIFixes", out _))
                {
                    IsTyfonPresent = true;
                }
                else
                {
                    IsTyfonPresent = false;
                }
            }

            return IsTyfonPresent.Value;
        }

    }
}
