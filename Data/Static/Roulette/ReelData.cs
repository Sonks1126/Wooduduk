using System;

namespace Wooduduk.Data.Static.Roulette
{
    /// <summary>
    /// 릴 = 슬롯 연출 구조 (확률과 무관)
    /// </summary>
    [Serializable]
    public class ReelData
    {
        /// <summary> 릴(칸) 개수 — 기본 슬롯은 5릴 </summary>
        public int _reelCount = 5;

        /// <summary>
        /// 릴 하나당 보이는 슬롯 개수
        /// </summary>
        public int _symbolsPerReel = 3;

        public float _spinSpeed = 1f;

        public float _stopDelay = 0.2f;
    }
}