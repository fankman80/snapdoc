#if WINDOWS
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace SnapDoc.Platforms.Windows;

public static class KeyboardHelper
{
    public static bool IsShiftPressed()
    {
        var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }
}
#endif
