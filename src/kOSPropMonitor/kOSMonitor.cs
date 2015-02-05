using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP.IO;
using kOS;
using kOS.Safe.Screen;
using kOS.Module;
using kOS.Execution;
using kOS.Safe.Compilation;
using kOS.Safe.Utilities;

namespace kOSPropMonitor
{
    public class kOSMonitor : InternalModule
    {
        //Buttons
        [KSPField]
        public int processorSelectorUpButton = 0;
        [KSPField]
        public int processorSelectorDownButton = 1;
        [KSPField]
        public int openConsoleButton = 2;
        [KSPField]
        public int toggleProcessorPowerButton = 3;
        [KSPField]
        public int toggleKeyboardButton = 4;

        //kOS Fields
        [KSPField]
        public string textTint = "[#009900ff]";
        [KSPField]
        public string textTintUnpowered = "[#ffffff3e]";
        [KSPField]
        public int consoleWidth = 50;
        [KSPField]
        public int consoleHeight = 36;

        //General State Variables
        private bool initialized = false;
        private string response = "kOS Terminal Standing By";
        private bool isPowered = false;


        //kOS Processor Variables
        private bool processorIsInstalled;
        private int current_processor_id = 0;
        private List<SharedObjects> processor_shares;
        private List<kOSProcessor> vessel_processors;

        //kOS Terminal Variables
        private bool consumeEvent;
        private const string CONTROL_LOCKOUT = "kOSPropMonitor";
        private bool isLocked = false;
        private bool consoleIsOpen = false;
        private string consoleBuffer;
        private float cursorBlinkTime;
        private string currentTextTint;

        //kOS Keyboar Variables
        private KeyBinding rememberThrottleCutoffKey;
        private KeyBinding rememberThrottleFullKey;
        private KeyBinding rememberCameraResetKey;
        private KeyBinding rememberCameraModeKey;
        private KeyBinding rememberCameraViewKey;



        public void Start()
        {

        }

        public override void OnUpdate()
        {
            if (processorIsInstalled) {
                if (initialized) {
                    //Set power state from SharedObjects
                    isPowered = processor_shares [current_processor_id].Window.IsPowered;

                    //Set text tinting depending on power state
                    if (isPowered) {
                        currentTextTint = textTint;
                    } else {
                        currentTextTint = textTintUnpowered;
                    }

                    //Process keystrokes
                    if (isLocked) {
                        if (processor_shares [current_processor_id] != null) {
                            ProcessKeyStrokes ();
                        }
                    }

                    //Unlock if console is not open, or if the selected console is not powered.
                    if (!isPowered && isLocked || !consoleIsOpen && isLocked) {
                        Unlock ();
                    }

                    //Copy the ScreenBuffer to the consoleBuffer
                    BufferConsole ();

                    //Consume event - IDEK
                    if (consumeEvent) {
                        consumeEvent = false;
                        Event.current.Use ();
                    }
                }
                cursorBlinkTime += Time.deltaTime;

                if (cursorBlinkTime > 1)
                    cursorBlinkTime -= 1;
            }
        }

        public void Initialize(int screenWidth, int screenHeight)
        {
            //Set Processor Installed Flag
            processorIsInstalled = false;

            //Register kOSProcessors
            vessel_processors = GetProcessorList();

            //Instantiate SharedObjects List
            processor_shares = new List<SharedObjects>();

            foreach (kOSProcessor kos_processor in vessel_processors)
            {
                //Set Processor Installed Flag
                processorIsInstalled = true;

                UnityEngine.Debug.Log("kOSPropMonitor Found A Processor! Beginning Registration...");

                //Register the kOSProcessor's SharedObjects
                processor_shares.Add(GetProcessorShared(kos_processor));
                UnityEngine.Debug.Log("kOSPropMonitor Registered Processor Share");
            }


            UnityEngine.Debug.Log("kOSPropMonitor Initialized!");
            initialized = true;
        }

        public string ContentProcessor(int screenWidth, int screenHeight)
        {
            //Check for initialization
            if (!initialized) {
                if (this.vessel != null)
                    Initialize (screenWidth, screenHeight);
            }

            if (processorIsInstalled) {
                //Do console logic if open
                if (consoleIsOpen) {
                    //Set screen size if needed
                    if (processor_shares [current_processor_id].Screen.ColumnCount != consoleWidth || processor_shares [current_processor_id].Screen.RowCount != consoleHeight) {
                        processor_shares [current_processor_id].Screen.SetSize (consoleHeight, consoleWidth);
                    }

                    //Set response to the consoleBuffer
                    response = consoleBuffer;
                }
            } else {
                response = "kOS is not installed!";
            }
            return response;
        }

        public void ButtonProcessor(int buttonID)
        {
            if (processorIsInstalled) {

                //A better kOSProcessor cycler. *Might* Improve a bit.
                if (buttonID == processorSelectorUpButton) {

                    response = "";
                
                    current_processor_id--;

                    if (current_processor_id == -1) {
                        current_processor_id = vessel_processors.Count - 1;
                    }

                    isPowered = processor_shares [current_processor_id].Window.IsPowered;

                    if (isPowered) {
                        currentTextTint = textTint;
                    } else {
                        currentTextTint = textTintUnpowered;
                    }

                    for (int processor_count = 0; processor_count < vessel_processors.Count; processor_count++) {
                        if (processor_count == current_processor_id) {
                            response += "kOS Processor " + currentTextTint + processor_count + "[#FFFFFF] <--" + System.Environment.NewLine;
                        } else {
                            response += "kOS Processor " + processor_count + System.Environment.NewLine;
                        }
                    }

                } else if (buttonID == processorSelectorDownButton) {

                    response = "";

                    current_processor_id++;

                    if (current_processor_id == vessel_processors.Count) {
                        current_processor_id = 0;
                    }

                    isPowered = processor_shares [current_processor_id].Window.IsPowered;

                    if (isPowered) {
                        currentTextTint = textTint;
                    } else {
                        currentTextTint = textTintUnpowered;
                    }

                    for (int processor_count = 0; processor_count < vessel_processors.Count; processor_count++) {
                        if (processor_count == current_processor_id) {
                            response += "kOS Processor " + currentTextTint + processor_count + "[#FFFFFF] <--" + System.Environment.NewLine;
                        } else {
                            response += "kOS Processor " + processor_count + System.Environment.NewLine;
                        }
                    }
                }


                //Opens the console
                else if (buttonID == openConsoleButton) {
                    consoleIsOpen = true;
                }


                //Power Toggle Button
                else if (buttonID == toggleProcessorPowerButton) {
                    if (vessel_processors [current_processor_id] != null) {
                        vessel_processors [current_processor_id].TogglePower ();

                        isPowered = processor_shares [current_processor_id].Window.IsPowered;

                        if (isPowered) {
                            currentTextTint = textTint;
                        } else {
                            currentTextTint = textTintUnpowered;
                        }

                        if (!consoleIsOpen) {
                            response = "";
                            for (int processor_count = 0; processor_count < vessel_processors.Count; processor_count++) {
                                if (processor_count == current_processor_id) {
                                    response += "kOS Processor " + currentTextTint + processor_count + "[#FFFFFF] <--" + System.Environment.NewLine;
                                } else {
                                    response += "kOS Processor " + processor_count + System.Environment.NewLine;
                                }
                            }
                        }
                    }
                }


                //Keyboard input lock button
                else if (buttonID == toggleKeyboardButton && consoleIsOpen) {
                    ToggleLock ();
                }

                //Allow usage of toggleKeyboardButton and toggleProcessorPowerButton without closing the console
                if (consoleIsOpen && buttonID != openConsoleButton && buttonID != toggleKeyboardButton && buttonID != toggleProcessorPowerButton) {
                    consoleIsOpen = false;
                }
            }
        }


        //kOS-Utilities
        public SharedObjects GetProcessorShared(kOSProcessor processor)
        {
            FieldInfo sharedField = typeof(kOSProcessor).GetField("shared", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
            var proc_shared = sharedField.GetValue(processor);
            return (SharedObjects)proc_shared;
        }

        public void ToggleOpen()
        {
            if (!consoleIsOpen)
                consoleIsOpen = true;
            else
                consoleIsOpen = false;
        }

        public List<kOSProcessor> GetProcessorList()
        {
            return this.vessel.FindPartModulesImplementing<kOSProcessor>();
        }
        
        
        //Printing
        public void BufferConsole()
        {
            if (processor_shares[current_processor_id] != null)
            {
                //Bliny Cursor!
                bool blinkOn = cursorBlinkTime < 0.5f && processor_shares[current_processor_id].Screen.CursorRowShow < processor_shares[current_processor_id].Screen.RowCount && isPowered;

                string cursor = " ";
                if (blinkOn)
                {
                    cursor = "_";
                }

                List<char[]> buffer = processor_shares[current_processor_id].Screen.GetBuffer();
                consoleBuffer = "";

                for (int row = 0; row < processor_shares[current_processor_id].Screen.RowCount; row++)
                {

                    char[] lineBuffer = buffer[row];
                    string line = "";

                    int column_index = 0;
                    foreach (char c in lineBuffer)
                    {
                        if (column_index == processor_shares[current_processor_id].Screen.CursorColumnShow && row == processor_shares[current_processor_id].Screen.CursorRowShow)
                        {
                            line += cursor;
                        }

                        line += c;
                        column_index++;
                    }

                    consoleBuffer += currentTextTint + line + "[#ffffffff]" + System.Environment.NewLine;
                }
            }
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
            rememberCameraResetKey = GameSettings.CAMERA_RESET;
            GameSettings.CAMERA_RESET = new KeyBinding(KeyCode.None);
            rememberCameraModeKey = GameSettings.CAMERA_MODE;
            GameSettings.CAMERA_MODE = new KeyBinding(KeyCode.None);
            rememberCameraViewKey = GameSettings.CAMERA_NEXT;
            GameSettings.CAMERA_NEXT = new KeyBinding(KeyCode.None);
            rememberThrottleCutoffKey = GameSettings.THROTTLE_CUTOFF;
            GameSettings.THROTTLE_CUTOFF = new KeyBinding(KeyCode.None);
            rememberThrottleFullKey = GameSettings.THROTTLE_FULL;
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
            if (rememberThrottleCutoffKey != null)
                GameSettings.THROTTLE_CUTOFF = rememberThrottleCutoffKey;
            if (rememberThrottleFullKey != null)
                GameSettings.THROTTLE_FULL = rememberThrottleFullKey;
            if (rememberCameraResetKey != null)
                GameSettings.CAMERA_RESET = rememberCameraResetKey;
            if (rememberCameraModeKey != null)
                GameSettings.CAMERA_MODE = rememberCameraModeKey;
            if (rememberCameraViewKey != null)
                GameSettings.CAMERA_NEXT = rememberCameraViewKey;
        }

        private void ProcessKeyStrokes()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // Unity handles some keys in a particular way
                // e.g. Keypad7 is mapped to 0xffb7 instead of 0x37
                var c = (char)(e.character & 0x007f);

                // command sequences
                if (e.keyCode == KeyCode.C && e.control) // Ctrl+C
                {
                    SpecialKey(kOSKeys.BREAK);
                    consumeEvent = true;
                    return;
                }
                if (e.keyCode == KeyCode.X && e.control && e.shift) // Ctrl+Shift+X
                {
                    consumeEvent = true;
                    return;
                }

                if (0x20 <= c && c < 0x7f) // printable characters
                {
                    Type(c);
                    consumeEvent = true;
                }
                else if (e.keyCode != KeyCode.None) 
                {
                    Keydown(e.keyCode);
                    consumeEvent = true;
                }
            }
        }

        private void Keydown(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Break:      SpecialKey(kOSKeys.BREAK); break;
                    case KeyCode.F1:         SpecialKey(kOSKeys.F1);    break;
                    case KeyCode.F2:         SpecialKey(kOSKeys.F2);    break;
                    case KeyCode.F3:         SpecialKey(kOSKeys.F3);    break;
                    case KeyCode.F4:         SpecialKey(kOSKeys.F4);    break;
                    case KeyCode.F5:         SpecialKey(kOSKeys.F5);    break;
                    case KeyCode.F6:         SpecialKey(kOSKeys.F6);    break;
                    case KeyCode.F7:         SpecialKey(kOSKeys.F7);    break;
                    case KeyCode.F8:         SpecialKey(kOSKeys.F8);    break;
                    case KeyCode.F9:         SpecialKey(kOSKeys.F9);    break;
                    case KeyCode.F10:        SpecialKey(kOSKeys.F10);   break;
                    case KeyCode.F11:        SpecialKey(kOSKeys.F11);   break;
                    case KeyCode.F12:        SpecialKey(kOSKeys.F12);   break;
                    case KeyCode.UpArrow:    SpecialKey(kOSKeys.UP);    break;
                    case KeyCode.DownArrow:  SpecialKey(kOSKeys.DOWN);  break;
                    case KeyCode.LeftArrow:  SpecialKey(kOSKeys.LEFT);  break;
                    case KeyCode.RightArrow: SpecialKey(kOSKeys.RIGHT); break;
                    case KeyCode.Home:       SpecialKey(kOSKeys.HOME);  break;
                    case KeyCode.End:        SpecialKey(kOSKeys.END);   break;
                    case KeyCode.Delete:     SpecialKey(kOSKeys.DEL);   break;
                    case KeyCode.PageUp:     SpecialKey(kOSKeys.PGUP);  break;
                    case KeyCode.PageDown:   SpecialKey(kOSKeys.PGDN);  break;

                    case (KeyCode.Backspace):
                    Type((char)8);
                    break;

                    case (KeyCode.KeypadEnter):
                    case (KeyCode.Return):
                    Type('\r');
                    break;

                    case (KeyCode.Tab):
                    Type('\t');
                    break;
            }
        }

        private void Type(char ch)
        {
            if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            {
                processor_shares[current_processor_id].Interpreter.Type(ch);
            }
        }

        private void SpecialKey(kOSKeys key)
        {
            if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            {
                processor_shares[current_processor_id].Interpreter.SpecialKey(key);
            }
        }
    }
}