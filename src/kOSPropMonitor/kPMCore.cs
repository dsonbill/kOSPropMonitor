using System;
using System.Collections.Generic;
using UnityEngine;
using kOS.Safe.UserIO;

namespace kOSPropMonitor
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class kPMCore : MonoBehaviour
    {
        //Singleton
        private static kPMCore singleton;

        //Tracking
        public Dictionary<Guid, kPMVesselTrack> vessel_register;
        public Dictionary<Guid, kOSMonitor> monitor_register;
        private string CONTROL_LOCKOUT = "kPMCore";
        private Guid lock_control;
        private Guid master_lock;
        private bool wasInFlight;

        //Keyboard Memory Variables
        private KeyBinding rememberThrottleCutoffKey;
        private KeyBinding rememberThrottleFullKey;
        private KeyBinding rememberCameraResetKey;
        private KeyBinding rememberCameraModeKey;
        private KeyBinding rememberCameraViewKey;

        //ctor
        public kPMCore()
        {
            singleton = this;
            master_lock = Guid.NewGuid();
            lock_control = master_lock;
            Debug.Log("kPMCore GUID: " + master_lock);
        }

        //Singleton
        public static kPMCore fetch
        {
            get
            {
                return singleton;
            }
        }

        //Delegate
        public delegate void Del();

        void Awake()
        {
            //Initialize
            GameObject.DontDestroyOnLoad(this);
            vessel_register = new Dictionary<Guid, kPMVesselTrack>();
            monitor_register = new Dictionary<Guid, kOSMonitor>();
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);

            //Set Key Binding Memory
            rememberCameraResetKey = GameSettings.CAMERA_RESET;
            rememberCameraModeKey = GameSettings.CAMERA_MODE;
            rememberCameraViewKey = GameSettings.CAMERA_NEXT;
            rememberThrottleCutoffKey = GameSettings.THROTTLE_CUTOFF;
            rememberThrottleFullKey = GameSettings.THROTTLE_FULL;
        }

        void Update()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT && !wasInFlight) wasInFlight = true;

            if (HighLogic.LoadedScene != GameScenes.FLIGHT && wasInFlight)
            {
                vessel_register = new Dictionary<Guid, kPMVesselTrack>();
                monitor_register = new Dictionary<Guid, kOSMonitor>();
            }

            if (lock_control != master_lock)
            {
                //No Lock If Not In Flight
                if (HighLogic.LoadedScene != GameScenes.FLIGHT)
                {
                    Unlock();
                }
            }
        }

        void OnGUI()
        {
            if (lock_control != master_lock)
            {
                ProcessKeyEvents();
            }
        }

        private void OnVesselDestroy(Vessel destroyedVessel)
        {
            if (vessel_register.ContainsKey(destroyedVessel.id))
            {
                foreach(Guid monitorID in vessel_register[destroyedVessel.id].monitors)
                {
                    monitor_register.Remove(monitorID);
                }
                vessel_register.Remove(destroyedVessel.id);
            }
            Unlock();
        }

        public void RegisterVessel(Guid vesselID)
        {
            if (vessel_register.ContainsKey(vesselID))
            {
                return;
            }
            vessel_register[vesselID] = new kPMVesselTrack();
        }

        public kPMVesselTrack GetVesselTrack(Guid vesselID)
        {
            return vessel_register[vesselID];
        }

        public void RegisterMonitor(kOSMonitor monitor, Guid monitorID)
        {
            if (monitor_register.ContainsKey(monitorID))
            {
                return;
            }
            monitor_register[monitorID] = monitor;
        }

        public bool IsLocked(Guid monitorID)
        {
            if (lock_control != monitorID)
            {
                return false;
            }

            return true;
        }

        public void ToggleLock(Guid monitorID)
        {
            if (lock_control != master_lock)
            {
                if (lock_control != monitorID)
                {
                    return;
                }
                Unlock();
            }
            else
                Lock(monitorID);
        }

        void Lock(Guid monitorID)
        {
            if (lock_control != master_lock) return;
            lock_control = monitorID;
            InputLockManager.SetControlLock(CONTROL_LOCKOUT);
            if (lock_control != master_lock) Debug.Log("kPMCore: Locked. New Lock: " + lock_control);

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
            if (lock_control == master_lock) return;
            lock_control = master_lock;
            InputLockManager.RemoveControlLock(CONTROL_LOCKOUT);
            Debug.Log("kPMCore: Unlocked. Returned To Master Lock");

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
                        case KeyCode.KeypadEnter:  // (deliberate fall through to next case)
                        case KeyCode.Return: Type((char)UnicodeCommand.STARTNEXTLINE); break;

                        // More can be added to the list here to support things like F1, F2, etc.  But at the moment we don't use them yet.

                        // default: ignore and allow the event to pass through to whatever else wants to read it:
                        default: break;
                    }
                }
            }
        }

        public void Type(char ch)
        {
            monitor_register[lock_control].Type(ch);
        }
    }

    class kPMVesselTrack
    {
        public List<kPMAPI> kpmAPI;
        public Dictionary<int, bool> buttonStates;
        public Dictionary<int, string> buttonLabels;
        public Dictionary<int, string> buttonID;
        public Dictionary<int, bool> flagStates;
        public Dictionary<int, string> flagLabels;
        public List<Guid> monitors;

        public delegate void OnButtonStateChange(int button, bool value);
        public event OnButtonStateChange onButtonStateChange;
        public delegate void OnButtonLabelChange(int button, string value);
        public event OnButtonLabelChange onButtonLabelChange;

        public delegate void OnFlagStateChange(int flag, bool value);
        public event OnFlagStateChange onFlagStateChange;
        public delegate void OnFlagLabelChange(int flag, string value);
        public event OnFlagLabelChange onFlagLabelChange;

        public kPMVesselTrack()
        {
            kpmAPI = new List<kPMAPI>();
            buttonStates = new Dictionary<int, bool>();
            buttonLabels = new Dictionary<int, string>();
            buttonID = new Dictionary<int, string>();
            flagStates = new Dictionary<int, bool>();
            flagLabels = new Dictionary<int, string>();
            monitors = new List<Guid>();
        }

        public void RegisterMonitor(Guid monitorID)
        {
            monitors.Add(monitorID);
        }

        public void RegisterAPI(kPMAPI api)
        {
            api.GetButtons().onButtonStateChange += ChangeButtonState;
            api.GetButtons().onButtonLabelChange += ChangeButtonLabel;
            api.GetFlags().onFlagStateChange += ChangeFlagState;
            api.GetFlags().onFlagLabelChange += ChangeFlagLabel;
            kpmAPI.Add(api);
        }

        public void ChangeButtonState(int button, bool value)
        {
            if (onButtonStateChange != null) onButtonStateChange(button, value);
        }

        public void ChangeButtonLabel(int button, string value)
        {
            if (onButtonLabelChange != null) onButtonLabelChange(button, value);
        }

        public void ChangeFlagState(int flag, bool value)
        {
            if (onFlagStateChange != null) onFlagStateChange(flag, value);
        }

        public void ChangeFlagLabel(int flag, string value)
        {
            if (onFlagLabelChange != null) onFlagLabelChange(flag, value);
        }
    }
}