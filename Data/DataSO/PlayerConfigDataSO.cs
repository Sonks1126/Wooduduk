using UnityEngine;

namespace Wooduduk.Data.DataSO
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "GameData/PlayerConfig")]
    public class PlayerConfigDataSO : ScriptableObject
    {
        [Header("Pickup (PickupSensor)")]
        public float magnetRadius = 4f;
        public float pullSpeed = 12f;
        public float collectRadius = 0.6f;
        [Header("Interact")]
        public float interactRadius = 2.5f;       // PlayerInteractor._detectRadius
        public float pickupHoldDuration = 0.4f;   // DroppedWeapon._holdDuration
        [Header("Aim (PlayerAim)")]
        public float aimTurnSpeed = 720f;
        public float aimThreshold = 3f;
    }
}
