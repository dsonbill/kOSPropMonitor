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
            kPMCore.fetch.RegisterVessel(shared.Vessel.id);
            kPMCore.fetch.GetVesselTrack(shared.Vessel.id).RegisterAPI(this);

            AddSuffix("BUTTONS", new Suffix<kPMButtonAPI>(GetButtons));
            AddSuffix("FLAGS", new Suffix<kPMFlagAPI>(GetFlags));
        }

        public override BooleanValue Available()
        {
            return BooleanValue.True;
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

        public Dictionary<int, bool> buttonStates = new Dictionary<int, bool>();
        public Dictionary<int, string> buttonLabels = new Dictionary<int, string>();

        public delegate void OnButtonStateChange(int button, bool value);
        public event OnButtonStateChange onButtonStateChange;
        public delegate void OnButtonLabelChange(int button, string value);
        public event OnButtonLabelChange onButtonLabelChange;

        public kPMButtonAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetButtonLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetButtonState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetButtonState));
        }

        private BooleanValue GetButtonState(ScalarIntValue value)
        {
            if (value < 0 || !buttonLabels.ContainsKey(value)) throw new KOSException("Cannot get button status, input out of range.");

            return new BooleanValue(buttonStates[value]);
        }

        private void SetButtonLabel(ScalarIntValue index, StringValue value)
        {
            if (shared.Vessel.isActiveVessel)
            {
                buttonLabels[index] = value;
                if (onButtonLabelChange != null) onButtonLabelChange(index, value);
            }
        }

        private void SetButtonState(ScalarIntValue index, BooleanValue value)
        {
            if (shared.Vessel.isActiveVessel)
            {
                buttonStates[index] = value;
                if (onButtonStateChange != null) onButtonStateChange(index, value);
            }
        }
    }

    [KOSNomenclature("FLAGS")]
    public class kPMFlagAPI : Structure
    {
        private SharedObjects shared;

        public Dictionary<int, bool> flagStates = new Dictionary<int, bool>();
        public Dictionary<int, string> flagLabels = new Dictionary<int, string>();

        public delegate void OnFlagStateChange(int flag, bool value);
        public event OnFlagStateChange onFlagStateChange;
        public delegate void OnFlagLabelChange(int flag, string value);
        public event OnFlagLabelChange onFlagLabelChange;

        public kPMFlagAPI(SharedObjects shared)
        {
            this.shared = shared;
            AddSuffix("SETLABEL", new TwoArgsSuffix<ScalarIntValue, StringValue>(SetFlagLabel));
            AddSuffix("GETSTATE", new OneArgsSuffix<BooleanValue, ScalarIntValue>(GetFlagState));
            AddSuffix("SETSTATE", new TwoArgsSuffix<ScalarIntValue, BooleanValue>(SetFlagState));
        }

        private BooleanValue GetFlagState(ScalarIntValue value)
        {
            if (value < 0 || !flagLabels.ContainsKey(value)) throw new KOSException("Cannot get flag status, input out of range.");

            return new BooleanValue(flagStates[value]);
        }

        private void SetFlagLabel(ScalarIntValue index, StringValue value)
        {
            if (shared.Vessel.isActiveVessel)
            {
                flagLabels[index] = value;
                if (onFlagLabelChange != null) onFlagLabelChange(index, value);
            }
        }

        private void SetFlagState(ScalarIntValue index, BooleanValue value)
        {
            if (shared.Vessel.isActiveVessel)
            {
                flagStates[index] = value;
                if (onFlagStateChange != null) onFlagStateChange(index, value);
            }
        }
    }
}