using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "MakgeolliEffectData", menuName = "GameData/MakgeolliEffectData")]
    public class MakgeolliEffectDataSO : BaseSheetDataSO
    {
        public MakgeolliEffectData _data;

        public override string ID
        {
            get => _data?._id ?? "Unknown";
            set { _data ??= new MakgeolliEffectData(); _data._id = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new MakgeolliEffectData();

            _data._id = ParseString(dict, "ID");
            _data._name = ParseString(dict, "Name");
            _data._type = ParseEnum<BuffType>(dict, "Type");
            _data._durationSpin = ParseInt(dict, "Dur");
            _data._effects = ParseEffects(dict, "Effects");
        }
    }
}