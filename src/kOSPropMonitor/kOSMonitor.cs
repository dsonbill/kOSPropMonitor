using System;
using System.IO;
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
        public int processorSelectorUpButton = 7;
        [KSPField]
        public int processorSelectorDownButton = 8;
        [KSPField]
        public int connectButton = 4;
        [KSPField]
        public int toggleProcessorPowerButton = 9;

        //Top Buttons
        [KSPField]
        public int topButton0 = 17;
        [KSPField]
        public int topButton1 = 18;
        [KSPField]
        public int topButton2 = 19;
        [KSPField]
        public int topButton3 = 20;
        [KSPField]
        public int topButton4 = 21;
        [KSPField]
        public int topButton5 = 22;
        [KSPField]
        public int topButton6 = 23;

        //Bottom Buttons
        [KSPField]
        public int bottomButton0 = 10;
        [KSPField]
        public int bottomButton1 = 11;
        [KSPField]
        public int bottomButton2 = 12;
        [KSPField]
        public int bottomButton3 = 13;
        [KSPField]
        public int bottomButton4 = 14;
        [KSPField]
        public int bottomButton5 = 15;
        [KSPField]
        public int bottomButton6 = 16;

        //Directional Buttons
        [KSPField]
        public int upButton = 0;
        [KSPField]
        public int downButton = 1;
        [KSPField]
        public int leftButton = 6;
        [KSPField]
        public int rightButton = 5;

        //Enter and Cancel
        [KSPField]
        public int enterButton = 2;
        [KSPField]
        public int cancelButton = 3;

        //kOS Fields
        [KSPField]
        public string textTint = "[#009900ff]";
        [KSPField]
        public string textTintUnpowered = "[#ffffff3e]";
        [KSPField]
        public string textTintButtonOn = "[#009900ff]";
        [KSPField]
        public string textTintButtonOff = "[#ffffff3e]";
        [KSPField]
        public string textTintLightOn = "[#009900ff]";
        [KSPField]
        public string textTintLightOff = "[#ffffff3e]";
        [KSPField]
        public int consoleWidth = 40;
        [KSPField]
        public int consoleHeight = 20;

        //Terminal Fields
        [KSPField]
        public string template = "";
        [KSPField]
        public string buttonSide = "##########";
        [KSPField]
        public string buttonSideSmall = "#";
        [KSPField]
        public string buttonEmptyLabel = "        ";
        [KSPField]
        public string lightSide = "##########";
        [KSPField]
        public string lightSideSmall = "#";
        [KSPField]
        public string lightEmptyLabel = "        ";

        //General State Variables
        private bool initialized = false;
        private string response = "kOS Terminal Standing By";
        private bool isPowered = false;
        private int lastPartCount = 0;
        private Dictionary<string, string> response_formats;
        private Dictionary<string, bool> buttonStates;
        private Dictionary<string, string> buttonLabels;
        private Dictionary<int, string> buttonID;
        private Dictionary<string, bool> lightStates;
        private Dictionary<string, string> lightLabels;

        //kOS Processor Variables
        private bool processorIsInstalled;
        private int current_processor_id = 0;
        private List<SharedObjects> processor_shares;
        private List<kOSProcessor> processors;

        //kOS Terminal Variables
        private bool consumeEvent;
        private const string CONTROL_LOCKOUT = "kOSPropMonitor";
        private bool isLocked = false;

        //private string consoleBuffer;
        private float cursorBlinkTime;
        private string currentTextTint;
        private IScreenSnapShot mostRecentScreen;
        private DateTime lastBufferGet;
        private int screenWidth;
        private int screenHeight;

        //Keyboard Memory Variables
        private KeyBinding rememberThrottleCutoffKey;
        private KeyBinding rememberThrottleFullKey;
        private KeyBinding rememberCameraResetKey;
        private KeyBinding rememberCameraModeKey;
        private KeyBinding rememberCameraViewKey;

        public override void OnUpdate()
        {
            if (processorIsInstalled)
            {
                if (initialized)
                {
                    //Check for destruction
                    if (this.vessel.parts.Count != lastPartCount)
                    {
                        Initialize(screenWidth, screenHeight);
                    }

                    //Set power state from SharedObjects
                    isPowered = processor_shares[current_processor_id].Window.IsPowered;

                    //Set Power State Logic
                    if (isPowered)
                    {
                        currentTextTint = textTint;
                    }
                    else
                    {
                        currentTextTint = textTintUnpowered;
                        if (isLocked)
                        {
                            ToggleLock();
                        }
                    }

                    //Process keystrokes
                    if (isLocked)
                    {
                        ProcessKeyEvents();
                    }

                    //Buffer the console and Processor List
                    GetNewestBuffer();
                    BufferConsole();
                    BufferProcessorList();

                    //Set screen size if needed
                    if (processor_shares[current_processor_id].Screen.ColumnCount != consoleWidth || processor_shares[current_processor_id].Screen.RowCount != consoleHeight)
                    {
                        processor_shares[current_processor_id].Screen.SetSize(consoleHeight, consoleWidth);
                    }

                    //Format Response
                    response = Utilities.Format(template, response_formats);
                    SetButtons();
                    SetLights();

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
            else if (isLocked)
            {
                Unlock();
            }
        }

        public void Initialize(int screenWidth, int screenHeight)
        {
            //Set Processor Installed Flag
            processorIsInstalled = false;

            //Response Dictionary
            response_formats = new Dictionary<string, string>();

            //Register kOSProcessors
            processors = GetProcessorList();

            //Instantiate SharedObjects List
            processor_shares = GetProcessorShares();

            //Set Vessel Part Cound
            lastPartCount = this.vessel.parts.Count;

            //Add Getters and Setters
            AddGettersAndSetters();

            //Single-Init Actions
            if (!initialized)
            {
                //Set Key Binding Memory
                rememberCameraResetKey = GameSettings.CAMERA_RESET;
                rememberCameraModeKey = GameSettings.CAMERA_MODE;
                rememberCameraViewKey = GameSettings.CAMERA_NEXT;
                rememberThrottleCutoffKey = GameSettings.THROTTLE_CUTOFF;
                rememberThrottleFullKey = GameSettings.THROTTLE_FULL;

                ReadTemplate();

                buttonLabels = new Dictionary<string, string>();
                buttonLabels["buttonT0"] = buttonEmptyLabel;
                buttonLabels["buttonT1"] = buttonEmptyLabel;
                buttonLabels["buttonT2"] = buttonEmptyLabel;
                buttonLabels["buttonT3"] = buttonEmptyLabel;
                buttonLabels["buttonT4"] = buttonEmptyLabel;
                buttonLabels["buttonT5"] = buttonEmptyLabel;
                buttonLabels["buttonT6"] = buttonEmptyLabel;
                buttonLabels["buttonB0"] = buttonEmptyLabel;
                buttonLabels["buttonB1"] = buttonEmptyLabel;
                buttonLabels["buttonB2"] = buttonEmptyLabel;
                buttonLabels["buttonB3"] = buttonEmptyLabel;
                buttonLabels["buttonB4"] = buttonEmptyLabel;
                buttonLabels["buttonB5"] = buttonEmptyLabel;
                buttonLabels["buttonB6"] = buttonEmptyLabel;

                buttonStates = new Dictionary<string, bool>();
                buttonStates["buttonT0"] = false;
                buttonStates["buttonT1"] = false;
                buttonStates["buttonT2"] = false;
                buttonStates["buttonT3"] = false;
                buttonStates["buttonT4"] = false;
                buttonStates["buttonT5"] = false;
                buttonStates["buttonT6"] = false;
                buttonStates["buttonB0"] = false;
                buttonStates["buttonB1"] = false;
                buttonStates["buttonB2"] = false;
                buttonStates["buttonB3"] = false;
                buttonStates["buttonB4"] = false;
                buttonStates["buttonB5"] = false;
                buttonStates["buttonB6"] = false;

                buttonID = new Dictionary<int, string>();
                buttonID[topButton0] = "T0";
                buttonID[topButton1] = "T1";
                buttonID[topButton2] = "T2";
                buttonID[topButton3] = "T3";
                buttonID[topButton4] = "T4";
                buttonID[topButton5] = "T5";
                buttonID[topButton6] = "T6";
                buttonID[bottomButton0] = "B0";
                buttonID[bottomButton1] = "B1";
                buttonID[bottomButton2] = "B2";
                buttonID[bottomButton3] = "B3";
                buttonID[bottomButton4] = "B4";
                buttonID[bottomButton5] = "B5";
                buttonID[bottomButton6] = "B6";

                lightLabels = new Dictionary<string, string>();
                lightLabels["lightT0"] = buttonEmptyLabel;
                lightLabels["lightT1"] = buttonEmptyLabel;
                lightLabels["lightT2"] = buttonEmptyLabel;
                lightLabels["lightT3"] = buttonEmptyLabel;
                lightLabels["lightT4"] = buttonEmptyLabel;
                lightLabels["lightT5"] = buttonEmptyLabel;
                lightLabels["lightT6"] = buttonEmptyLabel;
                lightLabels["lightB0"] = buttonEmptyLabel;
                lightLabels["lightB1"] = buttonEmptyLabel;
                lightLabels["lightB2"] = buttonEmptyLabel;
                lightLabels["lightB3"] = buttonEmptyLabel;
                lightLabels["lightB4"] = buttonEmptyLabel;
                lightLabels["lightB5"] = buttonEmptyLabel;
                lightLabels["lightB6"] = buttonEmptyLabel;


                lightStates = new Dictionary<string, bool>();
                lightStates["lightT0"] = false;
                lightStates["lightT1"] = false;
                lightStates["lightT2"] = false;
                lightStates["lightT3"] = false;
                lightStates["lightT4"] = false;
                lightStates["lightT5"] = false;
                lightStates["lightT6"] = false;
                lightStates["lightB0"] = false;
                lightStates["lightB1"] = false;
                lightStates["lightB2"] = false;
                lightStates["lightB3"] = false;
                lightStates["lightB4"] = false;
                lightStates["lightB5"] = false;
                lightStates["lightB6"] = false;

                initialized = true;
            }

            UnityEngine.Debug.Log("kOSPropMonitor Initialized!");
        }

        public string ContentProcessor(int screenWidth, int screenHeight)
        {
            //Check for initialization
            if (!initialized)
            {
                if (this.vessel != null)
                {
                    Initialize(screenWidth, screenHeight);
                    this.screenWidth = screenWidth;
                    this.screenHeight = screenHeight;
                }
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
                //A much better kOSProcessor cycler.
                if (buttonID == processorSelectorUpButton)
                {
                    current_processor_id--;

                    if (current_processor_id == -1)
                    {
                        current_processor_id = processors.Count - 1;
                    }

                    if (isLocked)
                    {
                        ToggleLock();
                    }
                }
                else if (buttonID == processorSelectorDownButton)
                {
                    current_processor_id++;

                    if (current_processor_id == processors.Count)
                    {
                        current_processor_id = 0;
                    }

                    if (isLocked)
                    {
                        ToggleLock();
                    }
                }


                //Connect Button
                else if (buttonID == connectButton)
                {
                    ToggleLock();
                }


                //Power Toggle Button
                else if (buttonID == toggleProcessorPowerButton)
                {
                    if (processors[current_processor_id] != null)
                    {
                        processors[current_processor_id].TogglePower();
                    }
                }

                //Programmable Buttons
                ProcessProgramButton(buttonID);
            }
        }


        //Button Utilities
        void ProcessProgramButton(int ID)
        {
            if (buttonID.ContainsKey(ID))
            {
                buttonStates["button" + buttonID[ID]] = !buttonStates["button" + buttonID[ID]];
            }
        }


        //kOS-Utilities
        List<SharedObjects> GetProcessorShares()
        {
            List<SharedObjects> solist = new List<SharedObjects>();
            foreach (kOSProcessor kos_processor in processors)
            {
                //Set Processor Installed Flag
                processorIsInstalled = true;

                //Register the kOSProcessor's SharedObjects
                FieldInfo sharedField = typeof(kOSProcessor).GetField("shared", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
                var proc_shared = sharedField.GetValue(kos_processor);
                solist.Add((SharedObjects)proc_shared);
                //UnityEngine.Debug.Log("kOSPropMonitor Registered Processor Share");
            }
            return solist;
        }

        List<kOSProcessor> GetProcessorList()
        {
            return this.vessel.FindPartModulesImplementing<kOSProcessor>();
        }


        //Printing
        void ReadTemplate()
        {
            string FILE_NAME = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), template);
            if (!File.Exists(FILE_NAME))
            {
                UnityEngine.Debug.Log("kOSPropMonitor: Template {0} does not exist." + FILE_NAME);
                return;
            }
            using (StreamReader sr = File.OpenText(FILE_NAME))
            {
                template = "";
                String input;
                while ((input = sr.ReadLine()) != null)
                {
                    template += input + Environment.NewLine;
                }
                sr.Close();
            }
        }

        void GetNewestBuffer()
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

        void BufferConsole()
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

                //consoleBuffer = "";

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

                    response_formats["l" + (row + 1)] = currentTextTint + line + "[#ffffffff]";
                }
            }
        }

        void BufferProcessorList()
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

            for (int processor_entry_count = 0; processor_entry_count < processors.Count / 4 + 1; processor_entry_count++)
            {
                int current_position = processor_entry_count * 4;

                if (current_processor_id == current_position || current_processor_id == current_position + 1 ||
                    current_processor_id == current_position + 2 || current_processor_id == current_position + 3)
                {
                    for (int lrange = 0; lrange < 4; lrange++)
                    {
                        if (current_position + lrange >= processors.Count)
                        {
                            response_formats["kCPU" + (lrange + 1)] = "       ";
                            continue;
                        }

                        isPowered = processor_shares[current_position + lrange].Window.IsPowered;

                        if (isPowered)
                        {
                            currentTextTint = textTint;
                        }
                        else
                        {
                            currentTextTint = textTintUnpowered;
                        }

                        if (current_position + lrange == current_processor_id)
                        {
                            response_formats["kCPU" + (lrange + 1)] = currentTextTint + "kCPU  " + (current_position + lrange) + "[#FFFFFF]";
                        }
                        else
                        {
                            response_formats["kCPU" + (lrange + 1)] = "kCPU  " + currentTextTint + (current_position + lrange) + "[#FFFFFF]";
                        }
                    }
                    break;
                }
            }
        }

        void AddGettersAndSetters()
        {
            foreach (SharedObjects so in processor_shares)
            {
                //Looping doesn't seem to work - I'm sure there's another way to do this, but this is simple enough

                //Top
                so.BindingMgr.AddGetter("LIGHTT0", () => lightStates["lightT0"]);
                so.BindingMgr.AddGetter("LIGHTT1", () => lightStates["lightT1"]);
                so.BindingMgr.AddGetter("LIGHTT2", () => lightStates["lightT2"]);
                so.BindingMgr.AddGetter("LIGHTT3", () => lightStates["lightT3"]);
                so.BindingMgr.AddGetter("LIGHTT4", () => lightStates["lightT4"]);
                so.BindingMgr.AddGetter("LIGHTT5", () => lightStates["lightT5"]);
                so.BindingMgr.AddGetter("LIGHTT6", () => lightStates["lightT6"]);

                so.BindingMgr.AddGetter("BUTTONT0", () => buttonStates["buttonT0"]);
                so.BindingMgr.AddGetter("BUTTONT1", () => buttonStates["buttonT1"]);
                so.BindingMgr.AddGetter("BUTTONT2", () => buttonStates["buttonT2"]);
                so.BindingMgr.AddGetter("BUTTONT3", () => buttonStates["buttonT3"]);
                so.BindingMgr.AddGetter("BUTTONT4", () => buttonStates["buttonT4"]);
                so.BindingMgr.AddGetter("BUTTONT5", () => buttonStates["buttonT5"]);
                so.BindingMgr.AddGetter("BUTTONT6", () => buttonStates["buttonT6"]);

                so.BindingMgr.AddSetter("LIGHTT0", value => lightStates["lightT0"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT1", value => lightStates["lightT1"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT2", value => lightStates["lightT2"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT3", value => lightStates["lightT3"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT4", value => lightStates["lightT4"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT5", value => lightStates["lightT5"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTT6", value => lightStates["lightT6"] = (bool)value);
                
                so.BindingMgr.AddSetter("BUTTONT0", value => buttonStates["buttonT0"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT1", value => buttonStates["buttonT1"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT2", value => buttonStates["buttonT2"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT3", value => buttonStates["buttonT3"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT4", value => buttonStates["buttonT4"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT5", value => buttonStates["buttonT5"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONT6", value => buttonStates["buttonT6"] = (bool)value);

                so.BindingMgr.AddSetter("LIGHTT0LABEL", value => lightLabels["lightT0"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT1LABEL", value => lightLabels["lightT1"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT2LABEL", value => lightLabels["lightT2"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT3LABEL", value => lightLabels["lightT3"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT4LABEL", value => lightLabels["lightT4"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT5LABEL", value => lightLabels["lightT5"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTT6LABEL", value => lightLabels["lightT6"] = (string)value);

                so.BindingMgr.AddSetter("BUTTONT0LABEL", value => buttonLabels["buttonT0"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT1LABEL", value => buttonLabels["buttonT1"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT2LABEL", value => buttonLabels["buttonT2"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT3LABEL", value => buttonLabels["buttonT3"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT4LABEL", value => buttonLabels["buttonT4"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT5LABEL", value => buttonLabels["buttonT5"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONT6LABEL", value => buttonLabels["buttonT6"] = (string)value);

                //Bottom
                so.BindingMgr.AddGetter("LIGHTB0", () => lightStates["lightB0"]);
                so.BindingMgr.AddGetter("LIGHTB1", () => lightStates["lightB1"]);
                so.BindingMgr.AddGetter("LIGHTB2", () => lightStates["lightB2"]);
                so.BindingMgr.AddGetter("LIGHTB3", () => lightStates["lightB3"]);
                so.BindingMgr.AddGetter("LIGHTB4", () => lightStates["lightB4"]);
                so.BindingMgr.AddGetter("LIGHTB5", () => lightStates["lightB5"]);
                so.BindingMgr.AddGetter("LIGHTB6", () => lightStates["lightB6"]);

                so.BindingMgr.AddGetter("BUTTONB0", () => buttonStates["buttonB0"]);
                so.BindingMgr.AddGetter("BUTTONB1", () => buttonStates["buttonB1"]);
                so.BindingMgr.AddGetter("BUTTONB2", () => buttonStates["buttonB2"]);
                so.BindingMgr.AddGetter("BUTTONB3", () => buttonStates["buttonB3"]);
                so.BindingMgr.AddGetter("BUTTONB4", () => buttonStates["buttonB4"]);
                so.BindingMgr.AddGetter("BUTTONB5", () => buttonStates["buttonB5"]);
                so.BindingMgr.AddGetter("BUTTONB6", () => buttonStates["buttonB6"]);

                so.BindingMgr.AddSetter("LIGHTB0", value => lightStates["lightB0"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB1", value => lightStates["lightB1"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB2", value => lightStates["lightB2"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB3", value => lightStates["lightB3"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB4", value => lightStates["lightB4"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB5", value => lightStates["lightB5"] = (bool)value);
                so.BindingMgr.AddSetter("LIGHTB6", value => lightStates["lightB6"] = (bool)value);

                so.BindingMgr.AddSetter("BUTTONB0", value => buttonStates["buttonB0"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB1", value => buttonStates["buttonB1"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB2", value => buttonStates["buttonB2"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB3", value => buttonStates["buttonB3"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB4", value => buttonStates["buttonB4"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB5", value => buttonStates["buttonB5"] = (bool)value);
                so.BindingMgr.AddSetter("BUTTONB6", value => buttonStates["buttonB6"] = (bool)value);

                so.BindingMgr.AddSetter("LIGHTB0LABEL", value => lightLabels["lightB0"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB1LABEL", value => lightLabels["lightB1"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB2LABEL", value => lightLabels["lightB2"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB3LABEL", value => lightLabels["lightB3"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB4LABEL", value => lightLabels["lightB4"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB5LABEL", value => lightLabels["lightB5"] = (string)value);
                so.BindingMgr.AddSetter("LIGHTB6LABEL", value => lightLabels["lightB6"] = (string)value);

                so.BindingMgr.AddSetter("BUTTONB0LABEL", value => buttonLabels["buttonB0"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB1LABEL", value => buttonLabels["buttonB1"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB2LABEL", value => buttonLabels["buttonB2"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB3LABEL", value => buttonLabels["buttonB3"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB4LABEL", value => buttonLabels["buttonB4"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB5LABEL", value => buttonLabels["buttonB5"] = (string)value);
                so.BindingMgr.AddSetter("BUTTONB6LABEL", value => buttonLabels["buttonB6"] = (string)value);
            }
        }

        void SetLights()
        {
            foreach (KeyValuePair<string, bool> kvpair in lightStates)
            {
                string color = "";
                if (kvpair.Value == true)
                {
                    color = textTintButtonOn;
                }
                else
                {
                    color = textTintButtonOff;
                }
                string sub = kvpair.Key.Substring(5);
                response = response.Replace("{lightSide" + sub + "}", (color + lightSide + "[#FFFFFF]").ToString());
                response = response.Replace("{lightSideSmall" + sub + "}", (color + lightSideSmall + "[#FFFFFF]").ToString());
                response = response.Replace("{lightLabel" + sub + "}", (lightLabels[kvpair.Key]).ToString());
            }
        }

        void SetButtons()
        {
            foreach (KeyValuePair<int, string> kvpair in buttonID)
            {
                string color = "";
                if (buttonStates["button" + kvpair.Value] == true)
                {
                    color = textTintButtonOn;
                }
                else
                {
                    color = textTintButtonOff;
                }
                response = response.Replace("{buttonSide" + kvpair.Value + "}", (color + buttonSide + "[#FFFFFF]").ToString());
                response = response.Replace("{buttonSideSmall" + kvpair.Value + "}", (color + buttonSideSmall + "[#FFFFFF]").ToString());
                response = response.Replace("{buttonLabel" + kvpair.Value + "}", (buttonLabels["button" + kvpair.Value]).ToString());
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

        void Lock()
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

        void Unlock()
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

        void Type(char command)
        {
            if (processor_shares[current_processor_id] != null && processor_shares[current_processor_id].Interpreter != null)
            {
                processor_shares[current_processor_id].Window.ProcessOneInputChar(command, null);
            }
        }
    }
}