//using FishNet.Connection;
//using FishNet.Object;
//using FishNet.Object.Synchronizing;
//using UnityEngine;
//using Firebase.Auth;

//namespace Wooduduk.Network.NetworkData
//{
//    public class SlotNetworkPlayer : NetworkBehaviour
//    {
//        private readonly SyncVar<string> _userId = new();

//        public string UserId => _userId.Value;

//        public override void OnStartClient()
//        {
//            base.OnStartClient();

//            if (!IsOwner) return;

//            string uid = FirebaseAuth.DefaultInstance.CurrentUser != null
//                ? FirebaseAuth.DefaultInstance.CurrentUser.UserId
//                : System.Guid.NewGuid().ToString();

//            LoginServerRpc(uid);
//        }

//        [ServerRpc]
//        private void LoginServerRpc(string uid)
//        {
//            _userId.Value = uid;
//            GameServerManager.Instance.RegisterPlayer(this);
//        }

//        public void RequestSpin()
//        {
//            if (!IsOwner) return;
//            RequestSpinServerRpc();
//        }

//        [ServerRpc]
//        private void RequestSpinServerRpc()
//        {
//            GameServerManager.Instance.HandleSpinRequest(this);
//        }

//        public void RequestSettle()
//        {
//            if (!IsOwner) return;
//            RequestSettleServerRpc();
//        }

//        [ServerRpc]
//        private void RequestSettleServerRpc()
//        {
//            GameServerManager.Instance.HandleSettleRequest(this);
//        }

//        [TargetRpc]
//        public void TargetReceiveSpinResult(NetworkConnection conn, SpinResultNetData result)
//        {
//            Debug.Log($"[Client] SpinResult 받음: {result._message}");

//            // TODO:
//            // 1. UI 갱신
//            // 2. 릴 애니메이션 트리거
//            // 3. 콤보/장작/체온 표시
//            // 예: SlotClientPresenter.Instance.OnSpinResult(result);
//        }
//    }
//}