using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBOBYH_ItemPreviewQoL
{
    internal class Debug
    {
        public static void Log(object message)
        {
            UnityExplorer.ExplorerCore.Log($"[TarkovDebug] {message}");
        }
        public static void LogError(object message)
        {
            UnityExplorer.ExplorerCore.LogError($"[TarkovDebug Error] {message}");
        }
        public static void LogWarning(object message)
        {
            UnityExplorer.ExplorerCore.LogWarning($"[TarkovDebug Error] {message}");
        }
    }
}
