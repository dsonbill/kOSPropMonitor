using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using kOS.Safe.Encapsulation;
using kOS.Safe.Encapsulation.Suffixes;
using kOS.Safe.Exceptions;
using kOS.Safe.Utilities;
using kOS.Suffixed;
using kOS.Utilities;
using kOS.AddOns;
using kOS;

namespace kOSPropMonitor
{
    [kOSAddon("KPM")]
    [KOSNomenclature("KPMAddon")]
    public class kPMAPI : kOS.Suffixed.Addon
    {
        private kPMButtonAPI buttons;
        private kPMButtonDelegateAPI buttonDelegates;
        private kPMFlagAPI flags;

        public kPMAPI(SharedObjects shared) : base(shared)
        {
            AddSuffix("BUTTONS", new Suffix<kPMButtonAPI>(GetButtons));
            AddSuffix("DELEGATES", new Suffix<kPMButtonDelegateAPI>(GetButtonDelegates));
            AddSuffix("FLAGS", new Suffix<kPMFlagAPI>(GetFlags));
            AddSuffix("GETGUID", new OneArgsSuffix<StringValue, ScalarIntValue>((ScalarIntValue index) => GetGUID(index, false)));
            AddSuffix("GETGUIDSHORT", new OneArgsSuffix<StringValue, ScalarIntValue>((ScalarIntValue index) => GetGUID(index, true)));
            AddSuffix("GETMONITORCOUNT", new NoArgsSuffix<ScalarIntValue>(GetMonitorCount));
            AddSuffix("GETINDEXOF", new OneArgsSuffix<ScalarIntValue, StringValue>(IndexOf));
        }

        public override BooleanValue Available()
        {
            return kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id);
        }

        private kPMButtonAPI GetButtons()
        {
            if (buttons == null)
            {
                buttons = new kPMButtonAPI(shared);
            }
            return buttons;
        }

        private kPMButtonDelegateAPI GetButtonDelegates()
        {
            if (buttonDelegates == null)
            {
                buttonDelegates = new kPMButtonDelegateAPI(shared);
            }
            return buttonDelegates;
        }

        private kPMFlagAPI GetFlags()
        {
            if (flags == null)
            {
                flags = new kPMFlagAPI(shared);
            }
            return flags;
        }

        private StringValue GetGUID(ScalarIntValue index, bool shortGuid)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return "";
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= index) throw new KOSException("Cannot get monitor guid, input out of range.");

            if (shortGuid) return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[index].ToString().Substring(0, 8);
            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[index].ToString();
        }

        private ScalarIntValue GetMonitorCount()
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return 0;
            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count;
        }

        private ScalarIntValue IndexOf(StringValue guid)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return -1;
            foreach (KeyValuePair<int, Guid> kvpair in kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors)
            {
                if (guid == kvpair.Value.ToString().Substring(0, 8)) return kvpair.Key;
                if (guid == kvpair.Value.ToString()) return kvpair.Key;
            }
            return -1;
        }
    }

    [KOSNomenclature("BUTTONS")]
    public class kPMButtonAPI : Structure
    {
        private SharedObjects shared;
        private int monitor = 0;

        public kPMButtonAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("CURRENTMONITOR", new SetSuffix<ScalarIntValue>(() => { return monitor; }, (monitor) => { this.monitor = monitor; }));
            AddSuffix("GETLABEL", new OneArgsSuffix<StringValue, ScalarIntValue>(GetButtonLabel));
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetButtonLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetButtonState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetButtonState));
        }

        private StringValue GetButtonLabel(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return "";
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return "";
            if (!kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonLabels[monitor].ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonLabels[monitor][value];
        }

        private void SetButtonLabel(ScalarIntValue index, StringValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return;
            kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonLabels[monitor][index] = value;
        }

        private BooleanValue GetButtonState(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return false;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return false;
            if (value < 0)
            {
                if (value == -1) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].enterButtonState;
                else if (value == -2) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].cancelButtonState;
                else if (value == -3) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].upButtonState;
                else if (value == -4) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].downButtonState;
                else if (value == -5) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].leftButtonState;
                else if (value == -6) return kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].rightButtonState;
            }
            if (!kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonStates[monitor].ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");
            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonStates[monitor][value];
        }

        private void SetButtonState(ScalarIntValue index, BooleanValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return;
            if (index < 0)
            {
                if (index == -1) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].enterButtonState = value;
                if (index == -2) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].cancelButtonState = value;
                if (index == -3) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].upButtonState = value;
                if (index == -4) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].downButtonState = value;
                if (index == -5) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].leftButtonState = value;
                if (index == -6) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].rightButtonState = value;
                return;
            }
            kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonStates[monitor][index] = value;
        }

        private void ButtonTrigger(ScalarIntValue index, UserDelegate value)
        {

        }
    }


    [KOSNomenclature("DELEGATES")]
    public class kPMButtonDelegateAPI : Structure
    {
        private SharedObjects shared;
        private int monitor = 0;

        public kPMButtonDelegateAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("CURRENTMONITOR", new SetSuffix<ScalarIntValue>(() => { return monitor; }, (monitor) => { this.monitor = monitor; }));
            AddSuffix("SETDELEGATE", new TwoArgsSuffix<ScalarIntValue, UserDelegate>(SetButtonDelegate));
        }

        private void SetButtonDelegate (ScalarIntValue index, UserDelegate value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return;
            if (index < 0)
            {
                if (index == -1) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].enterButtonDelegate = value;
                else if (index == -2) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].cancelButtonDelegate = value;
                else if (index == -3) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].upButtonDelegate = value;
                else if (index == -4) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].downButtonDelegate = value;
                else if (index == -5) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].leftButtonDelegate = value;
                else if (index == -6) kPMCore.fetch.monitor_register[kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors[monitor]].rightButtonDelegate = value;
                return;
            }
            kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).buttonDelegates[monitor][index] = value;
        }
    }



    [KOSNomenclature("FLAGS")]
    public class kPMFlagAPI : Structure
    {
        private SharedObjects shared;
        private int monitor = 0;

        public kPMFlagAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("CURRENTMONITOR", new SetSuffix<ScalarIntValue>(() => { return monitor; }, (monitor) => { this.monitor = monitor; }));
            AddSuffix("GETLABEL", new OneArgsSuffix<StringValue, ScalarIntValue>(GetFlagLabel));
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetFlagLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetFlagState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetFlagState));
        }

        private StringValue GetFlagLabel(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return "";
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return "";
            if (!kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagLabels[monitor].ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagLabels[monitor][value];
        }

        private void SetFlagLabel(ScalarIntValue index, StringValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return;
            kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagLabels[monitor][index] = value;
        }

        private BooleanValue GetFlagState(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return false;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return false;
            if (!kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagStates[monitor].ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagStates[monitor][value];
        }

        private void SetFlagState(ScalarIntValue index, BooleanValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            if (kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count == 0 || kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).monitors.Count <= monitor) return;
            kPMCore.fetch.GetVesselMonitors(shared.Vessel.id).flagStates[monitor][index] = value;
        }
    }
}