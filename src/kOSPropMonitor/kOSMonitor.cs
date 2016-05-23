using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using kOS;
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

        //General Variables
        private bool initialized = false;
        private kPMVesselTrack vt;
        private int lastPartCount = 0;
        private List<int> multiFunctionButtonsPOS;
        private char[] delimiterChars = { ' ', ',', '.', ':'};
        private Dictionary<string, string> response_formats;
        private Guid guid;

        //kOS Processor Variables
        private bool isPowered = false;
        private bool processorIsInstalled;
        private int current_processor_id = 0;
        private List<SharedObjects> processor_shares;
        private List<kOSProcessor> processors;
        private Dictionary<int, float> wasOff;

        //kOS Terminal Variables
        private bool consumeEvent;
        private bool isLocked = false;
        private string response = "kOS Terminal Standing By";

        //private string consoleBuffer;
        private float cursorBlinkTime;
        private string currentTextTint;
        private IScreenSnapShot mostRecentScreen;
        private int screenWidth;
        private int screenHeight;

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

                    //Check if a processor was recently booted
                    if (wasOff.Count > 0)
                    {
                        foreach (KeyValuePair<int, float> kvpair in wasOff)
                        {
                            if (kvpair.Value > 0.25f)
                            {
                                //Add bound variables
                                AddGettersAndSetters(processor_shares[kvpair.Key]);
                                wasOff.Remove(kvpair.Key);
                                Debug.Log("kOSMonitor Re-Initialized Processor");
                            }
                            else
                            {
                                wasOff[kvpair.Key] += Time.deltaTime;
                                //Debug.Log("kOSMonitor Initializing Processor...");
                            }
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

                    //Format Response
                    response = Utilities.Format(template, response_formats);
                    SetButtons();
                    SetFlags();

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
                ToggleLock();
            }
        }

        public void Initialize(int screenWidth, int screenHeight)
        {
            //Set Processor Installed Flag
            processorIsInstalled = false;

            //Response Dictionary
            response_formats = new Dictionary<string, string>();

            //Boot-up tracking
            wasOff = new Dictionary<int, float>();

            //Register kOSProcessors
            processors = GetProcessorList();

            //Get SharedObjects
            processor_shares = new List<SharedObjects>();
            foreach (kOSProcessor kos_processor in processors)
            {
                //Set Processor Installed Flag
                processorIsInstalled = true;

                //Register the kOSProcessor's SharedObjects
                processor_shares.Add(GetProcessorShare(kos_processor));
            }

            //Set Vessel Part Cound
            lastPartCount = this.vessel.parts.Count;

            //Single-Init Actions
            if (!initialized)
            {
                //Create GUID
                guid = Guid.NewGuid();

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

                ReadTemplate();

                //Register Vessel and Get Track
                kPMCore.fetch.RegisterVessel(this.vessel.id);
                vt = kPMCore.fetch.GetVesselTrack(this.vessel.id);
                vt.RegisterMonitor(guid);

                //Register monitor and Keyboard Delegate
                kPMCore.fetch.RegisterMonitor(this, guid);

                //Register Buttons and Flags
                for (int i = vt.buttonLabels.Count; i < multiFunctionButtonsPOS.Count; i++)
                {
                    vt.buttonLabels["button" + i] = buttonEmptyLabel;
                    vt.buttonStates["button" + i] = false;
                }

                for (int i = vt.flagLabels.Count; i < flagCount; i++)
                {
                    vt.flagLabels["flag" + i] = flagEmptyLabel;
                    vt.flagStates["flag" + i] = false;
                }

                //Add Getters and Setters
                foreach (SharedObjects so in processor_shares)
                {
                    AddGettersAndSetters(so);
                }

                initialized = true;
            }

            Debug.Log("kOSMonitor Initialized!");
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
                    //Set power state from SharedObjects
                    isPowered = processor_shares[current_processor_id].Window.IsPowered;

                    //Reset getters and setters on power-up
                    if (!isPowered)
                    {
                        Debug.Log("kOSMonitor Adding Boot-Up Event");
                        processors[current_processor_id].TogglePower();
                        wasOff[current_processor_id] = 0.00f;
                    }
                    else
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
            if (multiFunctionButtonsPOS.Contains(ID))
            {
                int bID = multiFunctionButtonsPOS.IndexOf(ID);
                vt.buttonStates["button" + bID] = !vt.buttonStates["button" + bID];
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

            if (isPowered)
            {
                currentTextTint = textTint;
            }
            else
            {
                currentTextTint = textTintUnpowered;
            }

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

        void AddGettersAndSetters(SharedObjects so)
        {
            //Looping doesn't seem to work - I'm sure there's another way to do this, but this is simple enough

            //Flags
            for (int i = 0; i < vt.flagStates.Count; i ++)
            {
                string flagname = "FLAG" + i;
                Debug.Log("kOSMonitor: Registering Flag " + flagname);
                so.BindingMgr.AddGetter(flagname, () => vt.flagStates[flagname.ToLower()]);
                so.BindingMgr.AddSetter(flagname, value => vt.flagStates[flagname.ToLower()] = (bool)value);

                so.BindingMgr.AddGetter(flagname + "LABEL", () => vt.flagLabels[flagname.ToLower()]);
                so.BindingMgr.AddSetter(flagname + "LABEL", value => vt.flagLabels[flagname.ToLower()] = (string)value);
            }
            //Buttons
            for (int i = 0; i < vt.buttonStates.Count; i++)
            {
                string buttonname = "BUTTON" + i;
                Debug.Log("kOSMonitor: Registering Button " + buttonname);
                so.BindingMgr.AddGetter(buttonname, () => vt.buttonStates[buttonname.ToLower()]);
                so.BindingMgr.AddSetter(buttonname, value => vt.buttonStates[buttonname.ToLower()] = (bool)value);
                
                so.BindingMgr.AddGetter(buttonname + "LABEL", () => vt.buttonLabels[buttonname.ToLower()]);
                so.BindingMgr.AddSetter(buttonname + "LABEL", value => vt.buttonLabels[buttonname.ToLower()] = (string)value);
            }
        }

        void SetFlags()
        {
            string color = "";
            foreach (KeyValuePair<string, bool> kvpair in vt.flagStates)
            {
                if (kvpair.Value)
                {
                    color = textTintFlagOn;
                }
                else
                {
                    color = textTintFlagOff;
                }
                string sub = kvpair.Key.Substring(4);
                try
                {
                    response = response.Replace("{flagSide" + sub + "}", (color + flagSide + "[#FFFFFF]"));
                    response = response.Replace("{flagSideSmall" + sub + "}", (color + flagSideSmall + "[#FFFFFF]"));
                    response = response.Replace("{flagLabel" + sub + "}", (color + vt.flagLabels[kvpair.Key]) + "[#FFFFFF]");
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
                response = response.Replace("{keyboardFlag}", (color + keyboardActiveLabel));
            }
            catch
            {
                Debug.Log("kOSMonitor: Error setting keyboard flag!");
            }

            try
            {
                response = response.Replace("{GUID}", guid.ToString());
            }
            catch
            {
                Debug.Log("kOSMonitor: Error setting GUID flag!");
            }
        }

        void SetButtons()
        {
            foreach (KeyValuePair<string, bool> kvpair in vt.buttonStates)
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
                string sub = kvpair.Key.Substring(6);
                try
                {
                    response = response.Replace("{buttonSide" + sub + "}", (color + buttonSide + "[#FFFFFF]").ToString());
                    response = response.Replace("{buttonSideSmall" + sub + "}", (color + buttonSideSmall + "[#FFFFFF]").ToString());
                    response = response.Replace("{buttonLabel" + sub + "}", (color + vt.buttonLabels["button" + sub] + "[#FFFFFF]").ToString());
                }
                catch
                {
                    Debug.Log("kOSMonitor: Error in templating for buttons!");
                }
            }
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