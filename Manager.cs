using BepInEx;
using EFT.Communications;
using EFT.UI;
using EFT.UI.WeaponModding;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    /// <summary>
    /// Класс-контейнер для хранения состояния и ссылок на объекты для одного конкретного экземпляра окна предпросмотра.
    /// Это позволяет корректно работать с несколькими открытыми окнами одновременно.
    /// </summary>
    internal class PreviewInstanceData
    {
        public enum DragMode { None, Rotating, Panning, LightRotating }

        public struct TransformState
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        // Текущее состояние взаимодействия
        public DragMode CurrentDragMode = DragMode.None;
        private bool _initialPositionStored = false;

        // Приватные поля для кеширования ссылок на игровые объекты
        private Transform _previewPivot;
        private Transform _rotator;
        private Camera _weaponCamera;
        private List<Light> _lights;
        private CameraLightSwitcher _lightSwitcher;
        private Dictionary<Light, TransformState> _originalLightStates;
        private readonly ItemInfoWindowLabels _instance;
        public LayoutElement _tyfonPreviewPanelLayout;
        public Transform _tyfonCaptionPanel;

        // Ссылка на главный компонент предпросмотра, из которого получаем все остальное
        private readonly WeaponPreview _weaponPreview;

        // Ссылки на UI элементы для управления полноэкранным режимом
        public GameObject DescriptionPanelGO;
        public Button FullscreenButton;
        public bool IsFullscreen = false;

        public bool restoreButtonShouldBeVisible;

        // Поля для сохранения оригинального состояния RectTransform при переходе в полноэкранный режим
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalAnchoredPosition;
        private Vector2 _originalSizeDelta;
        private ContentSizeFitter _sizeFitter;
        private RectTransform _windowRectTransform;
        public float _originalFlexibleHeight;

        // Публичные свойства с "ленивой" загрузкой для безопасного доступа к объектам
        public Transform Rotator => _rotator ??= _weaponPreview?.Rotator;

        public Transform PreviewPivot
        {
            get
            {
                if (_previewPivot == null && _weaponPreview != null)
                {
                    // Доступ к приватному полю transform_2 через рефлексию (более надежно, чем поиск по имени)
                    _previewPivot = AccessTools.Field(typeof(WeaponPreview), "transform_2").GetValue(_weaponPreview) as Transform;
                }
                return _previewPivot;
            }
        }

        public Camera WeaponCamera
        {
            get
            {
                if (_weaponCamera == null && _weaponPreview != null)
                {
                    // Доступ к приватному полю camera_0
                    _weaponCamera = AccessTools.Field(typeof(WeaponPreview), "camera_0").GetValue(_weaponPreview) as Camera;
                }
                return _weaponCamera;
            }
        }

        public List<Light> Lights
        {
            get
            {
                if (_lights == null && WeaponCamera != null)
                {
                    _lightSwitcher = WeaponCamera.GetComponent<CameraLightSwitcher>();
                    if (_lightSwitcher != null)
                    {
                        _lights = _lightSwitcher.Lights;

                        // Если мы получили список источников света впервые, сохраняем их состояние
                        if (_lights != null && _originalLightStates == null)
                        {
                            _originalLightStates = [];
                            foreach (var light in _lights)
                            {
                                if (light != null)
                                {
                                    _originalLightStates[light] = new TransformState
                                    {
                                        Position = light.transform.position,
                                        Rotation = light.transform.rotation
                                    };
                                }
                            }
                        }
                    }
                }
                return _lights;
            }
        }

        private Vector3 _absoluteInitialPivotPosition;

        public PreviewInstanceData(WeaponPreview weaponPreview, ItemInfoWindowLabels instance)
        {
            _weaponPreview = weaponPreview;
            _instance = instance;
        }

        public void Initialize()
        {
            // Получаем и кэшируем ссылку на RectTransform один раз
            if (_windowRectTransform == null)
            {
                _windowRectTransform = _instance.GetComponent<RectTransform>();
            }

            // Кешируем другие часто используемые компоненты
            if (DescriptionPanelGO == null)
            {
                var descriptionTransform = _instance.transform.Find("Inner/Contents/DescriptionPanel");
                if (descriptionTransform != null)
                {
                    DescriptionPanelGO = descriptionTransform.gameObject;
                }
            }

            // Кешируем компоненты для интеграции с Tyfon
            if (Plugin.TyfonPresent())
            {
                _tyfonPreviewPanelLayout = _instance.transform.Find("Inner/Contents/Preview Panel")?.GetComponent<LayoutElement>();
                _tyfonCaptionPanel = _instance.transform.Find("Inner/Caption Panel");
            }
        }
        public void BringToFront()
        {
            _windowRectTransform?.SetAsLastSibling();
        }

        /// <summary>
        /// Обрабатывает перемещение (панорамирование) предмета.
        /// </summary>
        public void HandlePanning(PointerEventData eventData, float baseSpeed, float limit)
        {
            if (PreviewPivot == null || WeaponCamera == null) return;

            // Сохраняем начальную позицию один раз при первом перемещении
            if (!_initialPositionStored)
            {
                _absoluteInitialPivotPosition = PreviewPivot.position;
                _initialPositionStored = true;
            }

            // Корректируем скорость перемещения в зависимости от размера окна и уровня зума
            var windowRect = _windowRectTransform;
            if (windowRect == null) return;

            const float baseWindowWidth = 670f;

            float sizeFactor = 1f;
            if (windowRect != null && windowRect.rect.width > 0)
            {
                sizeFactor = Mathf.Clamp(windowRect.rect.width / baseWindowWidth, 0.5f, 5f);
            }

            float zoomFactor = Mathf.Abs(WeaponCamera.transform.localPosition.z);
            float currentPanSpeed = baseSpeed * zoomFactor / sizeFactor;

            Vector3 deltaMove =
                (WeaponCamera.transform.right * eventData.delta.x * currentPanSpeed) +
                (WeaponCamera.transform.up * eventData.delta.y * currentPanSpeed);

            // Ограничиваем смещение от начальной точки
            Vector3 nextPotentialPosition = PreviewPivot.position + deltaMove;
            Vector3 offsetFromOrigin = nextPotentialPosition - _absoluteInitialPivotPosition;

            offsetFromOrigin.x = Mathf.Clamp(offsetFromOrigin.x, -limit, limit);
            offsetFromOrigin.y = Mathf.Clamp(offsetFromOrigin.y, -limit, limit);
            offsetFromOrigin.z = Mathf.Clamp(offsetFromOrigin.z, -limit, limit);

            PreviewPivot.position = _absoluteInitialPivotPosition + offsetFromOrigin;
        }

        /// <summary>
        /// Обрабатывает вращение самого предмета.
        /// </summary>
        public void HandleRotation(PointerEventData eventData, float rotationSpeed)
        {
            if (Rotator == null || WeaponCamera == null) return;

            float yaw = -eventData.delta.x * rotationSpeed;
            Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);

            float pitch = eventData.delta.y * rotationSpeed;
            Quaternion pitchRotation = Quaternion.AngleAxis(pitch, WeaponCamera.transform.right);

            Rotator.rotation = yawRotation * pitchRotation * Rotator.rotation;
        }

        /// <summary>
        /// Обрабатывает вращение источников света вокруг предмета.
        /// </summary>
        public void HandleLightRotation(PointerEventData eventData, float rotationSpeed)
        {
            if (Lights == null || Lights.Count == 0 || PreviewPivot == null) return;

            float yaw = -eventData.delta.x * rotationSpeed;
            float pitch = eventData.delta.y * rotationSpeed;

            foreach (Light light in Lights)
            {
                if (light == null) continue;

                // Вращаем каждый источник света вокруг центральной точки (пивота)
                light.transform.RotateAround(PreviewPivot.position, Vector3.up, yaw);
                light.transform.RotateAround(PreviewPivot.position, WeaponCamera.transform.right, pitch);
            }
        }

        public void ToggleFullscreen() 
        {
            // Обращаемся к полям напрямую, без 'data.'
            if (_sizeFitter == null) _sizeFitter = _instance.GetComponent<ContentSizeFitter>();

            if (DescriptionPanelGO == null)
            {
                var descriptionTransform = _instance.transform.Find("Inner/Contents/DescriptionPanel");
                if (descriptionTransform != null)
                {
                    DescriptionPanelGO = descriptionTransform.gameObject;
                }
            }

            if (_windowRectTransform == null || _sizeFitter == null) return;


            DescriptionPanelGO?.SetActive(!IsFullscreen);

            if (!IsFullscreen)
            {
                #region Tyfon

                if (Plugin.TyfonPresent())
                {
                    LayoutElement previewPanel = _tyfonPreviewPanelLayout?.GetComponent<LayoutElement>();
                    if (previewPanel != null)
                    {
                        _originalFlexibleHeight = previewPanel.flexibleHeight;
                        previewPanel.flexibleHeight = 1;
                    }
                    Transform сaptionPanel = null;
                    if (_tyfonCaptionPanel != null)
                    {
                        сaptionPanel = _tyfonCaptionPanel;
                    }
                    else
                    {
                        // Если Tyfon не установлен, ищем Caption Panel в другом месте
                        сaptionPanel = _instance.transform.Find("Inner/Caption Panel");
                    }
                    foreach (Transform child in сaptionPanel)
                    {
                        if (child.name == "Close Button(Clone)")
                        {
                            child.gameObject.SetActive(false);
                        }
                        else if (child.name == "Restore")
                        {
                            Button restoreButton = child.GetComponent<Button>();
                            if (restoreButton != null && restoreButton.IsActive())
                            {
                                restoreButton.onClick.AddListener(() =>
                                {
                                    if (IsFullscreen)
                                    {
                                        foreach (Transform child in сaptionPanel)
                                        {
                                            if (child.name == "Close Button(Clone)")
                                            {
                                                child.gameObject.SetActive(true);
                                            }
                                            else if (child.name == "Restore")
                                            {
                                                restoreButton.transform.localPosition = new Vector3(restoreButton.transform.localPosition.x - 2 * (((RectTransform)restoreButton.transform).rect.width + 3), restoreButton.transform.localPosition.y, restoreButton.transform.localPosition.z);
                                            }
                                        }
                                        ExitFullscreenIfActive();
                                    }
                                    restoreButtonShouldBeVisible = false;
                                    ItemPreviewInteractionManager.restoreButtonShouldBeVisibleGlobal = false;
                                });
                                restoreButton.transform.localPosition = new Vector3(restoreButton.transform.localPosition.x + 2 * (((RectTransform)restoreButton.transform).rect.width + 3), restoreButton.transform.localPosition.y, restoreButton.transform.localPosition.z);
                            }
                        }
                    }
                }

                #endregion

                // --- РЕЖИМ "НА ВЕСЬ ЭКРАН" ---
                BringToFront();
                _sizeFitter.enabled = false;

                _originalAnchorMin = _windowRectTransform.anchorMin;
                _originalAnchorMax = _windowRectTransform.anchorMax;
                _originalAnchoredPosition = _windowRectTransform.anchoredPosition;
                _originalSizeDelta = _windowRectTransform.sizeDelta;

                _windowRectTransform.anchorMin = Vector2.zero;
                _windowRectTransform.anchorMax = Vector2.one;
                _windowRectTransform.offsetMin = new Vector2(0, 35);
                _windowRectTransform.offsetMax = new Vector2(0, 0);

                DescriptionPanelGO?.SetActive(false);

                IsFullscreen = true;
            }
            else
            {
                #region Tyfon

                if (Plugin.TyfonPresent())
                {
                    LayoutElement layoutElement = null;
                    if (_tyfonPreviewPanelLayout != null)
                    {
                        layoutElement = _tyfonPreviewPanelLayout?.GetComponent<LayoutElement>();
                    }
                    else
                    {
                        // Если Tyfon не установлен, ищем LayoutElement в другом месте
                        layoutElement = _instance.transform.Find("Inner/Contents/Preview Panel")?.GetComponent<LayoutElement>();
                    }
                    if (layoutElement != null)
                    {
                        layoutElement.flexibleHeight = _originalFlexibleHeight;
                    }

                    Transform сaptionPanel = null;
                    if (_tyfonCaptionPanel != null)
                    {
                        сaptionPanel = _tyfonCaptionPanel;
                    }
                    else
                    {
                        // Если Tyfon не установлен, ищем Caption Panel в другом месте
                        сaptionPanel = _instance.transform.Find("Inner/Caption Panel");
                    }
                    foreach (Transform child in сaptionPanel)
                    {
                        if (child.name == "Close Button(Clone)")
                        {
                            child.gameObject.SetActive(true);
                        }
                        else if (child.name == "Restore")
                        {
                            Button restoreButton = child.GetComponent<Button>();
                            if (restoreButton != null && restoreButton.IsActive())
                            {
                                restoreButton.transform.localPosition = new Vector3(restoreButton.transform.localPosition.x - 2 * (((RectTransform)restoreButton.transform).rect.width + 3), restoreButton.transform.localPosition.y, restoreButton.transform.localPosition.z);
                            }
                        }
                    }

                }

                #endregion

                RestoreWindowedMode();
            }
        }

        /// <summary>
        /// Принудительно сворачивает окно из полноэкранного режима, если оно было в нем.
        /// </summary>
        public void ExitFullscreenIfActive()
        {
            if (IsFullscreen)
            {
                RestoreWindowedMode();
            }
        }

        private void RestoreWindowedMode()
        {
            // Если мы не в полноэкранном режиме, ничего не делаем
            if (!IsFullscreen) return;

            if (_windowRectTransform == null) return;

            if (_sizeFitter != null)
            {
                _sizeFitter.enabled = true;
            }

            // --- ВОССТАНАВЛИВАЕМ ИСХОДНЫЙ РАЗМЕР ---
            _windowRectTransform.anchorMin = _originalAnchorMin;
            _windowRectTransform.anchorMax = _originalAnchorMax;
            _windowRectTransform.anchoredPosition = _originalAnchoredPosition;
            _windowRectTransform.sizeDelta = _originalSizeDelta;


            DescriptionPanelGO?.SetActive(true);

            IsFullscreen = false;
        }

        /// <summary>
        /// Сбрасывает положение и вращение источников света к их исходному состоянию.
        /// </summary>
        public void ResetLights()
        {
            // Если у нас нет сохраненных состояний или источников света, ничего не делаем
            if (_originalLightStates == null || _lights == null)
            {
                return;
            }

            foreach (var light in _lights)
            {
                // Проверяем, есть ли для этого источника сохраненное состояние
                if (light != null && _originalLightStates.TryGetValue(light, out var originalState))
                {
                    light.transform.position = originalState.Position;
                    light.transform.rotation = originalState.Rotation;
                }
            }
        }
    }

    /// <summary>
    /// Статический менеджер, который управляет всеми активными окнами предпросмотра.
    /// Он отслеживает, какое окно сейчас активно, и применяет к нему нужную логику.
    /// </summary>
    internal static class ItemPreviewInteractionManager
    {
        // --- Настройки ---
        private const float BasePanSpeed = 0.001f;
        private const float RotationSpeed = 0.4f;
        private const float PanLimit = 0.4f;

        // Словарь для хранения данных по каждому экземпляру окна. Ключ - сам компонент окна.
        private static readonly Dictionary<ItemInfoWindowLabels, PreviewInstanceData> _instanceData = [];
        public static IReadOnlyDictionary<ItemInfoWindowLabels, PreviewInstanceData> AllInstanceData => _instanceData;

        public static bool restoreButtonShouldBeVisibleGlobal = false;

        /// <summary>
        /// Регистрирует новый экземпляр окна, когда оно открывается.
        /// </summary>
        public static void RegisterInstance(ItemInfoWindowLabels instance, WeaponPreview weaponPreview)
        {
            if (_instanceData.ContainsKey(instance)) return;

            var data = new PreviewInstanceData(weaponPreview, instance);
            data.Initialize();


            _instanceData[instance] = data;



            // Создаем кастомную кнопку для полноэкранного режима
            if (!Plugin.TyfonPresent())
                CreateFullscreenButton(instance, data);
        }

        /// <summary>
        /// Удаляет экземпляр окна из менеджера при его закрытии.
        /// </summary>
        public static void DeregisterInstance(ItemInfoWindowLabels instance)
        {
            if (_instanceData.TryGetValue(instance, out var data))
            {
                data.ResetLights();

                // Уничтожаем созданную нами кнопку, чтобы избежать утечек памяти
                if (data.FullscreenButton != null)
                {
                    UnityEngine.Object.Destroy(data.FullscreenButton.gameObject);
                }
                _instanceData.Remove(instance);
            }
        }

        private static void CreateFullscreenButton(ItemInfoWindowLabels instance, PreviewInstanceData data)
        {
            var infoWindow = instance.GetComponent<InfoWindow>();
            if (infoWindow == null || infoWindow.CloseButton == null) return;

            var originalButtonGO = infoWindow.CloseButton.gameObject;
            var newButtonGO = UnityEngine.Object.Instantiate(originalButtonGO, originalButtonGO.transform.parent, false);
            newButtonGO.name = "FullscreenButton_Modded";


            // Позиция кнопки справа от кнопки закрытия
            //newButtonGO.transform.localPosition = new Vector3(304.742f, 0, 0);
            var originalButtonRect = (RectTransform)originalButtonGO.transform;
            var step = originalButtonRect.rect.width + 3f; // 3f - это стандартный отступ между кнопками в заголовке

            // Смещаем кнопку влево от кнопки "Закрыть"
            newButtonGO.transform.localPosition = originalButtonGO.transform.localPosition - new Vector3(step, 0, 0);

            // Стилизация фона кнопки
            var backgroundImage = newButtonGO.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 1f);
            }

            // Удаляем оригинальную иконку крестика
            foreach (Transform child in newButtonGO.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            // Копируем иконку лупы из родительского объекта и делаем ее нашей иконкой
            var originalIconTransform = originalButtonGO.transform.parent.Find("Icon");
            if (originalIconTransform != null)
            {
                var newIconGO = UnityEngine.Object.Instantiate(originalIconTransform.gameObject, newButtonGO.transform, false);
                newIconGO.name = "Fullscreen_Icon";
                newIconGO.transform.localScale = new Vector3(1.04f, 0.8182f, 1f);
                newIconGO.transform.localPosition = new Vector3(-21.08f, -8.5f, 0f);
            }

            var newButton = newButtonGO.GetComponent<Button>();
            if (newButton == null) return;

            // Устанавливаем начальную видимость кнопки в зависимости от текущего состояния мода
            newButtonGO.SetActive(Plugin.EnablePlugin.Value);

            data.FullscreenButton = newButton;
            newButton.onClick.RemoveAllListeners();
            newButton.onClick.AddListener(() => data.ToggleFullscreen());
        }

        #region Обработчики событий мыши

        public static void OnBeginDrag(ItemInfoWindowLabels instance, PointerEventData eventData)
        {
            if (!Plugin.EnablePlugin.Value) return;
            if (!_instanceData.TryGetValue(instance, out var data)) return;

            data.BringToFront();

            // Определяем режим взаимодействия в зависимости от нажатой кнопки мыши
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Middle:
                    data.CurrentDragMode = PreviewInstanceData.DragMode.Panning;
                    break;
                case PointerEventData.InputButton.Right:
                    data.CurrentDragMode = PreviewInstanceData.DragMode.LightRotating;
                    break;
                default: // Левая кнопка и любые другие
                    data.CurrentDragMode = PreviewInstanceData.DragMode.Rotating;
                    break;
            }
        }

        public static void OnDrag(ItemInfoWindowLabels instance, PointerEventData eventData)
        {
            if (!Plugin.EnablePlugin.Value) return;
            if (!_instanceData.TryGetValue(instance, out var data)) return;

            // Выполняем действие в соответствии с ранее установленным режимом
            switch (data.CurrentDragMode)
            {
                case PreviewInstanceData.DragMode.Panning:
                    data.HandlePanning(eventData, BasePanSpeed, PanLimit);
                    break;
                case PreviewInstanceData.DragMode.Rotating:
                    data.HandleRotation(eventData, RotationSpeed);
                    break;
                case PreviewInstanceData.DragMode.LightRotating:
                    data.HandleLightRotation(eventData, RotationSpeed);
                    break;
            }
        }

        public static void OnEndDrag(ItemInfoWindowLabels instance)
        {
            if (!Plugin.EnablePlugin.Value) return;
            if (!_instanceData.TryGetValue(instance, out var data)) return;

            // Сбрасываем режим после отпускания кнопки мыши
            data.CurrentDragMode = PreviewInstanceData.DragMode.None;
        }

        /// <summary>
        /// Обновляет видимость кнопок "На весь экран" для всех активных окон.
        /// </summary>
        /// <param name="isVisible">Показывать ли кнопки.</param>
        public static void UpdateButtonsVisibility(bool isVisible)
        {
            // Проходим по всем значениям в словаре (по всем данным экземпляров)
            foreach (var kvp in _instanceData)
            {
                var instance = kvp.Key;
                var data = kvp.Value;

                // Устанавливаем активность игрового объекта кнопки
                data.FullscreenButton?.gameObject.SetActive(isVisible);

                #region Tyfon
                if (Plugin.TyfonPresent())
                {
                    LayoutElement layoutElement = null;
                    if (data._tyfonPreviewPanelLayout != null)
                    {
                        layoutElement = data._tyfonPreviewPanelLayout?.GetComponent<LayoutElement>();
                    }
                    else
                    {
                        // Если Tyfon не установлен, ищем LayoutElement в другом месте
                        layoutElement = instance.transform.Find("Inner/Contents/Preview Panel")?.GetComponent<LayoutElement>();
                    }
                    if (layoutElement != null)
                    {
                        layoutElement.flexibleHeight = data._originalFlexibleHeight;
                    }
                    Transform сaptionPanel = null;
                    if (data._tyfonCaptionPanel != null)
                    {
                        сaptionPanel = data._tyfonCaptionPanel;
                    }
                    else
                    {
                        // Если Tyfon не установлен, ищем Caption Panel в другом месте
                        сaptionPanel = instance.transform.Find("Inner/Caption Panel");
                    }
                    foreach (Transform child in сaptionPanel)
                    {
                        if (child.name == "Close Button(Clone)")
                        {
                            child.gameObject.SetActive(true);
                        }
                        else if (child.name == "Restore")
                        {
                            Button restoreButton = child.GetComponent<Button>();
                            if (restoreButton != null && restoreButton.IsActive() && !isVisible && data.IsFullscreen)
                            {
                                restoreButton.transform.localPosition = new Vector3(restoreButton.transform.localPosition.x - 2 * (((RectTransform)restoreButton.transform).rect.width + 3), restoreButton.transform.localPosition.y, restoreButton.transform.localPosition.z);
                            }
                        }
                    }

                }
                #endregion

                // Если плагин выключается (isVisible == false),
                // то мы должны свернуть окно, если оно было развернуто.
                if (!isVisible)
                {
                    data.ExitFullscreenIfActive();
                }

            }
        }

        /// <summary>
        /// Обрабатывает двойной клик, вызывая переключение полноэкранного режима.
        /// </summary>
        public static void HandleDoubleClick(ItemInfoWindowLabels instance)
        {
            if (_instanceData.TryGetValue(instance, out var data))
            {
                data.ToggleFullscreen();
            }
        }

        #endregion

    }


}
//todo: refactor