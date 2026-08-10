using UnityEngine;
using Wooduduk.Data.Static;

namespace Wooduduk.Data.DataSO
{
    /// <summary>
    /// 게임 전역 밸런스 SO
    /// 값은 인스펙터에서 직접 편집
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "GameData/GameBalance")]
    public class GameBalanceDataSO : ScriptableObject
    {
        public GameBalanceData _data;
    }
}