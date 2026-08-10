using System.Collections.Generic;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.Static
{
    /// <summary>
    /// 막걸리 = 스핀 이후 발생하는 랜덤 이벤트 테이블
    /// (스핀 결과 기반 추가 효과 시스템)
    /// </summary>
    [System.Serializable]
    public class MakgeolliEffectData
    {
        public string _id;
        public string _name;
        public BuffType _type;
        public int _durationSpin;
        public List<EffectData> _effects;
    }
}