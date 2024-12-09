namespace DelsysTestFramework.CommonParts
{
    public class INA190
    {

        public static double GetINA190Current(double Vout, double Vref, double Gain, double R)
        {
            return ((Vout - Vref) / Gain) / R;
        }
    }
}
