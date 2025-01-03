using System;
using System.Collections.Generic;
using System.Linq;

namespace DelsysNICommon
{

    public abstract class Part
    {


        /// <summary>
        /// List of all pins
        /// </summary>
        public List<Pin> Pins { get; protected set; } = new List<Pin>();


        /// <summary>
        /// Internal state memory of the part
        /// </summary>
        public List<bool> State { get; protected set; } = new List<bool>();

        /// <summary>
        /// User defined label
        /// </summary>
        public string Label { get; set; }


        /// <summary>
        /// Returns all adjacent parts in the part graph
        /// </summary>
        public List<Part> GetConnectedParts()
        {
            List<Part> parts = new List<Part>();
            for (int i = 0; i < Pins.Count; i++)
            {
                if (Pins[i].PinDirection == Pin.IN_OUT.INPUT) continue;

                foreach (Part part in Pins[i].NetList.Select(p => p.ParentPart))
                {
                    if (!parts.Contains(part))
                        parts.Add(part);
                }
            }

            parts.Remove(this);

            return parts;
        }

        internal abstract void ChangeHandler(object sender, ref List<Part> queue);


        public override string ToString()
        {
            return Label;
        }
    }




    public class PartEventArgs : EventArgs
    {
        public Queue<Part> PartUpdateQueue = new Queue<Part>();

        //public List<Pin> ChangedPins = new List<Pin>();
        //public List<object> NewValues = new List<object>();
    }
}
