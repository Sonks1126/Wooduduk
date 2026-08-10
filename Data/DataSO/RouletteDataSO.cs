using UnityEngine;
using Wooduduk.Data.Static;

namespace Wooduduk.Data.DataSO
{
    /// <summary>
    /// 룰렛 정의 SO (인스펙터에서 직접 세팅 — 시트 임포트 아님).
    /// 룰렛이 1종이라 단일 SO로 두고, 릴 구조 + 심볼 풀(SymbolDataSO)을
    /// _data 안에서 인스펙터로 연결한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RouletteData", menuName = "GameData/RouletteData")]
    public class RouletteDataSO : ScriptableObject
    {
        public RouletteData _data;
    }
}
