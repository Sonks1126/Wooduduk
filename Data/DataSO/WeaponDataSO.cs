using Wooduduk.Data.Static.Shop;
using Wooduduk.Data.Static.StaticEnum;
using System.Collections.Generic;
using UnityEngine;

namespace Wooduduk.Data.DataSO
{
    /// <summary>
    /// 무기 스킨 SO (코스메틱 — 게임플레이 영향 없음).
    /// 로비에서 UserData._equippedWeaponId 로 외형만 적용.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "GameData/WeaponData")]
    public class WeaponDataSO : BaseSheetDataSO
    {
        public WeaponData _data;

        public override string ID
        {
            get => _data?._id ?? "Unknown";
            set { _data ??= new WeaponData(); _data._id = value; }
        }

        public override void SetData(Dictionary<string, string> dict)
        {
            _data ??= new WeaponData();

            _data._id = ParseString(dict, "ID");
            _data._name = ParseString(dict, "Name");
            _data._rarity = ParseEnum<Rarity>(dict, "Rarity");
            _data._token = ParseInt(dict, "Token");
            _data._returnGold = ParseInt(dict, "ReturnGold");
            _data._weight = ParseFloat(dict, "Weight");
            _data._description = ParseString(dict, "Description");
            _data._visualEffectSetId = ParseString(dict, "VfxSet");
            Debug.Log($"[WeaponData] ID={_data._id} Token={_data._token} ReturnGold={_data._returnGold}");
        }
    }
}