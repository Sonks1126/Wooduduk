using UnityEngine;
using Wooduduk.Player;
using Wooduduk.Slot;

namespace Wooduduk.Data.Static
{
    /// <summary>
    /// 전체 게임 템포를 조절하는 핵심 밸런스 (게임당 1개, 전역값)
    /// </summary>
    [System.Serializable]
    public class GameBalanceData
    {

        #region 슬롯 밸런스
        public SlotBalanceConfig _slotBalance = new SlotBalanceConfig();
        #endregion

        #region 슬롯 경제
        public SlotEconomyConfig _slotEconomy = new SlotEconomyConfig();
        #endregion

        #region 슬롯 점수
        public SlotScoreConfig _slotScore = new SlotScoreConfig();
        #endregion

        #region 슬롯 판정
        public SlotJudgeConfig _slotJudge = new SlotJudgeConfig();
        #endregion

        #region 생존
        public SurvivalConfig _survival = new SurvivalConfig();
        #endregion
    }
}