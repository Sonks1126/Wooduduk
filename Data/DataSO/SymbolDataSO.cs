using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static.Roulette;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "SymbolData", menuName = "GameData/SymbolData")]
    public class SymbolDataSO : BaseSheetDataSO
    {
        public SymbolData _data;

        public override string ID
        {
            get => _data?._id ?? "Unknown";
            set { _data ??= new SymbolData(); _data._id = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new SymbolData();

            _data._id = ParseString(dict, "ID");
            _data._name = ParseString(dict, "Name");
            _data._symbolType = ParseEnum<SymbolType>(dict, "Symbol");
            _data._baseWeight = ParseFloat(dict, "Weight");
        }
    }
}