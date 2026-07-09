using TEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexDemo
{
    public static class HexGameModule
    {
        private const string BattleSandboxSceneName = "BattleSandbox";
        private static RootModule _root;
        private static IUpdateDriver _update;
        private static ITimerModule _timer;
        private static IHexNetworkModule _network;

        public static RootModule Root => _root != null ? _root : EnsureRoot();
        public static IUpdateDriver Update => _update ??= Get<IUpdateDriver>();
        public static ITimerModule Timer => _timer ??= Get<ITimerModule>();
        public static IHexNetworkModule Network => _network ??= Get<IHexNetworkModule>();

        public static void Initialize()
        {
            EnsureRoot();
            _ = Update;
            _ = Timer;
            _ = Network;
        }

        public static void StartGame()
        {
            Initialize();
            HexNetworkSessionController.EnsureExists();
            HexDemo.Network.GameNetworkManager.EnsureExists();
            GameEvent.Send(HexGameEvents.GameStarted);

            // BattleSandbox 场景是纯战斗直达入口，不拉起冒险主流程与其 UI。
            if (SceneManager.GetActiveScene().name == BattleSandboxSceneName)
                return;

            HexAdventureController.TryBootstrap();
        }

        public static void Shutdown()
        {
            _update = null;
            _timer = null;
            _network = null;
            _root = null;
        }

        private static RootModule EnsureRoot()
        {
            if (_root != null)
                return _root;

            _root = Object.FindFirstObjectByType<RootModule>();
            if (_root != null)
                return _root;

            var rootObject = new GameObject("[TEngine]");
            Object.DontDestroyOnLoad(rootObject);
            _root = rootObject.AddComponent<RootModule>();
            return _root;
        }

        private static T Get<T>() where T : class
        {
            InitializeRootOnly();
            return ModuleSystem.GetModule<T>();
        }

        private static void InitializeRootOnly()
        {
            if (_root == null)
                EnsureRoot();
        }
    }
}
