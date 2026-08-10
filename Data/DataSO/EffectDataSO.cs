using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Core.TSVConvert;
using Wooduduk.Data.Static;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "EffectData", menuName = "GameData/EffectData")]
    public class EffectDataSO : BaseSheetDataSO
    {
        public EffectData _data;

        // EffectData에는 _id가 없으므로 _effectType을 ID로 사용합니다.
        public override string ID
        {
            get => _data?._effectType ?? "Unknown";
            set { _data ??= new EffectData(); _data._effectType = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new EffectData();

            _data._effectType = ParseString(dict, "EffectType");
            _data._target = ParseEnum<EffectTarget>(dict, "Target"); // Enum 파서 사용
            _data._value1 = ParseFloat(dict, "Value1");
            _data._value2 = ParseFloat(dict, "Value2");
            _data._durationSpin = ParseInt(dict, "DurationSpin");
        }
    }
}