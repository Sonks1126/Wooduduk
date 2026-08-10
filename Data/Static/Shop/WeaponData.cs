using Wooduduk.Data.Static.StaticEnum;
using UnityEngine;

namespace Wooduduk.Data.Static.Shop
{
    /// <summary>
    /// 무기는 룰렛 확률을 변형하는 시스템
    /// </summary>
    [System.Serializable]
    public class WeaponData
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
        /// 아이콘 (로비 UI용)
        /// </summary>
        public Sprite _icon;

        /// <summary>
        /// 외형 프리팹 ID (3D 모델 / 애니메이션 연결용)
        /// </summary>
        public GameObject _prefab;

        /// <summary>
        /// 장착 시 VFX / SFX 세트
        /// </summary>
        public string _visualEffectSetId;

        public bool _isOwned;
    }
}
