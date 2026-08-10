using Wooduduk.Data.Static.StaticEnum;
using UnityEngine;

namespace Wooduduk.Data.Static.Roulette
{
    /// <summary>
    /// 룰렛머신 외형 데이터
    /// (순수 외형 + UI 정보)
    /// </summary>
    [System.Serializable]
    public class RouletteSkinData
    {
        public string _id;
        public string _name;

        public Rarity _rarity;
        public int _token;
        public int _returnGold;
        public float _weight;

        [TextArea]
        /// <summary>
        /// UI 설명
        /// </summary>
        public string _description;

        /// <summary>
        /// 아이콘 
        /// </summary>
        public Sprite _icon; 

        /// <summary>
        /// 실제 스킨 프리팹 
        /// </summary>
        public GameObject _prefab;

        /// <summary>
        /// 착용 시 VFX/SFX 세트
        /// </summary>
        public string _visualEffectSetId;

        public bool _isOwned;
    }
}