using EFT.UI;
using HarmonyLib;
using JBOBYH_ItemPreviewQoL.Patches;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JBOBYH_ItemPreviewQoL.Patches
{
    public static class DragTrigger_Patch
    {
        private static ItemInfoWindowLabels _currentWindow;
        public static void Enable()
        {
            new HighPriorityDragPatch(typeof(DragTrigger), nameof(DragTrigger.OnDrag)).Enable();
            new HighPriorityDragPatch(typeof(DragTrigger), nameof(DragTrigger.OnBeginDrag)).Enable();
            new HighPriorityDragPatch(typeof(DragTrigger), nameof(DragTrigger.OnEndDrag)).Enable();

            new HighPriorityDragPatch(typeof(UIDragComponent), "UnityEngine.EventSystems.IDragHandler.OnDrag").Enable();
            new HighPriorityDragPatch(typeof(UIDragComponent), "UnityEngine.EventSystems.IBeginDragHandler.OnBeginDrag").Enable();
        }

        public class HighPriorityDragPatch(Type type, string methodName) : ModulePatch
        {
            private readonly string methodName = methodName;
            private readonly Type type = type;

            protected override MethodBase GetTargetMethod() => AccessTools.Method(type, methodName);

            [HarmonyPriority(Priority.First)]
            [PatchPrefix]
            private static bool Prefix(object __instance, PointerEventData eventData, MethodBase __originalMethod)
            {
                if (_currentWindow != null)
                {
                    if (__originalMethod.Name.Contains("OnDrag"))
                        ItemPreviewInteractionManager.OnDrag(_currentWindow, eventData);
                    else if (__originalMethod.Name.Contains("OnEndDrag"))
                    {
                        ItemPreviewInteractionManager.OnEndDrag(_currentWindow);
                        _currentWindow = null;
                    }
                    return true;
                }

                if (__originalMethod.Name.Contains("OnBeginDrag"))
                {
                    Component instanceComp = __instance as Component;
                    if (instanceComp != null)
                    {
                        var currentWindow = instanceComp.GetComponentInParent<ItemInfoWindowLabels>();
                        if (currentWindow != null && instanceComp.name == "Preview Panel")
                        {
                            _currentWindow = currentWindow;

                            ItemPreviewInteractionManager.OnBeginDrag(_currentWindow, eventData);
                            return true;
                        }
                    }
                }

                return true;
            }
        }
    }
}
