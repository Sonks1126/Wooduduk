//using FishNet.Object;
//using FishNet.Object.Synchronizing;
//using FishNet.Component.Transforming;
//using FishNet.Component.Animating;
//using UnityEngine;
//using UnityEngine.AI;
//using Wooduduk.AI.EnemyAI;
//using Wooduduk.Combat;
//using Wooduduk.Core;
//using Wooduduk.Player;

//namespace Wooduduk.Network.NetworkData
//{
//    [RequireComponent(typeof(NetworkObject))]
//    [RequireComponent(typeof(NetworkTransform))]
//    [RequireComponent(typeof(NetworkAnimator))]
//    public class NetworkAI : NetworkBehaviour
//    {
//        private BaseAIController _aiController;
//        private NavMeshAgent _navAgent;
//        private Rigidbody _rb;
//        private AttackLogic _attackLogic;
//        private PlayerHealth _health;
//        private Animator _animator;

//        // HP 동기화
//        private readonly SyncVar<float> _maxHp = new();
//        private readonly SyncVar<float> _currentHp = new();

//        private void Awake()
//        {
//            _aiController = GetComponent<BaseAIController>();
//            _navAgent = GetComponent<NavMeshAgent>();
//            _rb = GetComponent<Rigidbody>();
//            _attackLogic = GetComponent<AttackLogic>();
//            _health = GetComponent<PlayerHealth>();
//            _animator = GetComponent<Animator>();
//        }

//        public override void OnStartNetwork()
//        {
//            base.OnStartNetwork();

//            // 서버 권한 (호스트 또는 전용 서버)
//            if (base.IsServerInitialized)
//            {
//                InitializeServer();
//            }
//            // 순수 클라이언트 권한
//            else
//            {
//                InitializeClient();
//            }
//        }

//        private void InitializeServer()
//        {
//            if (_aiController) _aiController.enabled = true;
//            if (_navAgent) _navAgent.enabled = true;
//            if (_rb) _rb.isKinematic = true;

//            // AI 종류에 따라 AttackLogic이 없을 수 있음 (예: GoldenAI)
//            if (_attackLogic) _attackLogic.enabled = true;

//            // 초기 HP 설정
//            if (_health)
//            {
//                var stats = GetComponent<IPlayerStats>();
//                _maxHp.Value = stats != null ? stats.MaxHealth : 100f;
//                _currentHp.Value = _maxHp.Value;
//            }
//        }

//        private void InitializeClient()
//        {
//            // 클라이언트는 연산을 하지 않고 눈에 보이는 것만 처리함
//            if (_aiController) _aiController.enabled = false;
//            if (_navAgent) _navAgent.enabled = false;
//            if (_rb) _rb.isKinematic = true; // 클라이언트 물리 연산 방지
//            if (_attackLogic) _attackLogic.enabled = false;
//        }

//        // 서버에서 HP 변화가 있을 때 호출하여 모든 클라이언트에 동기화
//        [Server]
//        public void SetServerHp(float maxHp, float currentHp)
//        {
//            _maxHp.Value = maxHp;
//            _currentHp.Value = currentHp;
//        }

//        // AI 소멸 로직
//        [Server]
//        public void DespawnAI()
//        {
//            if (IsSpawned)
//            {
//                ServerManager.Despawn(NetworkObject);
//            }
//        }
//    }
//}