using System.ComponentModel;

namespace HeboTech.ATLib.DTOs
{
    public static class Storages
    {
        /// <summary>
        /// FLASH message storage
        /// </summary>
        [Description("FLASH message storage")]
        public static string FlashME => "ME";

        /// <summary>
        /// FLASH message storage
        /// </summary>
        [Description("FLASH message storage")]
        public static string FlashMT => "MT";

        /// <summary>
        /// SIM message storage
        /// </summary>
        [Description("SIM message storage")]
        public static string SIM => "SM";

        /// <summary>
        /// Status report storage
        /// </summary>
        [Description("Status report storage")]
        public static string StatusReport => "SR";
    }
}
