namespace DelsysTestLib.Util
{
    public class StringHelper
    {
        public static StringHelper Instance { get; } = new StringHelper();

        public string ReplaceCommonEscapeSequences(string s)
        {
            return s.Replace("\\n", "\n").Replace("\\r", "\r");
        }
        public string InsertCommonEscapeSequences(string s)
        {
            return s.Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
