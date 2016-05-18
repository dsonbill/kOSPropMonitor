using System;
using System.Collections.Generic;
using UnityEngine;
using kOS.Safe.UserIO;

namespace kOSPropMonitor
{
    public class RPMConsole : InternalModule
    {
        //Control Variables
        private const string CONTROL_LOCKOUT = "kOSPropMonitor";

        //State Tracking
        private bool isLocked = false;

        //Keyboard Memory Variables
        private KeyBinding rememberThrottleCutoffKey;
        private KeyBinding rememberThrottleFullKey;
        private KeyBinding rememberCameraResetKey;
        private KeyBinding rememberCameraModeKey;
        private KeyBinding rememberCameraViewKey;



        public override void OnUpdate()
        {
            
        }


        void Initialize()
        {
            // Set Key Binding Memory - these are safe from kOS up here
            rememberCameraResetKey = GameSettings.CAMERA_RESET;
            rememberCameraModeKey = GameSettings.CAMERA_MODE;
            rememberCameraViewKey = GameSettings.CAMERA_NEXT;
            rememberThrottleCutoffKey = GameSettings.THROTTLE_CUTOFF;
            rememberThrottleFullKey = GameSettings.THROTTLE_FULL;
        }

        //Keyboard Control
        public void ToggleLock()
        {
            if (isLocked)
                Unlock();
            else
                Lock();
        }

        private void Lock()
        {
            if (isLocked) return;

            isLocked = true;

            InputLockManager.SetControlLock(CONTROL_LOCKOUT);

            // Prevent editor keys from being pressed while typing
            EditorLogic editor = EditorLogic.fetch;
            //TODO: POST 0.90 REVIEW
            if (editor != null && InputLockManager.IsUnlocked(ControlTypes.All)) editor.Lock(true, true, true, CONTROL_LOCKOUT);

            // This seems to be the only way to force KSP to let me lock out the "X" throttle
            // key.  It seems to entirely bypass the logic of every other keypress in the game,
            // so the only way to fix it is to use the keybindings system from the Setup screen.
            // When the terminal is focused, the THROTTLE_CUTOFF action gets unbound, and then
            // when its unfocused later, its put back the way it was:
            GameSettings.CAMERA_RESET = new KeyBinding(KeyCode.None);
            GameSettings.CAMERA_MODE = new KeyBinding(KeyCode.None);
            GameSettings.CAMERA_NEXT = new KeyBinding(KeyCode.None);
            GameSettings.THROTTLE_CUTOFF = new KeyBinding(KeyCode.None);
            GameSettings.THROTTLE_FULL = new KeyBinding(KeyCode.None);
        }

        private void Unlock()
        {
            if (!isLocked) return;

            isLocked = false;

            InputLockManager.RemoveControlLock(CONTROL_LOCKOUT);


            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) editor.Unlock(CONTROL_LOCKOUT);

            // This seems to be the only way to force KSP to let me lock out the "X" throttle
            // key.  It seems to entirely bypass the logic of every other keypress in the game:
            GameSettings.THROTTLE_CUTOFF = rememberThrottleCutoffKey;
            GameSettings.THROTTLE_FULL = rememberThrottleFullKey;
            GameSettings.CAMERA_RESET = rememberCameraResetKey;
            GameSettings.CAMERA_MODE = rememberCameraModeKey;
            GameSettings.CAMERA_NEXT = rememberCameraViewKey;
        }

        void ProcessKeyEvents()
        {
            Event e = Event.current;

            // This *HAS* to be up here. I have no idea what's causing this.
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Type((char)UnicodeCommand.STARTNEXTLINE);
            }

            if (e.type == EventType.KeyDown)
            {
                // Unity handles some keys in a particular way
                // e.g. Keypad7 is mapped to 0xffb7 instead of 0x37
                var c = (char)(e.character & 0x007f);

                // command sequences
                if (e.keyCode == KeyCode.C && e.control) // Ctrl+C
                {
                    Type((char)UnicodeCommand.BREAK);
                    return;
                }
                // Command used to be Control-shift-X, now we don't care if shift is down aymore, to match the telnet expereince
                // where there is no such thing as "uppercasing" a control char.
                if ((e.keyCode == KeyCode.X && e.control) ||
                    (e.keyCode == KeyCode.D && e.control) // control-D to match the telnet experience
                   )
                {
                    Type((char)0x000d);
                    return;
                }

                if (e.keyCode == KeyCode.A && e.control)
                {
                    Type((char)0x0001);
                    return;
                }

                if (e.keyCode == KeyCode.E && e.control)
                {
                    Type((char)0x0005);
                    return;
                }

                if (0x20 <= c && c < 0x7f) // printable characters
                {
                    Type(c);
                    //cursorBlinkTime = 0.0f; // Don't blink while the user is still actively typing.
                }

                else if (e.keyCode != KeyCode.None)
                {
                    switch (e.keyCode)
                    {
                        case KeyCode.Tab: Type('\t'); break;
                        case KeyCode.LeftArrow: Type((char)UnicodeCommand.LEFTCURSORONE); break;
                        case KeyCode.RightArrow: Type((char)UnicodeCommand.RIGHTCURSORONE); break;
                        case KeyCode.UpArrow: Type((char)UnicodeCommand.UPCURSORONE); break;
                        case KeyCode.DownArrow: Type((char)UnicodeCommand.DOWNCURSORONE); break;
                        case KeyCode.Home: Type((char)UnicodeCommand.HOMECURSOR); break;
                        case KeyCode.End: Type((char)UnicodeCommand.ENDCURSOR); break;
                        case KeyCode.PageUp: Type((char)UnicodeCommand.PAGEUPCURSOR); break;
                        case KeyCode.PageDown: Type((char)UnicodeCommand.PAGEDOWNCURSOR); break;
                        case KeyCode.Delete: Type((char)UnicodeCommand.DELETERIGHT); break;
                        case KeyCode.Backspace: Type((char)UnicodeCommand.DELETELEFT); break;

                        //THESE ARE NOT WORKING FOR ME.
                        case KeyCode.KeypadEnter:  // (deliberate fall through to next case)
                        case KeyCode.Return: Type((char)UnicodeCommand.STARTNEXTLINE); break;

                        // More can be added to the list here to support things like F1, F2, etc.  But at the moment we don't use them yet.

                        // default: ignore and allow the event to pass through to whatever else wants to read it:
                        default: break;
                    }
                    //cursorBlinkTime = 0.0f;// Don't blink while the user is still actively typing.
                }
            }
        }

        private void Type(char command)
        {
            //if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            //{
            //    processor_shares[current_processor_id].Window.ProcessOneInputChar(command, null);
            //}
        }
    }
}