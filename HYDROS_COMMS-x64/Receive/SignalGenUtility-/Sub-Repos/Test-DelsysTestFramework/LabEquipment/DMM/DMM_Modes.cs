namespace DelsysTestLib.LabEquipment.DMM
{
    public enum DMM_Modes
    {
        TwoWireResistance = 0,
        FOURWireResistance = 1,
        CurrentDC = 2,
        VoltageDC = 3,
        Capacitance = 4,
        Diode = 5,
        Continuity = 6,
        Temperature = 7,
        Frequency = 8,
        Period = 9,
    }
    public class DMM_Mode
    {

        private static string[] StateStringDict =
        {
            ":SENS:FUNC \"RES\"",
            ":SENS:FUNC \"FRES\"",
            ":SENS:FUNC \"CURR:DC\"",
            ":SENS:FUNC \"VOLT:DC\"",
            ":SENS:FUNC \"CAP\"",
            ":SENS:FUNC \"DIOD\"",
            ":SENS:FUNC \"CONT\"",
            ":SENS:FUNC \"TEMP\"",
            ":SENS:FUNC \"FREQ\"",
            ":SENS:FUNC \"PER\"" };
        public static string GetStringFromState(DMM_Modes mode)
        {
            return StateStringDict[((int)mode)];
        }
    }
}