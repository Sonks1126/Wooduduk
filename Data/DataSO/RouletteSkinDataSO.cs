using Wooduduk.Data.Static.Roulette;
using Wooduduk.Data.Static.StaticEnum;
using System.Collections.Generic;
using UnityEngine;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "RouletteSkinData", menuName = "GameData/RouletteSkinData")]
    public class RouletteSkinDataSO : BaseSheetDataSO
    {
        public RouletteSkinData _data;

        public override string ID
        {
            get => _data?._id ?? "Unknown";
            set { _data ??= new RouletteSkinData(); _data._id = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new RouletteSkinData();

            _data._id = ParseString(dict, "ID");
            _data._name = ParseString(dict, "Name");
            _data._rarity = ParseEnum<Rarity>(dict, "Rarity"); // Rarity Enum 파싱
            _data._token = ParseInt(dict, "Token");
            _data._returnGold = ParseInt(dict, "ReturnGold");
            _data._weight = ParseFloat(dict, "Weight");
            _data._description = ParseString(dict, "Description");
            _data._visualEffectSetId = ParseString(dict, "VfxSet");
        }
    }
}