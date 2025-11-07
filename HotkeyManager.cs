using System;
using System.Windows;
using System.Runtime.InteropServices;

namespace CS2Overlay
{
    public static class HotkeyManager
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const uint MOD_CONTROL = 0x0002;
        const uint MOD_ALT = 0x0001;
        const uint VK_H = 0x48;  // H key
        const uint VK_U = 0x55;  // U key
        const uint VK_Q = 0x51;  // Q key

        public static void Register(Window window, Action toggleVisibility, Action toggleClickThrough, Action closeApp)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            
            // Register multiple hotkeys
            RegisterHotKey(helper.Handle, 9000, MOD_CONTROL | MOD_ALT, VK_H);  // Hide/Show
            RegisterHotKey(helper.Handle, 9001, MOD_CONTROL | MOD_ALT, VK_U);  // Unlock/Lock
            RegisterHotKey(helper.Handle, 9002, MOD_CONTROL | MOD_ALT, VK_Q);  // Quit

            System.Windows.Interop.HwndSource.FromHwnd(helper.Handle).AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == 0x0312) // WM_HOTKEY
                {
                    int hotkeyId = wParam.ToInt32();
                    switch (hotkeyId)
                    {
                        case 9000: // Ctrl+Alt+H - Toggle visibility
                            toggleVisibility();
                            break;
                        case 9001: // Ctrl+Alt+U - Toggle click-through
                            toggleClickThrough();
                            break;
                        case 9002: // Ctrl+Alt+Q - Close application
                            closeApp();
                            break;
                    }
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }
    }
}
