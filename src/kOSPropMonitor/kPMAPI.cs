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
        private kPMFlagAPI flags;

        public kPMAPI(SharedObjects shared) : base(shared)
        {
            //UnityEngine.Debug.Log("kPM: New API Object Created");
            AddSuffix("BUTTONS", new Suffix<kPMButtonAPI>(GetButtons));
            AddSuffix("FLAGS", new Suffix<kPMFlagAPI>(GetFlags));
        }

        public override BooleanValue Available()
        {
            return kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id);
        }

        public kPMButtonAPI GetButtons()
        {
            if (buttons == null)
            {
                buttons = new kPMButtonAPI(shared);
            }
            return buttons;
        }

        public kPMFlagAPI GetFlags()
        {
            if (flags == null)
            {
                flags = new kPMFlagAPI(shared);
            }
            return flags;
        }
    }

    [KOSNomenclature("BUTTONS")]
    public class kPMButtonAPI : Structure
    {
        private SharedObjects shared;

        public kPMButtonAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("GETLABEL", new OneArgsSuffix<StringValue, ScalarIntValue>(GetButtonLabel));
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetButtonLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetButtonState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetButtonState));
        }

        private StringValue GetButtonLabel(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return "";
            if (!kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonLabels.ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonLabels[value];
        }

        private void SetButtonLabel(ScalarIntValue index, StringValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonLabels[index] = value;
        }

        private BooleanValue GetButtonState(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return false;
            if (!kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonStates.ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonStates[value];
        }

        private void SetButtonState(ScalarIntValue index, BooleanValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            kPMCore.fetch.GetVesselTrack(shared.Vessel.id).buttonStates[index] = value;
        }
    }

    [KOSNomenclature("FLAGS")]
    public class kPMFlagAPI : Structure
    {
        private SharedObjects shared;

        public kPMFlagAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("GETLABEL", new OneArgsSuffix<StringValue, ScalarIntValue>(GetFlagLabel));
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetFlagLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetFlagState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetFlagState));
        }

        private StringValue GetFlagLabel(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return "";
            if (!kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagLabels.ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagLabels[value];
        }

        private void SetFlagLabel(ScalarIntValue index, StringValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagLabels[index] = value;
        }

        private BooleanValue GetFlagState(ScalarIntValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return false;
            if (!kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagStates.ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagStates[value];
        }

        private void SetFlagState(ScalarIntValue index, BooleanValue value)
        {
            if (!kPMCore.fetch.vessel_register.ContainsKey(shared.Vessel.id)) return;
            kPMCore.fetch.GetVesselTrack(shared.Vessel.id).flagStates[index] = value;
        }
    }
}