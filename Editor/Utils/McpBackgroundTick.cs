#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace McpUnity.Utils
{
    /// <summary>
    /// Keeps the Unity Editor loop ticking while the editor window is unfocused on Windows.
    /// </summary>
    internal static class McpBackgroundTick
    {
#if UNITY_EDITOR_WIN
        private delegate void TimerProc(IntPtr hWnd, uint uMsg, UIntPtr nIDEvent, uint dwTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern UIntPtr SetTimer(IntPtr hWnd, UIntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool KillTimer(IntPtr hWnd, UIntPtr uIDEvent);

        private const uint TickIntervalMs = 100;

        // Rooted for the complete native timer lifetime so the callback cannot be collected.
        private static readonly TimerProc Callback = OnTimer;
        private static UIntPtr _timerId;
        private static int _callbackRunning;
#endif

        /// <summary>
        /// Starts the Windows background timer after the WebSocket server is listening.
        /// </summary>
        public static void Start()
        {
#if UNITY_EDITOR_WIN
            if (_timerId != UIntPtr.Zero)
            {
                return;
            }

            _timerId = SetTimer(IntPtr.Zero, UIntPtr.Zero, TickIntervalMs, Callback);
            if (_timerId == UIntPtr.Zero)
            {
                McpLogger.LogError($"Failed to start background editor tick timer. Win32 error: {Marshal.GetLastWin32Error()}.");
            }
#endif
        }

        /// <summary>
        /// Stops the Windows background timer. Safe to call repeatedly.
        /// </summary>
        public static void Stop()
        {
#if UNITY_EDITOR_WIN
            UIntPtr timerId = _timerId;
            if (timerId == UIntPtr.Zero)
            {
                return;
            }

            try
            {
                if (!KillTimer(IntPtr.Zero, timerId))
                {
                    McpLogger.LogWarning($"Failed to stop background editor tick timer. Win32 error: {Marshal.GetLastWin32Error()}.");
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error stopping background editor tick timer: {ex.Message}");
            }
            finally
            {
                _timerId = UIntPtr.Zero;
            }
#endif
        }

        /// <summary>
        /// Resets the internal state for testing purposes.
        /// Should only be called in test environments.
        /// </summary>
        internal static void ResetForTesting()
        {
#if UNITY_EDITOR_WIN
            if (_timerId != UIntPtr.Zero)
            {
                try { KillTimer(IntPtr.Zero, _timerId); } catch { }
                _timerId = UIntPtr.Zero;
            }
            _callbackRunning = 0;
#endif
        }

#if UNITY_EDITOR_WIN
        private static void OnTimer(IntPtr hWnd, uint uMsg, UIntPtr nIDEvent, uint dwTime)
        {
            if (Interlocked.Exchange(ref _callbackRunning, 1) != 0)
            {
                return;
            }

            try
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception ex)
            {
                try
                {
                    McpLogger.LogError($"Error during background editor tick: {ex.Message}");
                }
                catch
                {
                    // Nothing may escape the native callback boundary.
                }
            }
            finally
            {
                Volatile.Write(ref _callbackRunning, 0);
            }
        }
#endif
    }
}
#endif
