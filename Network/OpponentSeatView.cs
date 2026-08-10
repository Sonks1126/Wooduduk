using UnityEngine;
using TMPro;
using Wooduduk.Network.Firebase.Matchmaking; // RoomLiveData

namespace Wooduduk.Network
{
    // ─────────────────────────────────────────────────────────────────────────────
    // OpponentSeatView — 상대 좌석(2P~4P) 표시전용 뷰.
    //
    // 무엇을 하나:
    //   · 배정된 uid 1명의 RoomLiveData(RoomLiveSync 폴링)를 읽어 자기 좌석 비주얼만 구동.
    //   · 모닥불 on/off(fireLit), 사망 딤(!alive), 점수·콤보·체온 라벨.
    //   · 실제 슬롯머신 아님 — 릴 결과는 동기화 안 되므로 표시 불가(설계: 표시전용 쉘).
    //
    // 왜 이렇게(중요):
    //   · SlotEvent는 "정적" 이벤트다. SlotmachineController/AnimationController/CampfireController를
    //     상대 좌석에 붙이면 내 스핀 1회에 상대 슬롯머신이 전부 반응하고, 상대 모닥불이 내 SFX를 유발한다.
    //   · 그래서 이 뷰는 전역 SlotEvent를 "구독도 발신도 안 한다". 오직 RoomLiveData 폴링만.
    //
    // [씬/프리팹 조립법] (OpponentSeat.prefab — 유저가 Unity에서 1회 제작)
    //   1) SlotMachineFeel 프리팹을 복제(또는 Variant 생성) → 이름 OpponentSeat.
    //   2) 복제본에서 게임플레이 컴포넌트 제거:
    //        SlotmachineController, SlotmachineAnimationController, (슬롯) 오디오 관련, CampfireController.
    //        → 릴/머신 메쉬는 육안용으로 그대로 둔다(정적).
    //   3) 모닥불 불꽃 오브젝트(CampFire의 flameRoot 격) 하나를 자식으로 두거나 남긴다.
    //   4) 월드스페이스 라벨(TextMeshPro) 몇 개 배치: 닉/점수/콤보/체온(원하는 것만).
    //   5) 루트에 이 OpponentSeatView 컴포넌트 부착 → 아래 참조들 연결(원하는 것만, 나머지 비워도 안전).
    //   6) 이 프리팹을 MultiSeatSpawner._opponentSeatPrefab 에 연결.
    //
    // 주의: Bind()는 MultiSeatSpawner가 스폰 직후 호출한다(수동 호출 불필요).
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleComponent]
    public class OpponentSeatView : MonoBehaviour
    {
        #region --- 인스펙터 참조 (전부 선택 — 없는 건 조용히 스킵) ---

        [Header("표시 참조 (프리팹 내부, 원하는 것만 연결)")]
        [Tooltip("모닥불 불꽃 루트. fireLit=true일 때만 SetActive(true).")]
        [SerializeField] private GameObject _flameRoot;

        [Tooltip("사망 딤/오버레이. alive=false일 때 SetActive(true).")]
        [SerializeField] private GameObject _deadOverlay;

        [Tooltip("사망 시 얼어붙는 연출 루트(예: FreezeSlotMachine). 생존=비활성(기본), 사망(alive=false)=활성. fireLit→불꽃과 동일 패턴.")]
        [SerializeField] private GameObject _deathFreezeRoot;

        [Tooltip("사망(체온0/alive=false) 시 비활성화할 오브젝트들 — 예: point light, (1)~(4) 5개. 생존=활성 / 사망=비활성. _deathFreezeRoot와 반대 방향.")]
        [SerializeField] private GameObject[] _deathHideObjects;

        [SerializeField] private TMP_Text _nickLabel;
        [SerializeField] private TMP_Text _scoreLabel;
        [SerializeField] private TMP_Text _comboLabel;
        [SerializeField] private TMP_Text _tempLabel;

        [Tooltip("통합 라벨(선택) — 기존 슬롯 모니터 TMP 하나 재활용용. 닉/점수/콤보/체온을 여러 줄로 한 텍스트에 표시. 개별 라벨과 동시 사용 가능(둘 다 연결하면 둘 다 갱신).")]
        [SerializeField] private TMP_Text _statusLabel;

        [Header("설정")]
        [Tooltip("라이브 데이터 폴링 주기(초). RoomLiveSync/보드와 맞춤 0.33s ≈ 3Hz.")]
        [SerializeField] private float _refreshInterval = 0.33f;

        #endregion

        #region --- 내부 상태 ---

        private string _uid;
        private string _nick;
        private RoomLiveSync _liveSync;
        private float _timer;
        private bool _bound;

        #endregion

        // 스포너가 스폰 직후 1회 호출. nick은 RoomLiveData에 없어 여기서 주입(매치 중 고정).
        public void Bind(string uid, string nick, RoomLiveSync liveSync)
        {
            _uid = uid;
            _nick = string.IsNullOrEmpty(nick) ? uid : nick;
            _liveSync = liveSync;
            _bound = !string.IsNullOrEmpty(uid) && liveSync != null;

            if (_nickLabel != null) _nickLabel.text = _nick;
            _timer = 0f;

            if (!_bound)
                Debug.LogWarning($"[OpponentSeatView] Bind 부실 — uid='{uid}', liveSync={(liveSync != null)}. 이 좌석은 갱신 안 됨.");
            else
                Debug.Log($"[OpponentSeatView] Bind 완료 — uid={uid}, nick={nick}");

            ApplyLive(); // 스폰 즉시 1회 반영(첫 폴링까지 대기 안 함)
        }

        private void Update()
        {
            if (!_bound) return;

            _timer += Time.deltaTime;
            if (_timer < _refreshInterval) return;
            _timer = 0f;

            ApplyLive();
        }

        // RoomLiveData → 좌석 비주얼. GetPlayerLiveData는 non-null 기본객체를 반환하지만 방어적으로 널체크.
        private void ApplyLive()
        {
            RoomLiveData live = _liveSync != null ? _liveSync.GetPlayerLiveData(_uid) : null;

            bool alive = live != null && live.alive;
            bool fireLit = live != null && live.fireLit;
            long score = live != null ? live.score : 0;
            int combo = live != null ? live.combo : 0;
            float temp = live != null ? live.temperature : 0f;

            if (_flameRoot != null && _flameRoot.activeSelf != fireLit) _flameRoot.SetActive(fireLit);
            if (_deadOverlay != null && _deadOverlay.activeSelf != !alive) _deadOverlay.SetActive(!alive);
            // 사망 시 얼음 연출 ON(생존=OFF). fireLit→불꽃과 같은 on/off 토글.
            if (_deathFreezeRoot != null && _deathFreezeRoot.activeSelf != !alive) _deathFreezeRoot.SetActive(!alive);
            // 사망 시 지정 오브젝트(포인트 라이트 등) OFF(생존=ON). _deathFreezeRoot와 반대 방향.
            if (_deathHideObjects != null)
            {
                for (int i = 0; i < _deathHideObjects.Length; i++)
                {
                    var o = _deathHideObjects[i];
                    if (o != null && o.activeSelf != alive) o.SetActive(alive);
                }
            }

            if (_scoreLabel != null) _scoreLabel.text = score.ToString();
            if (_comboLabel != null) _comboLabel.text = "x" + combo;
            if (_tempLabel != null) _tempLabel.text = Mathf.RoundToInt(temp) + "°";

            // 통합 라벨(기존 모니터 재활용) — 닉/점수/콤보/체온 여러 줄. 사망 시 닉 옆 ☠.
            if (_statusLabel != null)
            {
                _statusLabel.text =
                    (_nick ?? string.Empty) + (alive ? string.Empty : " ☠") + "\n" +
                    "▶ 점수 : " + score + "\n" +
                    "▶ 콤보 : x" + combo + "\n" +
                    "▶ 체온 : " + Mathf.RoundToInt(temp) + "°";
            }
        }
    }
}
