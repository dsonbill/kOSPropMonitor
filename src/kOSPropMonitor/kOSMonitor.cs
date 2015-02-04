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
        public int keyboardButton = 4;
        [KSPField]
        public int processorSelectorButton = 3;
        [KSPField]
        public int consoleButton = 2;

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
        private string response = "No Activity";
        private bool isPowered = false;


        //kOS Processor Variables
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
            if (initialized)
            {
                if (consumeEvent)
                {
                    consumeEvent = false;
                    Event.current.Use();
                }

                isPowered = processor_shares[current_processor_id].Window.IsPowered;

                if (isPowered)
                {
                    currentTextTint = textTint;
                }
                else
                {
                    currentTextTint = textTintUnpowered;
                }

                if (!isPowered && isLocked || !consoleIsOpen && isLocked)
                {
                    Unlock();
                }

                BufferConsole();

                if (consoleIsOpen)
                {
                    if (processor_shares[current_processor_id].Screen.ColumnCount != consoleWidth || processor_shares[current_processor_id].Screen.RowCount != consoleHeight)
                    {
                        processor_shares[current_processor_id].Screen.SetSize(consoleHeight, consoleWidth);
                    }
                    response = consoleBuffer;
                }
            }
            cursorBlinkTime += Time.deltaTime;

            if (cursorBlinkTime > 1) cursorBlinkTime -= 1;
        }

        public void Initialize(int screenWidth, int screenHeight)
        {
            vessel_processors = GetProcessorList();
            processor_shares = new List<SharedObjects>();

            foreach (kOSProcessor kos_processor in vessel_processors)
            {
                UnityEngine.Debug.Log("kOSPropMonitor Found A Processor! Beginning Registration...");

                //Register the kOSProcessor's SharedObjects
                processor_shares.Add(GetProcessorShared(kos_processor));
                UnityEngine.Debug.Log("kOSPropMonitor Registered Processor Share");

                //Set the screen size. In the future, either save the size and set it back
                //or standardize to what kOS uses for it's terminal (probably a good idea for portability!)
                //MAYBE WE DON'T NEED TO?!
                //kos_processor.Screen.SetSize(consoleHeight, consoleWidth);

            }


            UnityEngine.Debug.Log("kOSPropMonitor Initialized!");
            initialized = true;
        }

        public string ContentProcessor(int screenWidth, int screenHeight)
        {
            if (!initialized)
            {
                if(this.vessel != null)
                    Initialize(screenWidth, screenHeight);
            }

            if (isLocked)
            {
                if (processor_shares[current_processor_id] != null)
                {
                    ProcessKeyStrokes();
                }
            }

            return response;
        }

        public void ButtonProcessor(int buttonID)
        {
            //Keyboard input lock button
            if (buttonID == keyboardButton && consoleIsOpen)
            {
                ToggleLock();
            }


            //Very simple kOSProcessor cycler. Should probably improve.
            else if (buttonID == processorSelectorButton)
            {

                response = "";
                
                current_processor_id++;

                if (current_processor_id == vessel_processors.Count)
                {
                    current_processor_id = 0;
                }

                int processor_count = 0;
                foreach (kOSProcessor kos_processor in vessel_processors)
                {
                    if (kos_processor == vessel_processors[current_processor_id])
                    {
                        response += "kOS Processor [#009900]" + current_processor_id + "[#FFFFFF]" + System.Environment.NewLine;
                    }
                    else
                    {
                        response += "kOS Processor " + processor_count + System.Environment.NewLine;
                    }
                    processor_count++;
                }

            }


            //Opens the console
            else if (buttonID == consoleButton)
            {
                //SetCurrentProcessor(GetProcessorShared(vessel_processors[current_processor_id]));
                //ResizeScreenBuffer(consoleWidth, consoleHeight); //Doesn't work.
                consoleIsOpen = true;
            }


            //Print button ID on unassigned
            //else
            //{
            //  response = "Unassigned Button ID: " + buttonID + System.Environment.NewLine + "Newline Test";
            //}


            //Close Console on all buttons but consoleButton and keyboardButton
            if (consoleIsOpen && buttonID != consoleButton && buttonID !=keyboardButton)
            {
                consoleIsOpen = false;
            }
        }


        //kOS-Utilities
        public SharedObjects GetProcessorShared(kOSProcessor processor)
        {
            FieldInfo sharedField = typeof(kOSProcessor).GetField("shared", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
            var proc_shared = sharedField.GetValue(processor);
            return (SharedObjects)proc_shared;
        }

        //Not Needed
        //public void SetCurrentProcessor(SharedObjects curr_proc)
        //{
        //  current_processor = curr_proc;
        //}

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