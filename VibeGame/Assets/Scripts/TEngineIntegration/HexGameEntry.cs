using UnityEngine;

namespace HexDemo
{
    [DefaultExecutionOrder(-10000)]
    public sealed class HexGameEntry : MonoBehaviour
    {
        private static HexGameEntry _instance;
        private bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            EnsureExists().StartGameOnce();
        }

        public static HexGameEntry EnsureExists()
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<HexGameEntry>();
            if (_instance != null)
                return _instance;

            var entryObject = new GameObject(nameof(HexGameEntry));
            _instance = entryObject.AddComponent<HexGameEntry>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            HexGameModule.Initialize();
        }

        private void Start()
        {
            StartGameOnce();
        }

        public void StartGameOnce()
        {
            if (_started)
                return;

            _started = true;
            HexGameModule.StartGame();
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            HexGameModule.Shutdown();
            _instance = null;
        }
    }
}
