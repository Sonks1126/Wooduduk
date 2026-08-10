using Wooduduk.Data.Static.StaticEnum;
using System.Collections.Generic;
using UnityEngine;


namespace Wooduduk.Data.Static.Shop
{
    /// <summary>
    /// 카드 = 게임 규칙을 바꾸는 장착형 아이템
    /// (룰렛 확률 / 구조 / 보상 변경)
    /// </summary>
    [System.Serializable]
    public class CardData
    {
        public string _id;
        public string _name;

        // 카드 구매 비용 = SlotEconomy 권위(_cardBaseCost/_cardCostStep, 전역 점증). 카드별 cost 필드 없음(중복 제거).
        public int _count;

        /// <summary>
        /// 등급
        /// </summary>
        public Rarity _rarity;

        [TextArea]
        public string _description;

        /// <summary>
        /// 아이콘
        /// </summary>
        public Sprite _icon;

        /// <summary>
        /// 카드가 적용하는 효과 (룰렛 전에 항상 적용)
        /// </summary>
        public List<EffectData> _effects;
    }
}
