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
        private string response = "Console Standing By";
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
        private IScreenSnapShot mostRecentScreen;
        private IScreenSnapShot previousScreen;
        private DateTime lastBufferGet;

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
            if (initialized) {
                
                //Process keystrokes
                if (isLocked) {
                    ProcessKeyStrokes ();
                }
                
                //Unlock if console is not open, or if the selected console is not powered.
                if (!isPowered && isLocked || !consoleIsOpen && isLocked) {
                    Unlock ();
                }
                
                //Copy the ScreenBuffer to the consoleBuffer
                GetNewestBuffer ();
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

            if (!processorIsInstalled) {
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

        public Dictionary<string, kOS.Function.FunctionBase> GetFunctionDictionary(SharedObjects share)
        {
            FieldInfo functionsField = typeof(kOS.Function.FunctionManager).GetField("functions", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
            object manager_functions = functionsField.GetValue(share.FunctionManager);

            return (Dictionary<string, kOS.Function.FunctionBase>)manager_functions;
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
        public void GetNewestBuffer()
        {
            DateTime newTime = DateTime.Now;

            // Throttle it back so the faster Update() rates don't cause pointlessly repeated work:
            // Needs to be no faster than the fastest theoretical typist or script might change the view.
            if (newTime > lastBufferGet + TimeSpan.FromMilliseconds(50)) // = 1/20th second.
            {
                mostRecentScreen = new ScreenSnapShot(processor_shares[current_processor_id].Screen);
                lastBufferGet = newTime;
            }
        }

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


                List<IScreenBufferLine> buffer = mostRecentScreen.Buffer;

                int rowsToPaint = System.Math.Min (consoleHeight, buffer.Count);

                consoleBuffer = "";

                for (int row = 0; row < rowsToPaint; row++)
                {

                    IScreenBufferLine lineBuffer = buffer[row];
                    string line = "";

                    for (int column = 0; column < lineBuffer.Length; column++)
                    {
                        if (column == processor_shares[current_processor_id].Screen.CursorColumnShow && row == processor_shares[current_processor_id].Screen.CursorRowShow)
                        {
                            line += cursor;
                        }

                        line += lineBuffer[column];
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
                    SpecialKey((char)kOSKeys.BREAK);
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
                case KeyCode.Break:      SpecialKey((char)kOSKeys.BREAK); break;
                case KeyCode.F1:         SpecialKey((char)kOSKeys.F1);    break;
                case KeyCode.F2:         SpecialKey((char)kOSKeys.F2);    break;
                case KeyCode.F3:         SpecialKey((char)kOSKeys.F3);    break;
                case KeyCode.F4:         SpecialKey((char)kOSKeys.F4);    break;
                case KeyCode.F5:         SpecialKey((char)kOSKeys.F5);    break;
                case KeyCode.F6:         SpecialKey((char)kOSKeys.F6);    break;
                case KeyCode.F7:         SpecialKey((char)kOSKeys.F7);    break;
                case KeyCode.F8:         SpecialKey((char)kOSKeys.F8);    break;
                case KeyCode.F9:         SpecialKey((char)kOSKeys.F9);    break;
                case KeyCode.F10:        SpecialKey((char)kOSKeys.F10);   break;
                case KeyCode.F11:        SpecialKey((char)kOSKeys.F11);   break;
                case KeyCode.F12:        SpecialKey((char)kOSKeys.F12);   break;
                case KeyCode.UpArrow:    SpecialKey((char)kOSKeys.UP);    break;
                case KeyCode.DownArrow:  SpecialKey((char)kOSKeys.DOWN);  break;
                case KeyCode.LeftArrow:  SpecialKey((char)kOSKeys.LEFT);  break;
                case KeyCode.RightArrow: SpecialKey((char)kOSKeys.RIGHT); break;
                case KeyCode.Home:       SpecialKey((char)kOSKeys.HOME);  break;
                case KeyCode.End:        SpecialKey((char)kOSKeys.END);   break;
                case KeyCode.Delete:     SpecialKey((char)kOSKeys.DEL);   break;
                case KeyCode.PageUp:     SpecialKey((char)kOSKeys.PGUP);  break;
                case KeyCode.PageDown:   SpecialKey((char)kOSKeys.PGDN);  break;

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

        private void SpecialKey(char key)
        {
            switch (key) {
                case (char)kOSKeys.UP

            }
            if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            {
                processor_shares[current_processor_id].Interpreter.SpecialKey(key);
            }
        }
    }


    //kOS Functions
    public class FunctionTestNewFunction : kOS.Function.FunctionBase
    {
        public override void Execute(SharedObjects shared)
        {
            shared.Screen.Print ("Function called succesfully!");
        }
    }
}