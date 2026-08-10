using System.Collections.Generic;

namespace Wooduduk.Data.Core.TSVConvert
{
    public interface ISheetData
    {
        string ID { get; set; }
        void SetData(Dictionary<string, string> dict);
    }
}