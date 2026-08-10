using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static.Shop;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "CardData", menuName = "GameData/CardData")]
    public class CardDataSO : BaseSheetDataSO
    {
        public CardData _data;

        public override string ID
        {
            get => _data?._id ?? "Unknown";
            set { _data ??= new CardData(); _data._id = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new CardData();

            _data._id = ParseString(dict, "ID");
            _data._name = ParseString(dict, "Name");
            // 시트 'Cost' 컬럼 미사용 — 카드 비용은 SlotEconomy 권위. 임포트 안 함(있어도 무시).
            _data._count = ParseInt(dict, "Count");
            _data._rarity = ParseEnum<Rarity>(dict, "Rarity");
            _data._description = ParseString(dict, "Description");
            _data._effects = ParseEffects(dict, "Effects");
        }
    }
}