using EFT.Communications;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    internal class ItemSpecifications_Show_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemSpecificationPanel), nameof(ItemSpecificationPanel.Show));
        }

        [PatchPostfix]
        private static void Postfix(ItemSpecificationPanel __instance, InteractionButtonsContainer ____interactionButtonsContainer)
        {
            if (!Plugin.ShowScreenshotButton.Value) return;
            SimpleContextMenuButton _buttonTemplate = (SimpleContextMenuButton)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonTemplate").GetValue(____interactionButtonsContainer);
            RectTransform _buttonsContainer = (RectTransform)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonsContainer").GetValue(____interactionButtonsContainer);

            SimpleContextMenuButton newButton = ____interactionButtonsContainer.method_1("EXPORTFILE", "Screenshot", _buttonTemplate, _buttonsContainer, null,
                delegate
                {
                    Transform cameraImage = __instance.transform.Find("Inner/Contents/Preview Panel/Camera Image");
                    if (cameraImage == null)
                    {
                        Plugin.LogSource?.LogError("Error. No cameraImage");
                        NotificationManagerClass.DisplayMessageNotification("Error. No cameraImage", ENotificationDurationType.Default, ENotificationIconType.Alert, null);
                        return;
                    }

                    RawImage rawImage = cameraImage.GetComponent<RawImage>();
                    if (rawImage == null)
                    {
                        Plugin.LogSource?.LogError("Error. No rawImage");
                        NotificationManagerClass.DisplayMessageNotification("Error. No rawImage", ENotificationDurationType.Default, ENotificationIconType.Alert, null);
                        return;
                    }

                    string name = __instance.transform.Find("Inner/Caption Panel/Caption")?.GetComponent<CustomTextMeshProUGUI>()?.text;
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Escape from Tarkov", "Screenshots", $"{name} {EFTDateTimeClass.Now:yyyy-MM-dd[HH-mm-ss]}.png");
                    if (CaptureFromRenderTexture(rawImage, path))
                    {
                        NotificationManagerClass.DisplayMessageNotification($"Screeshot saved: {path}", ENotificationDurationType.Default, ENotificationIconType.Default, null);
                    }
                    else
                    {
                        Plugin.LogSource?.LogError("Error. Failed to save screenshot");
                        NotificationManagerClass.DisplayMessageNotification("Error. Failed to save screenshot", ENotificationDurationType.Default, ENotificationIconType.Alert, null);
                    }
                },
                null, false, false);


            // make the new button disposable
            ____interactionButtonsContainer.method_5(newButton);
        }

        private static bool CaptureFromRenderTexture(RawImage rawImage, string filePath)
        {
            // 1. Получаем RenderTexture из RawImage
            Texture source = rawImage.texture;
            if (source is not RenderTexture renderTexture)
            {
                Plugin.LogSource?.LogError("Текстура в RawImage не является RenderTexture!");
                // Можно добавить сюда логику для обычных Texture2D, если нужно
                return false;
            }

            // Сохраняем ссылку на текущую активную RenderTexture, чтобы потом ее вернуть
            RenderTexture currentActiveRT = RenderTexture.active;

            try
            {
                // 2. Создаем временную Texture2D для копирования пикселей
                Texture2D texture2D = new(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);

                // 3. Делаем нашу RenderTexture активной
                RenderTexture.active = renderTexture;

                // 4. Копируем пиксели из активной RenderTexture в нашу Texture2D
                texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

                // 5. Применяем изменения, чтобы пиксели загрузились в текстуру
                texture2D.Apply();

                // 6. Теперь, когда у нас есть Texture2D, мы можем ее закодировать в PNG
                byte[] bytes = texture2D.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);

                // Очищаем временную текстуру
                UnityEngine.Object.Destroy(texture2D);
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError($"Ошибка при сохранении скриншота: {ex.Message}");
                return false;
            }
            finally
            {
                // 7. ВОЗВРАЩАЕМ ОБРАТНО исходную активную RenderTexture. Это КРИТИЧЕСКИ ВАЖНО!
                RenderTexture.active = currentActiveRT;
            }
            return true;
        }
    }
}
