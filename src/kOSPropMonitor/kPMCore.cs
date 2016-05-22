using System;
using System.Collections.Generic;
using UnityEngine;

namespace kOSPropMonitor
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class kPMCore : MonoBehaviour
    {
        //Singleton
        private static kPMCore singleton;

        //Tracking
        private Dictionary<Guid, kPMVesselTrack> vessel_register;
        private Dictionary<Guid, Guid> monitor_register;


        public kPMCore()
        {
            singleton = this;
        }

        public static kPMCore fetch
        {
            get
            {
                return singleton;
            }
        }

        public Dictionary<Guid,bool> lock_states
        {
            get
            {
                return lock_states;
            }
            private set
            {
                lock_states = value;
            }
        }

        public void Awake()
        {
            //Initialize
            vessel_register = new Dictionary<Guid, kPMVesselTrack>();
            monitor_register = new Dictionary<Guid, Guid>();
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
        }

        public void Update()
        {
            foreach (KeyValuePair<Guid, kPMVesselTrack> kvpair in vessel_register)
            {
                if (kvpair.Value.isLocked)
                {
                    foreach (Func<bool> kpmfunc in kvpair.Value.keyboardFunctions)
                    {
                        kpmfunc();
                    }
                }
            }

        }

        private void OnVesselDestroy(Vessel destroyedVessel)
        {
            if (vessel_register.ContainsKey(destroyedVessel.id))
            {
                vessel_register.Remove(destroyedVessel.id);
            }
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

        public void RegisterMonitor(Guid vesselID, Guid monitorID)
        {
            if (monitor_register.ContainsKey(monitorID))
            {
                return;
            }
            monitor_register[monitorID] = vesselID;
        }
    }

    class kPMVesselTrack
    {
        public Dictionary<string, bool> buttonStates;
        public Dictionary<string, string> buttonLabels;
        public Dictionary<int, string> buttonID;
        public Dictionary<string, bool> flagStates;
        public Dictionary<string, string> flagLabels;
        public List<Func<bool>> keyboardFunctions;
        public int buttonsRegistered;
        public bool isLocked;

        public kPMVesselTrack()
        {
            buttonStates = new Dictionary<string, bool>();
            buttonLabels = new Dictionary<string, string>();
            buttonID = new Dictionary<int, string>();
            flagStates = new Dictionary<string, bool>();
            flagLabels = new Dictionary<string, string>();
            buttonsRegistered = 0;
            keyboardFunctions = new List<Func<bool>>();
        }
    }
}