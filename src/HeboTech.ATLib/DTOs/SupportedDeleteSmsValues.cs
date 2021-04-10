using System.Collections.Generic;

namespace HeboTech.ATLib.DTOs
{
    public class SupportedDeleteSmsValues
    {
        public SupportedDeleteSmsValues(IEnumerable<int> indexes, IEnumerable<int> delFlags)
        {
            Indexes = indexes;
            DelFlags = delFlags;
        }

        public IEnumerable<int> Indexes { get; }
        public IEnumerable<int> DelFlags { get; }
    }
}
