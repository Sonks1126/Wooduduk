using UnityEngine;
using Wooduduk.Data.DataSO;

namespace Wooduduk.Data.Core
{
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        [SerializeField]
        private DataManagerSO _data;

        public DataManagerSO Data => _data;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                _data.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}