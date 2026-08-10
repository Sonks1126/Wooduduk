#if false // [PIVOT-DISABLED 2026-06-22 한충희] 구 추출게임 코드 - 슬롯 피벗으로 컴파일 제외. 복구: 이 줄과 맨끝 #endif 삭제.
using Firebase.Auth;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Wooduduk.Core;
using Wooduduk.Network.Match;
using Wooduduk.Data.DataSO;
using Wooduduk.Data.Runtime;
using Wooduduk.Player;
using Wooduduk.Network.Firebase;
using Wooduduk.Map;
using System;
using FishNet;

namespace Wooduduk.Network.NetworkData
{
    [RequireComponent(typeof(PlayerMovement))]
    public partial class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private DataManagerSO _dataManager;
        [SerializeField] private bool _debugUniqueId = false; // 같은 PC 멀티 클라 테스트용: UID 강제 분리 (출시 시 해제)

        public event Action<float, float> OnHpChanged;
        public event Action<int> OnTraitLevelChanged;   // HUD용 레벨 변경
        public event Action<float> OnSkillCooldownStarted;  // HUD 스킬 쿨다운
        public float CurrentHp => _currentHp.Value;
        public float MaxHp => _maxHp.Value;

        private readonly SyncVar<string> _userId = new();
        private readonly SyncVar<float> _maxHp = new();
        private readonly SyncVar<float> _currentHp = new();
        private readonly SyncVar<int> _collectedWood = new();
        private readonly SyncVar<string> _axeSkinId = new();
        private readonly SyncVar<int> _traitLevel = new(); // 세션 특성 레벨
        private readonly SyncVar<Vector3> _facing = new();

        #region 산불 SyncVar
        private readonly SyncVar<float> _wildfireRadius = new();
        private readonly SyncVar<float> _wildfireDps = new();

        private IInputProvider _input;
        #endregion

        private string _equippedTraitId; // 로그인 시 클라에서 서버로 전달, 스킬 시스템용
        private string _equippedWeaponId = "W001"; // 로그인 시 클라에서 서버로 전달 (전용 서버엔 UserManager 없음)

        private WoodBag _woodBag;
        private WoodStack _woodStack;
        private Vector2 _lastSentInput;
        
        public string UserId => _userId.Value;
        private StyleManager _style;
        
        public string AxeSkinId => _axeSkinId.Value; // StyleManager 보정용
        public int CollectedWood => _collectedWood.Value;
        public int TraitLevel => _traitLevel.Value;
        public Vector3 Facing => _facing.Value;

        private void Awake()
        {
            _style = GetComponent<StyleManager>();
            _woodBag = GetComponent<WoodBag>();
            _woodStack = GetComponent<WoodStack>();
            _input = GetComponent<IInputProvider>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _collectedWood.OnChange += OnSyncWood;
            _currentHp.OnChange += OnSyncHp;
            _axeSkinId.OnChange += OnSyncAxeSkin;
            _currentHp.OnChange += OnWildfireHpCheck;

            // 풀 재사용 대비: 스폰될 때마다 이 오브젝트 1개의 상태를 깨끗이 초기화
            // (이전 점유자가 남긴 사망/비활성/입력 상태 제거)
            _lastSentInput = Vector2.zero;                          // ← 입력 초기화
            if (TryGetComponent<PlayerHealth>(out var health))
                health.ResetForSpawn();                             // ← HP·컴포넌트·애니 리셋
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _collectedWood.OnChange -= OnSyncWood;
            _currentHp.OnChange -= OnSyncHp;
            _axeSkinId.OnChange -= OnSyncAxeSkin;
            _currentHp.OnChange -= OnWildfireHpCheck;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) InitializeClient();

            if (IsOwner)
                Wooduduk.UI.InGameHUDController.Instance?.BindToPlayer(gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Debug.Log($"Spawned Player " + $"OwnerId={Owner.ClientId} " + $"ObjectId={NetworkObject.ObjectId}");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (GameServerManager.Instance == null) return;

            GameServerManager.Instance.PlayerNetworkManager.Unregister(Owner.ClientId, _userId.Value);

            // Debug.Log($"NetworkPlayer 제거 : {Owner.ClientId}");
        }

        private void InitializeClient()
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null)
            {
                string uid = user.UserId;

                // 디버그: 같은 PC에서 여러 클라가 같은 캐시 UID를 공유하는 문제 회피
                if (_debugUniqueId)
                    uid += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

                // 장착 특성/무기 ID를 서버에 전달 (서버는 UserManager 없음)
                string traitId = "";
                string weaponId = "W001";
                if (UserManager.Instance != null && UserManager.Instance.TryGetUser(out var ud))
                {
                    traitId = ud._equippedTraitId ?? "";
                    weaponId = string.IsNullOrEmpty(ud._equippedWeaponId) ? "W001" : ud._equippedWeaponId;
                }

                LoginServerRpc(uid, traitId, weaponId);
            }
            ApplyLoadoutToStats();
            string axeSkinId = "";
            if (UserManager.Instance != null && UserManager.Instance.TryGetUser(out var udSkin))
            {
                axeSkinId = udSkin._selectedSkinId ?? "";
            }
            SetAxeSkinServerRpc(axeSkinId);
        }

        private void Update()
        {
            if (!IsOwner)
                return;
            if (_input == null) return;

            Vector2 input = _input.MoveAxis; // 추상화 폴링
            //Vector2 input = new Vector2(
            //    Input.GetAxisRaw("Horizontal"),
            //    Input.GetAxisRaw("Vertical"));

            // 입력이 바뀔 때만 전송 (멈춤=0도 한 번 전송) → 트래픽 절감
            if (input != _lastSentInput)
            {
                _lastSentInput = input;
                SendInputServerRpc(input);
            }
        }

        private void ApplyLoadoutToStats()
        {
            var stats = GetComponent<PlayerStats>();
            if (stats == null || _dataManager == null) return;
            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var ud)) return;

            var charactor = _dataManager.GetCharacterData("C001");
            var weaponId = string.IsNullOrEmpty(ud._equippedWeaponId) ? "W001" : ud._equippedWeaponId;
            var weapon = _dataManager.GetWeaponData(weaponId);

            var runtime = PlayerFactory.Create(ud, charactor, weapon, _dataManager);
            stats.Initialize(new PlayerRuntimeBaseStats(runtime));

            // ✅ HUD에 장착 무기/특성 이름 표시 (세션 중 변경 없음 → 1회만 전달)
            string weaponName = weapon != null && weapon._data != null ? weapon._data._name : "기본 도끼";

            string traitName = "";
            if (!string.IsNullOrEmpty(ud._equippedTraitId))
            {
                var trait = _dataManager.GetTraitData(ud._equippedTraitId);
                if (trait != null && trait._data != null)
                    traitName = trait._data._name;
            }

            Wooduduk.UI.InGameHUDController.Instance?.UpdateLoadout(weaponName, traitName);
        }

        private void OnSyncWood(int oldValue, int newValue, bool asServer)
        {
            _woodBag?.SetDisplayWood(newValue); // 클라 표시 갱신
            if (asServer) return;
            _woodStack?.OnWoodCollected(newValue - oldValue, gameObject);

        }

        // HP 감소 감지 → 산불 DPS 기반 예상 데미지와 비교하여 산불 데미지만 필터링
        // PvP 근접 공격 등 다른 소스의 HP 감소는 무시
        private void OnWildfireHpCheck(float prev, float next, bool asServer)
        {
            if (asServer || !IsOwner) return;
            if (next >= prev || _wildfireDps.Value <= 0f) return;

            float actual = prev - next;
            float expectedFireDmg = _wildfireDps.Value * GameServerManager.SYNC_RATE;
            if (actual > expectedFireDmg * 2f) return; // 산불 예상치 2배 초과 → PvP/기타 피해로 판정

            FireEvents.RaiseFireDamage(gameObject, actual);
            // Debug.Log($"[WildfireDmg] hp {prev:F1}→{next:F1} dmg={actual:F1} dps={_wildfireDps.Value:F1}");
        }
        public void RequestSkill()
        {
            if (!IsOwner) return;
            RequestSkillServerRpc();
        }

        public void RequestPickup(int amount)
        {
            if (!IsOwner) return;
            RequestPickupServerRpc(amount);

        }

        #region 산불 존 동기화
        // 서버가 산불 존 상태를 SyncVar로 동기화
        public void SetWildfire(float radius, float dps)
        {
            _wildfireRadius.Value = radius;
            _wildfireDps.Value = dps;
        }

        // 서버가 계산한 HP를 SyncVar로 → 모든 클라에 자동 동기화
        public void SetServerHp(float maxHp, float currentHp)
        {
            _maxHp.Value = maxHp;
            _currentHp.Value = currentHp;
        }
        // 서버 런타임 레벨을 SyncVar로 푸시
        public void SetServerTraitLevel(int level)
        {
            _traitLevel.Value = level;
        }
        private void OnSyncHp(float prev, float next, bool asServer)
        {
            if (asServer) return;
            OnHpChanged?.Invoke(next, _maxHp.Value);
        }
        private void OnSyncAxeSkin(string prev, string next, bool asServer)
        {
            if (asServer) return;   
            _style?.Apply(next);    // 변경 감지 -> 적용
        }
        private void OnSyncTraitLevel(int prev, int next, bool asServer)
        {
            OnTraitLevelChanged?.Invoke(next);   // HUD 갱신
            if (asServer) return;
            if (!IsOwner) return;                 // 비오너 PlayerStats는 사용 안 하므로 미러 불필요
            if (_dataManager == null) return;
            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var ud)) return;
            var tid = ud._equippedTraitId;
            if (string.IsNullOrEmpty(tid)) return;
            var traitSO = _dataManager.GetTraitData(tid);
            var stats = GetComponent<IPlayerStats>();
            if (traitSO != null && stats != null)
                TraitLevelRuntime.ReapplyEffects(traitSO, stats, gameObject, next);
        }

        public float WildfireRadius => _wildfireRadius.Value;
        public float WildfireDps => _wildfireDps.Value;
        #endregion
        [Server]
        public void GrantTraitWoodExp(int wood, int[] expTable)
        {
            if (_dataManager == null || string.IsNullOrEmpty(_equippedTraitId)) return;
            var traitSO = _dataManager.GetTraitData(_equippedTraitId);
            if (traitSO == null) return;
            var runtime = GetComponent<PlayerRuntimeBinder>()?.Runtime;
            var stats = GetComponent<IPlayerStats>();
            if (runtime == null || stats == null) return;
            // 레벨 스탯 재계산/ SyncVar 푸시
            TraitLevelRuntime.GrantWoodExp(runtime, wood, traitSO, stats, gameObject, expTable);
            TraitLevelRuntime.EffectTriggers(traitSO, runtime, TraitTriggerKind.WoodChop);
        }
        [ServerRpc]
        private void LoginServerRpc(string uid, string equippedTraitId, string equippedWeaponId)
        {
            Debug.Log($"REGISTER ClientId={Owner.ClientId} UID={uid} Trait={equippedTraitId} Weapon={equippedWeaponId}");

            _userId.Value = uid;
            _equippedTraitId = equippedTraitId;
            _equippedWeaponId = string.IsNullOrEmpty(equippedWeaponId) ? "W001" : equippedWeaponId;

            GameServerManager.Instance.PlayerNetworkManager.Register(Owner.ClientId, uid, this);

            GameServerManager.Instance.RegisterPlayer(Owner.ClientId, _userId.Value, roomCode: null, _equippedTraitId, _equippedWeaponId);

            SendMatchListToClient();
        }

        // 서버로 방 목록 요청
        [ServerRpc]
        private void RequestMatchListServerRpc()
        {
            SendMatchListToClient();
        }

        // 서버에서 현재 방 목록 수집 후 클라로 전송
        private void SendMatchListToClient()
        {
            var list = GameServerManager.Instance.GetMatchList();
            TargetReceiveMatchList(Owner, list);
        }

        // 랜덤 매치 입장 (로비 UI에서 호출)
        [ServerRpc]
        public void JoinRandomMatchServerRpc()
        {
            GameServerManager.Instance.RegisterPlayer(Owner.ClientId, _userId.Value, roomCode: null, _equippedTraitId, _equippedWeaponId);
        }

        // 특정 방 입장 (로비 UI에서 호출)
        [ServerRpc]
        public void JoinMatchServerRpc(string roomCode)
        {
            GameServerManager.Instance.RegisterPlayer(Owner.ClientId, _userId.Value, roomCode, _equippedTraitId, _equippedWeaponId);
        }

        // 새 방 생성 후 입장 (로비 UI에서 호출)
        [ServerRpc]
        public void CreateMatchServerRpc()
        {
            string roomCode = GameServerManager.Instance.CreateNewMatch();
            if (roomCode != null)
                GameServerManager.Instance.RegisterPlayer(Owner.ClientId, _userId.Value, roomCode, _equippedTraitId, _equippedWeaponId);
        }

        // 서버 → 클라: 방 목록 전달
        [TargetRpc]
        private void TargetReceiveMatchList(NetworkConnection conn, MatchListData[] matches)
        {
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 input)
        {
            GameServerManager.Instance.OnReceiveInput(Owner.ClientId, input);
        }

        // EscapeZone에서 호출
        public void RequestExtract()
        {
            if (!IsOwner) return;
            RequestExtractServerRpc();
        }

        [ServerRpc]
        private void RequestExtractServerRpc()
        {
            GameServerManager.Instance.OnRequestExtract(Owner.ClientId);
        }

        // 서버에서 호출 / 판정 결과 회신(Owner)
        public void NotifyExtractResult(bool success)
        {
            TargetExtractResult(Owner, success);
        }

        public void NotifyMatchResult(MatchResultData result)
        {
            TargetMatchResult(Owner, result);
        }

        [TargetRpc]
        private void TargetMatchResult(NetworkConnection connection, MatchResultData result)
        {
            // 로컬 호스트: HandlePlayerExit에서 이미 RaisePlayerExited를 발생시켰으므로 생략
            //if (InstanceFinder.IsServerStarted) return;
            ExitReason reason = result._isExtracted ? ExitReason.Extract
                : (result._isDead ? ExitReason.Death : ExitReason.Disconnect);
            ServerEvents.RaisePlayerExited(result._userId, reason, result);
        }

        [TargetRpc]
        private void TargetExtractResult(NetworkConnection connection, bool success)
        {
            if (!success)
            {
                Debug.Log("미개방/사망/중복");
                return;
            }
            GameEvents.RaisePlayerExtracted(gameObject);
        }

        public void DespawnSelf()
        {
            Debug.Log($"[EXT] DespawnSelf 진입 / IsServerInitialized={IsServerInitialized}");
            if (!IsServerInitialized) return;
            ServerManager.Despawn(NetworkObject);
            Debug.Log("[EXT] Despawn 호출 완료");
        }
        public void DespawnForExtract()
        {
            if (TryGetComponent<PlayerHealth>(out var hp))
            {
                hp.DespawnOnExtract();
            }
        }

        [ServerRpc]
        private void RequestPickupServerRpc(int amount)
        {
            GameServerManager.Instance.OnRequestPickup(Owner.ClientId, amount);
        }
        public void SetServerWood(int wood)
        {
            _collectedWood.Value = wood;
        }
        public void RequestWeaponPickup(NetworkObject droppedWeapon)
        {
            if (!IsOwner || droppedWeapon == null) return;
            RequestWeaponPickupServerRpc(droppedWeapon);
        }
        [ServerRpc]
        private void RequestWeaponPickupServerRpc(NetworkObject droppedWeapon)
        {
            GameServerManager.Instance.OnRequestWeaponPickup(Owner.ClientId, droppedWeapon);
        }
        public void ReportMeleeHit(NetworkObject target)
        {
            if (!IsOwner ||  target == null) return;
            ReportMeleeHitServerRpc(target);
        }
        [ServerRpc]
        private void ReportMeleeHitServerRpc(NetworkObject target)
        {
            GameServerManager.Instance.OnMeleeHit(Owner.ClientId, target);
        }
        [ServerRpc]
        private void RequestSkillServerRpc()
        {
            if (_dataManager == null || string.IsNullOrEmpty(_equippedTraitId)) return;
            var traitSO = _dataManager.GetTraitData(_equippedTraitId);   
            var skill = traitSO != null ? traitSO.ActiveSkill : null;
            bool executed = GetComponent<PlayerActiveSkill>()?.TryExecuteServer(skill) ?? false;
            if (!executed) return;

            if (GetComponent<PlayerMovement>()?.IsRolling == true)   // 구르기 발동한 스킬이면
                ObserversPlayRoll();                                  // 전 클라 연출
            TargetSkillCooldown(Owner, skill.Cooldown);               // 오너 쿨다운 UI

        }
        [ServerRpc]
        private void ObserversPlayRoll() => GetComponent<PlayerAnimator>()?.HandleRoll();

        // 오너에게 쿨다운 전달 (HUD 게이지 시작용)
        [TargetRpc]
        private void TargetSkillCooldown(NetworkConnection conn, float cooldown)
            => OnSkillCooldownStarted?.Invoke(cooldown);
        // 죽기 직전 Gamble 시도
        [Server]
        public bool TryDeathSaveServer()
        {
            if (_dataManager == null || string.IsNullOrEmpty(_equippedTraitId)) return false;
            var traitSO = _dataManager.GetTraitData(_equippedTraitId);
            var skill = traitSO != null ? traitSO.ActiveSkill : null;
            if (skill is not IDeathTriggeredSkill) return false;          // 죽음 트리거형만
            var runtime = GetComponent<PlayerRuntimeBinder>()?.Runtime;
            if (runtime == null) return false;
            GetComponent<PlayerActiveSkill>()?.ExecuteDeathSkill(skill);  // 내부 ReviveFull→runtime 갱신
            return runtime._isDead == false;
        }

        #region 나무 데미지 (서버 권위)
        // 클라 → 서버: 나무 데미지 요청
        public void RequestTreeDamage(string treeId, int damage)
        {
            if (!IsOwner) return;
            RequestTreeDamageServerRpc(treeId, damage);
        }

        [ServerRpc]
        private void RequestTreeDamageServerRpc(string treeId, int damage)
        {
            GameServerManager.Instance.OnRequestTreeDamage(Owner.ClientId, treeId, damage);
        }
        public void SendAim(Vector3 aimDir)
        {
            if (!IsOwner) return;
            SendAimServerRpc(aimDir);
        }
        [ServerRpc]
        private void SendAimServerRpc(Vector3 aimDir)
        {
            _facing.Value = aimDir.normalized;                          // 프록시들이 이 값으로 회전
            GameServerManager.Instance.OnReceiveAim(Owner.ClientId, aimDir); // 런타임 _facing 유지(특성/combat용)
        }
        [ServerRpc]
        private void SetAxeSkinServerRpc(string axeSkinId)
        {
            _axeSkinId.Value = axeSkinId ?? "";
        }
        #endregion

    }
}
#endif
