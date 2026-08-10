namespace Wooduduk.Data.Static.StaticEnum
{
    public enum SymbolType
    {
        SWING,    // 1+ 명중, 갯수만큼 배율
        PERFECT,  // 2+ 퍼펙트 격상
        INSTANT,  // 도토리 즉발 재화
        WILD,     // 행운, 과반수 치환
        BOOST,    // 불빠따 이번배율+다음스핀 디버프
        BLANK,    // 옹이 무효
        FAIL,     // 좀벌레 2+ 실패
        TRIGGER,  // 막걸리 3매치 → 막걸리표 발동
    }
}
