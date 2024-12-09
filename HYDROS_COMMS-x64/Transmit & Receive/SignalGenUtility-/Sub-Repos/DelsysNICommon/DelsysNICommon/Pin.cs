using System;
using System.Collections.Generic;
using System.Linq;

namespace DelsysNICommon
{

    public abstract class Pin
    {
        public enum SIGNAL_TYPE
        {
            DIGITAL,
            ANALOG
        }

        public enum IN_OUT
        {
            INPUT,
            OUTPUT,
            INOUT
        }

        public SIGNAL_TYPE SignalType;
        public IN_OUT PinDirection;


        /// <summary>
        /// Default value to reset an NI-DAQ pin
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Current value of the pin
        /// </summary>
        public object CurrentValue { get; protected set; }


        /// <summary>
        /// User defined label
        /// </summary>
        public string Label { get; set; }


        /// <summary>
        /// Part the pin belongs to
        /// </summary>
        public Part ParentPart { get; private set; }

        /// <summary>
        /// List of all other connected pins (including self)
        /// Their values are bound
        /// </summary>
        public List<Pin> NetList { get; private set; }



        private Guid Id { get; }
        private List<Pin> GlobalPins = new List<Pin>();


        public Pin(Part parent, SIGNAL_TYPE signalType, IN_OUT pinDirection)
        {
            Id = Guid.NewGuid();
            ParentPart = parent;
            SignalType = signalType;
            PinDirection = pinDirection;
            NetList = new List<Pin>();

            // Could be useful 
            GlobalPins.Add(this);
        }



        /// <summary>
        /// Connect all pins to each other
        /// Also this must be an output pin to connect parts
        /// </summary>
        /// <param name="pins"></param>
        public void ConnectPins(params Pin[] pins)
        {
            var pinUnion = new List<Pin>(pins);
            pinUnion.Add(this);


            foreach (Pin p in pinUnion)
            {
                p.NetList = p.NetList.Union(pinUnion).ToList();

                /* if (this.PinDirection != IN_OUT.INPUT && p != this)
                     this.ParentPart.OutputChanged += p.ParentPart.ChangeHandler;*/
            }
        }

        public void DisconnectPins(Pin pin)
        {
            this.NetList.Remove(pin);
            pin.NetList.Remove(this);
        }

        public override string ToString()
        {
            return CurrentValue.ToString();
        }

    }

    public class DigitalPin : Pin
    {
        public DigitalPin(Part parentPart, IN_OUT pinDirection) : base(parentPart, SIGNAL_TYPE.DIGITAL, pinDirection)
        {
            CurrentValue = false; //change later
            DefaultValue = false;
        }


        public void SetValue(bool val)
        {
            CurrentValue = val;
            NetList.ForEach(p => ((DigitalPin)p).CurrentValue = val);
        }
    }

    public class AnalogPin : Pin
    {

        public AnalogPin(Part parentPart, IN_OUT pinDirection) : base(parentPart, SIGNAL_TYPE.ANALOG, pinDirection)
        {

            CurrentValue = 0; //change later
            DefaultValue = 0;
        }

        public void SetValue(double val)
        {
            CurrentValue = val;
            NetList.ForEach(p => ((AnalogPin)p).CurrentValue = val);
        }

    }
}