namespace DelsysTestFramework.Util
{
    public class Converter
    {

        public static bool[] IntToBoolArray32(UInt32 number)
        {
            int numBits = 32; // Assuming integers are 32-bit in your C# environment
            bool[] boolArray = new bool[numBits];

            for (int i = 0; i < numBits; i++)
            {
                boolArray[i] = (number & (1 << i)) != 0;
            }

            return boolArray;
        }

        public static bool[] IntToBoolArray16(UInt16 number)
        {
            int numBits = 16; // Assuming integers are 32-bit in your C# environment
            bool[] boolArray = new bool[numBits];

            for (int i = 0; i < numBits; i++)
            {
                boolArray[i] = (number & (1 << i)) != 0;
            }

            return boolArray;
        }
    }
}
