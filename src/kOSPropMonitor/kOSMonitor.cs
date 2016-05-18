using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using kOS;
using kOS.Safe.UserIO;
using kOS.Safe.Screen;
using kOS.Module;

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
        private List<kOSProcessor> processors;

        //kOS Terminal Variables
        private bool consumeEvent;
        private const string CONTROL_LOCKOUT = "kOSPropMonitor";
        private bool isLocked = false;
        private bool consoleIsOpen = false;
        private string consoleBuffer;
        private float cursorBlinkTime;
        private string currentTextTint;
        private IScreenSnapShot mostRecentScreen;
        private DateTime lastBufferGet;

        //Keyboard Memory Variables
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
            if (processorIsInstalled)
            {
                if (initialized)
                {
                    //Set power state from SharedObjects
                    isPowered = processor_shares[current_processor_id].Window.IsPowered;

                    //Set text tinting depending on power state
                    if (isPowered)
                    {
                        currentTextTint = textTint;
                    }
                    else
                    {
                        currentTextTint = textTintUnpowered;
                    }

                    //Process keystrokes
                    if (isLocked)
                    {
                        if (processor_shares[current_processor_id] != null)
                        {
                            ProcessKeyEvents();
                        }
                    }

                    //Unlock if console is not open, or if the selected console is not powered.
                    if (!isPowered && isLocked || !consoleIsOpen && isLocked)
                    {
                        Unlock();
                    }

                    //Copy the ScreenBuffer to the consoleBuffer
                    GetNewestBuffer();
                    BufferConsole();

                    //Do console logic if open
                    if (consoleIsOpen)
                    {
                        //Set screen size if needed
                        if (processor_shares[current_processor_id].Screen.ColumnCount != consoleWidth || processor_shares[current_processor_id].Screen.RowCount != consoleHeight)
                        {
                            processor_shares[current_processor_id].Screen.SetSize(consoleHeight, consoleWidth);
                        }

                        //Set response to the consoleBuffer
                        response = consoleBuffer;
                    }

                    //Consume event - IDEK
                    if (consumeEvent)
                    {
                        consumeEvent = false;
                        Event.current.Use();
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
            processors = GetProcessorList();

            //Instantiate SharedObjects List
            processor_shares = new List<SharedObjects>();

            foreach (kOSProcessor kos_processor in processors)
            {
                //Set Processor Installed Flag
                processorIsInstalled = true;

                UnityEngine.Debug.Log("kOSPropMonitor Found A Processor! Beginning Registration...");

                //Register the kOSProcessor's SharedObjects
                processor_shares.Add(GetProcessorShared(kos_processor));
                UnityEngine.Debug.Log("kOSPropMonitor Registered Processor Share");
            }

            // Set Key Binding Memory - these are safe from kOS up here
            rememberCameraResetKey = GameSettings.CAMERA_RESET;
            rememberCameraModeKey = GameSettings.CAMERA_MODE;
            rememberCameraViewKey = GameSettings.CAMERA_NEXT;
            rememberThrottleCutoffKey = GameSettings.THROTTLE_CUTOFF;
            rememberThrottleFullKey = GameSettings.THROTTLE_FULL;

            // List the processors if there are any
            if (processorIsInstalled)
            {
                PrintProcessorList();
            }

            UnityEngine.Debug.Log("kOSPropMonitor Initialized!");
            initialized = true;
        }

        public string ContentProcessor(int screenWidth, int screenHeight)
        {
            //Check for initialization
            if (!initialized)
            {
                if (this.vessel != null)
                    Initialize(screenWidth, screenHeight);
            }

            if (!processorIsInstalled)
            {
                response = "kOS is not installed!";
            }

            // Everything flows through here
            return response;
        }

        public void ButtonProcessor(int buttonID)
        {
            if (processorIsInstalled)
            {

                //A better kOSProcessor cycler. *Might* Improve a bit.
                if (buttonID == processorSelectorUpButton)
                {
                    current_processor_id--;

                    if (current_processor_id == -1)
                    {
                        current_processor_id = processors.Count - 1;
                    }

                    PrintProcessorList();

                }
                else if (buttonID == processorSelectorDownButton)
                {
                    current_processor_id++;

                    if (current_processor_id == processors.Count)
                    {
                        current_processor_id = 0;
                    }

                    PrintProcessorList();
                }


                //Opens the console
                else if (buttonID == openConsoleButton)
                {
                    consoleIsOpen = !consoleIsOpen;
                    if (!consoleIsOpen)
                    {
                        if (processorIsInstalled)
                        {
                            PrintProcessorList();
                        }
                    }
                }


                //Power Toggle Button
                else if (buttonID == toggleProcessorPowerButton)
                {
                    if (processors[current_processor_id] != null)
                    {
                        processors[current_processor_id].TogglePower();

                        if (!consoleIsOpen)
                        {
                            PrintProcessorList();
                        }
                    }
                }


                //Keyboard input lock button
                else if (buttonID == toggleKeyboardButton && consoleIsOpen)
                {
                    ToggleLock();
                }

                //Allow usage of toggleKeyboardButton and toggleProcessorPowerButton without closing the console
                if (consoleIsOpen && buttonID != openConsoleButton && buttonID != toggleKeyboardButton && buttonID != toggleProcessorPowerButton)
                {
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

        public List<kOSProcessor> GetProcessorList()
        {
            return this.vessel.FindPartModulesImplementing<kOSProcessor>();
        }

        public void PrintProcessorList()
        {
            isPowered = processor_shares[current_processor_id].Window.IsPowered;

            if (isPowered)
            {
                currentTextTint = textTint;
            }
            else
            {
                currentTextTint = textTintUnpowered;
            }

            response = "Processor List:" + Environment.NewLine;
            for (int processor_count = 0; processor_count < processors.Count; processor_count++)
            {
                if (processor_count == current_processor_id)
                {
                    response += "kOS Processor " + currentTextTint + processor_count + "[#FFFFFF] <--" + Environment.NewLine;
                }
                else
                {
                    response += "kOS Processor " + processor_count + Environment.NewLine;
                }
            }
        }

        public void ToggleOpen()
        {
            if (!consoleIsOpen)
                consoleIsOpen = true;
            else
                consoleIsOpen = false;
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

                int rowsToPaint = System.Math.Min(consoleHeight, buffer.Count);

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
                    consumeEvent = true;
                    return;
                }
                // Command used to be Control-shift-X, now we don't care if shift is down aymore, to match the telnet expereince
                // where there is no such thing as "uppercasing" a control char.
                if ((e.keyCode == KeyCode.X && e.control) ||
                    (e.keyCode == KeyCode.D && e.control) // control-D to match the telnet experience
                   )
                {
                    Type((char)0x000d);
                    consumeEvent = true;
                    return;
                }

                if (e.keyCode == KeyCode.A && e.control)
                {
                    Type((char)0x0001);
                    consumeEvent = true;
                    return;
                }

                if (e.keyCode == KeyCode.E && e.control)
                {
                    Type((char)0x0005);
                    consumeEvent = true;
                    return;
                }

                if (0x20 <= c && c < 0x7f) // printable characters
                {
                    Type(c);
                    consumeEvent = true;
                    cursorBlinkTime = 0.0f; // Don't blink while the user is still actively typing.
                }

                else if (e.keyCode != KeyCode.None)
                {
                    consumeEvent = true;
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
                        default: consumeEvent = false; break;
                    }
                    cursorBlinkTime = 0.0f;// Don't blink while the user is still actively typing.
                }
            }
        }

        private void Type(char command)
        {
            if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            {
                processor_shares[current_processor_id].Window.ProcessOneInputChar(command, null);
            }
        }
    }
}