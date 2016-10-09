using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using kOS;
using kOS.Safe.Screen;
using kOS.Module;
using JsonFx.Json;

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
        [KSPField]
        public string multiFunctionButtons = "17,18,19,20,21,22,23,10,11,12,13,14,15,16";

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

        //Terminal Fields
        [KSPField]
        public int flagCount = 14;
        [KSPField]
        public string template = "";
        [KSPField]
        public string replacements = "";
        [KSPField]
        public string buttonSide = "##########";
        [KSPField]
        public string buttonSideSmall = "#";
        [KSPField]
        public string buttonEmptyLabel = "        ";
        [KSPField]
        public string flagSide = "##########";
        [KSPField]
        public string flagSideSmall = "#";
        [KSPField]
        public string flagEmptyLabel = "        ";
        [KSPField]
        public string textTint = "[#009900ff]";
        [KSPField]
        public string coloredTextTint = "[#ffffffff]";
        [KSPField]
        public string textTintUnpowered = "[#ffffff3e]";
        [KSPField]
        public string textTintButtonOn = "[#009900ff]";
        [KSPField]
        public string textTintButtonOff = "[#ffffffff]";
        [KSPField]
        public string textTintFlagOn = "[#009900ff]";
        [KSPField]
        public string textTintFlagOff = "[#000000]";
        [KSPField]
        public string keyboardActiveLabel = "KBRD";
        [KSPField]
        public string keyboardActiveTint = "[#FFF72B]";
        [KSPField]
        public string keyboardInactiveTint = "[#000000]";
        [KSPField]
        public int consoleWidth = 40;
        [KSPField]
        public int consoleHeight = 20;
        [KSPField]
        public bool longGuid = false;
        [KSPField]
        public bool coloredTerminal = true;
        [KSPField]
        public bool replacementsToUpper = false;

        //General Variables
        private bool initialized = false;
        private kPMVesselMonitors vt;
        private int lastPartCount = 0;
        private List<int> multiFunctionButtonsPOS;
        private char[] delimiterChars = { ' ', ',', '.', ':'};
        private Dictionary<string, string> response_formats;
        private Dictionary<string, object> replacement_formats;
        private Guid guid;

        //kOS Processor Variables
        private bool isPowered = false;
        private bool processorIsInstalled;
        private int current_processor_id = 0;
        private List<SharedObjects> processor_shares;
        private List<kOSProcessor> processors;

        //kOS Terminal Variables
        private bool consumeEvent;
        private bool isLocked = false;
        private string unformattedTemplate;
        private int monitorIndex = 0;
        public bool upButtonState = false;
        public bool downButtonState = false;
        public bool leftButtonState = false;
        public bool rightButtonState = false;
        public bool enterButtonState = false;
        public bool cancelButtonState = false;
        private float cursorBlinkTime;
        private string currentTextTint
        {
            get
            {
                if (isPowered)
                {
                    if (coloredTerminal) return coloredTextTint;
                    return textTint;
                }
                return textTintUnpowered;
            }
        }
        private string response
        {
            get
            {
                if (processorIsInstalled)
                {
                    if (response_formats != null && replacement_formats != null)
                    {
                        string output = Utilities.Format(unformattedTemplate, response_formats);
                        if (coloredTerminal) output = Utilities.FreeFormat(output, replacement_formats, replacementsToUpper).Replace("{COLOR}", currentTextTint);
                        output = SetButtons(output);
                        output = SetFlags(output);
                        return output;
                    }
                    return "kOS Terminal Standing By";
                }
                return "Processor Not Installed!";
            }
        }
        private IScreenSnapShot mostRecentScreen;
        private int screenWidth = 0;
        private int screenHeight = 0;

        public override void OnUpdate()
        {
            if (processorIsInstalled)
            {
                if (initialized)
                {
                    //Check for destruction or separation
                    if (this.vessel.parts.Count != lastPartCount)
                    {
                        Debug.Log("kPM: Ship Reconfiguring");
                        Initialize(screenWidth, screenHeight);
                        return;
                    }

                    //Set power state from SharedObjects
                    isPowered = processor_shares[current_processor_id].Window.IsPowered;

                    //Set Power State Logic
                    if (!isPowered)
                    {
                        if (isLocked)
                        {
                            ToggleLock();
                        }
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
            else
            {
                if (this.vessel.parts.Count != lastPartCount)
                {
                    Debug.Log("kPM: Ship Reconfiguring");
                    Initialize(screenWidth, screenHeight);
                    return;
                }
                if (isLocked)
                {
                    ToggleLock();
                }
            } 
        }

        public void Initialize(int screenWidth, int screenHeight)
        {
            //Set Processor Installed Flag
            processorIsInstalled = false;

            //Set Current Processor to 0
            current_processor_id = 0;

            //Unlock if locked
            if (isLocked) ToggleLock();

            //Register kOSProcessors
            processors = GetProcessorList();

            //Get SharedObjects
            processor_shares = new List<SharedObjects>();
            foreach (kOSProcessor processor in processors)
            {
                //Set Processor Installed Flag
                processorIsInstalled = true;

                //Register the kOSProcessor's SharedObjects
                processor_shares.Add(GetProcessorShare(processor));
            }

            //Return Early If No Processor
            if (!processorIsInstalled) return;

            //Register Vessel
            kPMCore.fetch.RegisterVessel(this.vessel.id);

            //Get Vessel Track
            vt = kPMCore.fetch.GetVesselMonitors(this.vessel.id);

            //Set Vessel Part Cound
            lastPartCount = this.vessel.parts.Count;

            //Single-Init Actions
            if (!initialized)
            {
                //Set Index
                monitorIndex = vt.monitors.Count;

                //Create or Get GUID
                if (vt.registeredMonitors.Count > monitorIndex) guid = vt.registeredMonitors[monitorIndex];
                else guid = Guid.NewGuid();

                //Register Monitor
                vt.RegisterMonitor(guid);

                //Split Multi-Function Buttons String
                multiFunctionButtonsPOS = new List<int>();
                foreach(string id in multiFunctionButtons.Split(delimiterChars))
                {
                    int id_int;
                    if (Int32.TryParse(id, out id_int))
                    {
                        multiFunctionButtonsPOS.Add(id_int);
                    }
                }

                //Register Buttons and Flags
                if (!vt.buttonLabels.ContainsKey(monitorIndex))
                {
                    vt.buttonLabels[monitorIndex] = new Dictionary<int, string>();
                    vt.buttonStates[monitorIndex] = new Dictionary<int, bool>();
                    vt.flagLabels[monitorIndex] = new Dictionary<int, string>();
                    vt.flagStates[monitorIndex] = new Dictionary<int, bool>();

                    for (int i = vt.buttonLabels[monitorIndex].Count; i < multiFunctionButtonsPOS.Count; i++)
                    {
                        vt.buttonLabels[monitorIndex][i] = buttonEmptyLabel;
                        vt.buttonStates[monitorIndex][i] = false;
                    }

                    for (int i = vt.flagLabels[monitorIndex].Count; i < flagCount; i++)
                    {
                        vt.flagLabels[monitorIndex][i] = flagEmptyLabel;
                        vt.flagStates[monitorIndex][i] = false;
                    }
                }

                //Format Dictionaries
                response_formats = new Dictionary<string, string>();
                replacement_formats = new Dictionary<string, object>();
                
                ReadTemplate();
                ReadReplacements();

                //Register monitor and Keyboard Delegate
                kPMCore.fetch.RegisterMonitor(this, guid);

                initialized = true;
            }
            
            Debug.Log("kPM: kOSMonitor Initialized!");
        }

        public string ContentProcessor(int screenWidth, int screenHeight)
        {
            //Check for initialization
            if (!initialized)
            {
                if (this.vessel != null)
                {
                    this.screenWidth = screenWidth;
                    this.screenHeight = screenHeight;
                    Initialize(screenWidth, screenHeight);
                }
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

                //Arrow Buttons
                else if (buttonID == upButton)
                {
                    upButtonState = !upButtonState;
                }
                else if (buttonID == downButton)
                {
                    downButtonState = !downButtonState;
                }
                else if (buttonID == leftButton)
                {
                    leftButtonState = !leftButtonState;
                }
                else if (buttonID == rightButton)
                {
                    rightButtonState = !rightButtonState;
                }

                //Enter and Cancel
                else if (buttonID == enterButton)
                {
                    enterButtonState = !enterButtonState;
                }
                else if (buttonID == cancelButton)
                {
                    cancelButtonState = !cancelButtonState;
                }

                //Power Toggle Button
                else if (buttonID == toggleProcessorPowerButton)
                {
                    //Set power state from SharedObjects
                    isPowered = processor_shares[current_processor_id].Window.IsPowered;
                    processors[current_processor_id].TogglePower();
                }

                //Programmable Buttons
                ProcessProgramButton(buttonID);
            }
        }


        //Button Utilities
        void ProcessProgramButton(int ID)
        {
            if (multiFunctionButtonsPOS.Contains(ID))
            {
                int bID = multiFunctionButtonsPOS.IndexOf(ID);
                vt.buttonStates[monitorIndex][bID] = !vt.buttonStates[monitorIndex][bID];
            }
        }


        //kOS-Utilities
        SharedObjects GetProcessorShare(kOSProcessor processor)
        {
            //Register the kOSProcessor's SharedObjects
            FieldInfo sharedField = typeof(kOSProcessor).GetField("shared", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
            var proc_shared = sharedField.GetValue(processor);
            return (SharedObjects)proc_shared;
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
                UnityEngine.Debug.Log(string.Format("kOSMonitor: Template {0} does not exist.", FILE_NAME));
                return;
            }
            using (StreamReader sr = File.OpenText(FILE_NAME))
            {
                unformattedTemplate = "";
                string input;
                while ((input = sr.ReadLine()) != null)
                {
                    unformattedTemplate += input + Environment.NewLine;
                }
                sr.Close();
            }
        }

        void ReadReplacements()
        {
            string FILE_NAME = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), replacements);
            if (!File.Exists(FILE_NAME))
            {
                UnityEngine.Debug.Log(string.Format("kOSMonitor: Replacements {0} does not exist.", FILE_NAME));
                return;
            }
            string json = "";
            using (StreamReader sr = File.OpenText(FILE_NAME))
            {
                
                string input;
                while ((input = sr.ReadLine()) != null)
                {
                    json += input + Environment.NewLine;
                }
                sr.Close();
            }

            JsonReader reader = new JsonReader();
            try
            {
                replacement_formats = reader.Read<Dictionary<string, object>>(json);
            }
            catch (Exception e)
            {
                Debug.Log("kPM: Error Loading JSON File: " + e.Message);
            }
        }

        void GetNewestBuffer()
        {
            mostRecentScreen = new ScreenSnapShot(processor_shares[current_processor_id].Screen);
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

            response_formats["currentCPU"] = currentTextTint + current_processor_id + "[#FFFFFF]";

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
                            response_formats["CPU" + (lrange + 1)] = "       ";
                            continue;
                        }

                        isPowered = processor_shares[current_position + lrange].Window.IsPowered;

                        if (current_position + lrange == current_processor_id)
                        {
                            response_formats["CPU" + (lrange + 1)] = currentTextTint + " CPU  " + (current_position + lrange) + "[#FFFFFF]";
                        }
                        else
                        {
                            response_formats["CPU" + (lrange + 1)] = " CPU  " + currentTextTint + (current_position + lrange) + "[#FFFFFF]";
                        }
                    }
                    break;
                }
            }
        }

        string SetFlags(string input)
        {
            string color = "";
            string output = input;
            foreach (KeyValuePair<int, bool> kvpair in vt.flagStates[monitorIndex])
            {
                if (kvpair.Value)
                {
                    color = textTintFlagOn;
                }
                else
                {
                    color = textTintFlagOff;
                }
                string sub = kvpair.Key.ToString();
                try
                {
                    output = output.Replace("{flagSide" + sub + "}", (color + flagSide + "[#FFFFFF]"));
                    output = output.Replace("{flagSideSmall" + sub + "}", (color + flagSideSmall + "[#FFFFFF]"));
                    output = output.Replace("{flagLabel" + sub + "}", (color + vt.flagLabels[monitorIndex][kvpair.Key]) + "[#FFFFFF]");
                }
                catch
                {
                    Debug.Log("kOSMonitor: Error in templating for flags!");
                }
            }

            if (isLocked)
            {
                color = keyboardActiveTint;
            }
            else
            {
                color = keyboardInactiveTint;
            }

            try
            {
                output = output.Replace("{keyboardFlag}", (color + keyboardActiveLabel));
            }
            catch
            {
                Debug.Log("kOSMonitor: Error setting keyboard flag!");
            }

            try
            {
                if (!longGuid) output = output.Replace("{GUID}", guid.ToString().Substring(0, 8));
                else output = output.Replace("{GUID}", guid.ToString());
            }
            catch
            {
                Debug.Log("kOSMonitor: Error setting GUID flag!");
            }
            return output;
        }

        string SetButtons(string input)
        {
            string output = input;
            foreach (KeyValuePair<int, bool> kvpair in vt.buttonStates[monitorIndex])
            {
                string color = "";
                if (kvpair.Value)
                {
                    color = textTintButtonOn;
                }
                else
                {
                    color = textTintButtonOff;
                }
                string sub = kvpair.Key.ToString();
                try
                {
                    output = output.Replace("{buttonSide" + sub + "}", color + buttonSide + "[#FFFFFF]");
                    output = output.Replace("{buttonSideSmall" + sub + "}", color + buttonSideSmall + "[#FFFFFF]");
                    output = output.Replace("{buttonLabel" + sub + "}", color + vt.buttonLabels[monitorIndex][kvpair.Key] + "[#FFFFFF]");
                }
                catch
                {
                    Debug.Log("kOSMonitor: Error in templating for buttons!");
                }
            }
            return output;
        }


        //Keyboard Control
        public void ToggleLock()
        {
            kPMCore.fetch.ToggleLock(guid);
            isLocked = kPMCore.fetch.IsLocked(guid);
        }

        public void Type(char command)
        {
            processor_shares[current_processor_id].Window.ProcessOneInputChar(command, null);
            cursorBlinkTime = 0.0f;// Don't blink while the user is still actively typing.
            consumeEvent = true;
        }
    }
}