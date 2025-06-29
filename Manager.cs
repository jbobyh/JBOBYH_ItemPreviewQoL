using EFT.UI;
using EFT.UI.WeaponModding;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ItemPreviewQoL.Patches
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

        // Ссылка на главный компонент предпросмотра, из которого получаем все остальное
        private readonly WeaponPreview _weaponPreview;

        // Ссылки на UI элементы для управления полноэкранным режимом
        public GameObject DescriptionPanelGO;
        public Button FullscreenButton;
        public bool IsFullscreen = false;

        // Поля для сохранения оригинального состояния RectTransform при переходе в полноэкранный режим
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalAnchoredPosition;
        private Vector2 _originalSizeDelta;
        private ContentSizeFitter _sizeFitter;
        private RectTransform _windowRectTransform;

        // Поля для хранения делегатов для корректной отписки
        public Action<PointerEventData> OnBeginDragAction;
        public Action<PointerEventData> OnDragAction;
        public Action<PointerEventData> OnEndDragAction;
        public Action<PointerEventData> OnPointerClickAction;

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
                            _originalLightStates = new Dictionary<Light, TransformState>();
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

        public PreviewInstanceData(WeaponPreview weaponPreview)
        {
            _weaponPreview = weaponPreview;
        }

        public void Initialize(ItemInfoWindowLabels instance)
        {
            // Получаем и кэшируем ссылку на RectTransform один раз
            if (_windowRectTransform == null)
            {
                _windowRectTransform = instance.GetComponent<RectTransform>();
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

        public void ToggleFullscreen(ItemInfoWindowLabels instance) 
        {
            var windowRect = _windowRectTransform;

            // Обращаемся к полям напрямую, без 'data.'
            if (_sizeFitter == null) _sizeFitter = instance.GetComponent<ContentSizeFitter>();

            if (DescriptionPanelGO == null)
            {
                var descriptionTransform = instance.transform.Find("Inner/Contents/DescriptionPanel");
                if (descriptionTransform != null)
                {
                    DescriptionPanelGO = descriptionTransform.gameObject;
                }
            }

            if (windowRect == null || _sizeFitter == null) return;

            IsFullscreen = !IsFullscreen;

            DescriptionPanelGO?.SetActive(!IsFullscreen);

            if (IsFullscreen)
            {
                // --- РЕЖИМ "НА ВЕСЬ ЭКРАН" ---
                BringToFront();
                _sizeFitter.enabled = false;

                _originalAnchorMin = windowRect.anchorMin;
                _originalAnchorMax = windowRect.anchorMax;
                _originalAnchoredPosition = windowRect.anchoredPosition;
                _originalSizeDelta = windowRect.sizeDelta;

                windowRect.anchorMin = Vector2.zero;
                windowRect.anchorMax = Vector2.one;
                windowRect.offsetMin = new Vector2(0, 35);
                windowRect.offsetMax = new Vector2(0, 0);
            }
            else
            {
                // --- ВОССТАНАВЛИВАЕМ ИСХОДНЫЙ РАЗМЕР ---
                windowRect.anchorMin = _originalAnchorMin;
                windowRect.anchorMax = _originalAnchorMax;
                windowRect.anchoredPosition = _originalAnchoredPosition;
                windowRect.sizeDelta = _originalSizeDelta;

                _sizeFitter.enabled = true;
            }
        }

        /// <summary>
        /// Принудительно сворачивает окно из полноэкранного режима, если оно было в нем.
        /// </summary>
        public void ExitFullscreenIfActive(ItemInfoWindowLabels instance)
        {
            // Если мы не в полноэкранном режиме, ничего не делаем
            if (!IsFullscreen) return;

            // Здесь мы по сути дублируем логику из "else" блока метода ToggleFullscreen
            var windowRect = _windowRectTransform;
            if (windowRect == null) return;

            // --- ВОССТАНАВЛИВАЕМ ИСХОДНЫЙ РАЗМЕР ---
            windowRect.anchorMin = _originalAnchorMin;
            windowRect.anchorMax = _originalAnchorMax;
            windowRect.anchoredPosition = _originalAnchoredPosition;
            windowRect.sizeDelta = _originalSizeDelta;

            if (_sizeFitter != null)
            {
                _sizeFitter.enabled = true;
            }

            DescriptionPanelGO?.SetActive(true);

            // И самое главное - обновляем флаг состояния
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
        private const float PanLimit = 0.3f;

        // Словарь для хранения данных по каждому экземпляру окна. Ключ - сам компонент окна.
        private static readonly Dictionary<ItemInfoWindowLabels, PreviewInstanceData> _instanceData = new Dictionary<ItemInfoWindowLabels, PreviewInstanceData>();

        /// <summary>
        /// Регистрирует новый экземпляр окна, когда оно открывается.
        /// </summary>
        public static void RegisterInstance(ItemInfoWindowLabels instance, WeaponPreview weaponPreview)
        {
            if (_instanceData.ContainsKey(instance)) return;

            var data = new PreviewInstanceData(weaponPreview);
            data.Initialize(instance);

            // Подписываемся на события перетаскивания мыши
            data.OnBeginDragAction = (eventData) => OnBeginDrag(instance, eventData);
            data.OnDragAction = (eventData) => OnDrag(instance, eventData);
            data.OnEndDragAction = (eventData) => OnEndDrag(instance, eventData);

            _instanceData[instance] = data;


            // Подписываемся на события, используя сохраненные делегаты
            instance.DragTrigger_0.onBeginDrag += data.OnBeginDragAction;
            instance.DragTrigger_0.onDrag += data.OnDragAction;
            instance.DragTrigger_0.onEndDrag += data.OnEndDragAction;

            // Создаем кастомную кнопку для полноэкранного режима
            CreateFullscreenButton(instance, data);

            // При закрытии окна вызываем DeregisterInstance для очистки
            //instance.AddDisposable((Action)(() => DeregisterInstance(instance)));
        }

        /// <summary>
        /// Удаляет экземпляр окна из менеджера при его закрытии.
        /// </summary>
        public static void DeregisterInstance(ItemInfoWindowLabels instance)
        {
            if (_instanceData.TryGetValue(instance, out var data))
            {
                data.ResetLights();

                instance.DragTrigger_0.onBeginDrag -= data.OnBeginDragAction;
                instance.DragTrigger_0.onDrag -= data.OnDragAction;
                instance.DragTrigger_0.onEndDrag -= data.OnEndDragAction;
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
            newButtonGO.transform.localPosition = new Vector3(304.742f, 0, 0);

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
            newButton.onClick.AddListener(() => data.ToggleFullscreen(instance));
        }

        #region Обработчики событий мыши

        private static void OnBeginDrag(ItemInfoWindowLabels instance, PointerEventData eventData)
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

        private static void OnDrag(ItemInfoWindowLabels instance, PointerEventData eventData)
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

        private static void OnEndDrag(ItemInfoWindowLabels instance, PointerEventData eventData)
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

                if (data.FullscreenButton != null)
                {
                    // Устанавливаем активность игрового объекта кнопки
                    data.FullscreenButton.gameObject.SetActive(isVisible);
                }

                // Если плагин выключается (isVisible == false),
                // то мы должны свернуть окно, если оно было развернуто.
                if (!isVisible)
                {
                    data.ExitFullscreenIfActive(instance);
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
                data.ToggleFullscreen(instance);
            }
        }

        #endregion
    }
}
//todo: сделать зум туда, где курсор находится?