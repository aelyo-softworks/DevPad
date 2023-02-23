using System.Diagnostics;
using System.Windows.Forms;
using DevPad.Utilities;

namespace DevPad.MonacoModel
{
    public class DevPadKeyEventArgs : DevPadEventArgs
    {
        public DevPadKeyEventArgs(DevPadEventType type, string json)
            : base(type, json)
        {
        }

        public int KeyCode => RootElement.GetValue("keyCode", 0);
        public string Code => RootElement.GetNullifiedValue("code");
        public bool Alt => RootElement.GetValue("altKey", false);
        public bool AltGraph => RootElement.GetValue("altGraphKey", false);
        public bool Ctrl => RootElement.GetValue("ctrlKey", false);
        public bool Meta => RootElement.GetValue("metaKey", false);
        public bool Shift => RootElement.GetValue("shiftKey", false);

        public Keys Keys
        {
            get
            {
                var c = From((KeyCode)KeyCode);
                if (Alt || AltGraph)
                {
                    c |= Keys.Alt;
                }

                if (Ctrl)
                {
                    c |= Keys.Control;
                }

                if (Shift)
                {
                    c |= Keys.Shift;
                }
                return c;
            }
        }

        public static Keys From(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case MonacoModel.KeyCode.Backspace:
                    return Keys.Back;
                case MonacoModel.KeyCode.Tab:
                    return Keys.Tab;
                case MonacoModel.KeyCode.Enter:
                    return Keys.Enter;
                case MonacoModel.KeyCode.Shift:
                    return Keys.Shift;
                case MonacoModel.KeyCode.Ctrl:
                    return Keys.Control;
                case MonacoModel.KeyCode.Alt:
                    return Keys.Alt;
                case MonacoModel.KeyCode.PauseBreak:
                    return Keys.Pause;
                case MonacoModel.KeyCode.CapsLock:
                    return Keys.CapsLock;
                case MonacoModel.KeyCode.Escape:
                    return Keys.Escape;
                case MonacoModel.KeyCode.Space:
                    return Keys.Space;
                case MonacoModel.KeyCode.PageUp:
                    return Keys.PageUp;
                case MonacoModel.KeyCode.PageDown:
                    return Keys.PageDown;
                case MonacoModel.KeyCode.End:
                    return Keys.End;
                case MonacoModel.KeyCode.Home:
                    return Keys.Home;
                case MonacoModel.KeyCode.LeftArrow:
                    return Keys.Left;
                case MonacoModel.KeyCode.UpArrow:
                    return Keys.Up;
                case MonacoModel.KeyCode.RightArrow:
                    return Keys.Right;
                case MonacoModel.KeyCode.DownArrow:
                    return Keys.Down;
                case MonacoModel.KeyCode.Insert:
                    return Keys.Insert;
                case MonacoModel.KeyCode.Delete:
                    return Keys.Delete;
                case MonacoModel.KeyCode.Digit0:
                    return Keys.D0;
                case MonacoModel.KeyCode.Digit1:
                    return Keys.D1;
                case MonacoModel.KeyCode.Digit2:
                    return Keys.D2;
                case MonacoModel.KeyCode.Digit3:
                    return Keys.D3;
                case MonacoModel.KeyCode.Digit4:
                    return Keys.D4;
                case MonacoModel.KeyCode.Digit5:
                    return Keys.D5;
                case MonacoModel.KeyCode.Digit6:
                    return Keys.D6;
                case MonacoModel.KeyCode.Digit7:
                    return Keys.D7;
                case MonacoModel.KeyCode.Digit8:
                    return Keys.D8;
                case MonacoModel.KeyCode.Digit9:
                    return Keys.D9;
                case MonacoModel.KeyCode.KeyA:
                    return Keys.A;
                case MonacoModel.KeyCode.KeyB:
                    return Keys.B;
                case MonacoModel.KeyCode.KeyC:
                    return Keys.C;
                case MonacoModel.KeyCode.KeyD:
                    return Keys.D;
                case MonacoModel.KeyCode.KeyE:
                    return Keys.E;
                case MonacoModel.KeyCode.KeyF:
                    return Keys.F;
                case MonacoModel.KeyCode.KeyG:
                    return Keys.G;
                case MonacoModel.KeyCode.KeyH:
                    return Keys.H;
                case MonacoModel.KeyCode.KeyI:
                    return Keys.I;
                case MonacoModel.KeyCode.KeyJ:
                    return Keys.J;
                case MonacoModel.KeyCode.KeyK:
                    return Keys.K;
                case MonacoModel.KeyCode.KeyL:
                    return Keys.L;
                case MonacoModel.KeyCode.KeyM:
                    return Keys.M;
                case MonacoModel.KeyCode.KeyN:
                    return Keys.N;
                case MonacoModel.KeyCode.KeyO:
                    return Keys.O;
                case MonacoModel.KeyCode.KeyP:
                    return Keys.P;
                case MonacoModel.KeyCode.KeyQ:
                    return Keys.Q;
                case MonacoModel.KeyCode.KeyR:
                    return Keys.R;
                case MonacoModel.KeyCode.KeyS:
                    return Keys.S;
                case MonacoModel.KeyCode.KeyT:
                    return Keys.T;
                case MonacoModel.KeyCode.KeyU:
                    return Keys.U;
                case MonacoModel.KeyCode.KeyV:
                    return Keys.V;
                case MonacoModel.KeyCode.KeyW:
                    return Keys.W;
                case MonacoModel.KeyCode.KeyX:
                    return Keys.X;
                case MonacoModel.KeyCode.KeyY:
                    return Keys.Y;
                case MonacoModel.KeyCode.KeyZ:
                    return Keys.Z;
                case MonacoModel.KeyCode.Meta:
                    return Keys.LWin;
                case MonacoModel.KeyCode.ContextMenu:
                    return Keys.RWin;
                case MonacoModel.KeyCode.F1:
                    return Keys.F1;
                case MonacoModel.KeyCode.F2:
                    return Keys.F2;
                case MonacoModel.KeyCode.F3:
                    return Keys.F3;
                case MonacoModel.KeyCode.F4:
                    return Keys.F4;
                case MonacoModel.KeyCode.F5:
                    return Keys.F5;
                case MonacoModel.KeyCode.F6:
                    return Keys.F6;
                case MonacoModel.KeyCode.F7:
                    return Keys.F7;
                case MonacoModel.KeyCode.F8:
                    return Keys.F8;
                case MonacoModel.KeyCode.F9:
                    return Keys.F9;
                case MonacoModel.KeyCode.F10:
                    return Keys.F10;
                case MonacoModel.KeyCode.F11:
                    return Keys.F11;
                case MonacoModel.KeyCode.F12:
                    return Keys.F12;
                case MonacoModel.KeyCode.F13:
                    return Keys.F13;
                case MonacoModel.KeyCode.F14:
                    return Keys.F14;
                case MonacoModel.KeyCode.F15:
                    return Keys.F15;
                case MonacoModel.KeyCode.F16:
                    return Keys.F16;
                case MonacoModel.KeyCode.F17:
                    return Keys.F17;
                case MonacoModel.KeyCode.F18:
                    return Keys.F18;
                case MonacoModel.KeyCode.F19:
                    return Keys.F19;
                case MonacoModel.KeyCode.NumLock:
                    return Keys.NumLock;
                case MonacoModel.KeyCode.ScrollLock:
                    return Keys.Scroll;
                case MonacoModel.KeyCode.Numpad0:
                    return Keys.NumPad0;
                case MonacoModel.KeyCode.Numpad1:
                    return Keys.NumPad1;
                case MonacoModel.KeyCode.Numpad2:
                    return Keys.NumPad2;
                case MonacoModel.KeyCode.Numpad3:
                    return Keys.NumPad3;
                case MonacoModel.KeyCode.Numpad4:
                    return Keys.NumPad4;
                case MonacoModel.KeyCode.Numpad5:
                    return Keys.NumPad5;
                case MonacoModel.KeyCode.Numpad6:
                    return Keys.NumPad6;
                case MonacoModel.KeyCode.Numpad7:
                    return Keys.NumPad7;
                case MonacoModel.KeyCode.Numpad8:
                    return Keys.NumPad8;
                case MonacoModel.KeyCode.Numpad9:
                    return Keys.NumPad9;
                case MonacoModel.KeyCode.NumpadSubtract:
                    return Keys.Subtract;
                case MonacoModel.KeyCode.NumpadAdd:
                    return Keys.Add;
                case MonacoModel.KeyCode.NumpadDecimal:
                    return Keys.Decimal;
                case MonacoModel.KeyCode.NumpadMultiply:
                    return Keys.Multiply;
                case MonacoModel.KeyCode.NumpadDivide:
                    return Keys.Divide;
                case MonacoModel.KeyCode.AudioVolumeMute:
                    return Keys.VolumeMute;
                case MonacoModel.KeyCode.AudioVolumeUp:
                    return Keys.VolumeUp;
                case MonacoModel.KeyCode.AudioVolumeDown:
                    return Keys.VolumeDown;
                case MonacoModel.KeyCode.BrowserSearch:
                    return Keys.BrowserSearch;
                case MonacoModel.KeyCode.BrowserHome:
                    return Keys.BrowserHome;
                case MonacoModel.KeyCode.BrowserBack:
                    return Keys.BrowserBack;
                case MonacoModel.KeyCode.BrowserForward:
                    return Keys.BrowserForward;
                case MonacoModel.KeyCode.MediaTrackNext:
                    return Keys.MediaNextTrack;
                case MonacoModel.KeyCode.MediaTrackPrevious:
                    return Keys.MediaPreviousTrack;
                case MonacoModel.KeyCode.MediaStop:
                    return Keys.MediaStop;
                case MonacoModel.KeyCode.MediaPlayPause:
                    return Keys.MediaPlayPause;
                case MonacoModel.KeyCode.Clear:
                    return Keys.Clear;
                case MonacoModel.KeyCode.OEM_8:
                    return Keys.Oem8;
                case MonacoModel.KeyCode.Comma:
                    return Keys.Oemcomma;
                case MonacoModel.KeyCode.Semicolon:
                    return Keys.OemSemicolon;
                case MonacoModel.KeyCode.Slash:
                    return Keys.OemQuestion;
                case MonacoModel.KeyCode.BracketLeft:
                    return Keys.OemOpenBrackets;
                case MonacoModel.KeyCode.Backslash:
                    return Keys.OemBackslash;
                case MonacoModel.KeyCode.BracketRight:
                    return Keys.OemCloseBrackets;
                case MonacoModel.KeyCode.Quote:
                    return Keys.OemQuotes;
                case MonacoModel.KeyCode.IntlBackslash:
                    return Keys.OemBackslash;

                //???
                case MonacoModel.KeyCode.Equal:
                case MonacoModel.KeyCode.Minus:
                case MonacoModel.KeyCode.Period:
                case MonacoModel.KeyCode.Backquote:
                case MonacoModel.KeyCode.NUMPAD_SEPARATOR:
                case MonacoModel.KeyCode.KEY_IN_COMPOSITION:
                case MonacoModel.KeyCode.ABNT_C1:
                case MonacoModel.KeyCode.ABNT_C2:
                    break;
            }
            Trace.WriteLine($"Unmapped keycode '{keyCode}'");
            return 0;
        }
    }
}
