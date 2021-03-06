using System.Diagnostics.CodeAnalysis;
using KRPC.Server;
using KRPC.UI;
using KRPC.Utils;
using KSP.UI.Screens;
using UnityEngine;

namespace KRPC
{
    /// <summary>
    /// Main KRPC addon. Contains the kRPC core, config and UI.
    /// </summary>
    [KSPAddonImproved (KSPAddonImproved.Startup.All, false)]
    [SuppressMessage ("Gendarme.Rules.Correctness", "DeclareEventsExplicitlyRule")]
    sealed public class Addon : MonoBehaviour
    {
        static Configuration config;
        static Core core;
        static Server.Server server;
        static Texture textureOnline;
        static Texture textureOffline;

        ApplicationLauncherButton applauncherButton;
        MainWindow mainWindow;
        InfoWindow infoWindow;
        ClientConnectingDialog clientConnectingDialog;
        ClientDisconnectDialog clientDisconnectDialog;

        static void Init ()
        {
            if (config != null)
                return;

            // Load config
            config = new Configuration ("PluginData/settings.cfg");
            config.Load ();

            // Set up core
            core = Core.Instance;
            core.OneRPCPerUpdate = config.OneRPCPerUpdate;
            core.MaxTimePerUpdate = config.MaxTimePerUpdate;
            core.AdaptiveRateControl = config.AdaptiveRateControl;
            core.BlockingRecv = config.BlockingRecv;
            core.RecvTimeout = config.RecvTimeout;

            // Set up server
            server = new Server.Server (config.Address, config.RPCPort, config.StreamPort);
        }

        /// <summary>
        /// Called whenever a scene change occurs. Ensures the server has been initialized,
        /// (re)creates the UI, and shuts down the server in the main menu.
        /// </summary>
        public void Awake ()
        {
            if (!ServicesChecker.OK)
                return;

            Init ();

            Service.CallContext.SetGameScene (KSPAddonImproved.CurrentGameScene.ToGameScene ());
            Logger.WriteLine ("Game scene switched to " + Service.CallContext.GameScene);
            core.GetUniversalTime = Planetarium.GetUniversalTime;

            // If a game is not loaded, ensure the server is stopped and then exit
            if (KSPAddonImproved.CurrentGameScene != GameScenes.EDITOR &&
                KSPAddonImproved.CurrentGameScene != GameScenes.FLIGHT &&
                KSPAddonImproved.CurrentGameScene != GameScenes.SPACECENTER &&
                KSPAddonImproved.CurrentGameScene != GameScenes.TRACKSTATION) {
                server.Stop ();
                return;
            }

            // Auto-start the server, if required
            if (config.AutoStartServer && !server.Running) {
                Logger.WriteLine ("Auto-starting server");
                StartServer ();
            }

            // (Re)create the UI
            InitUI ();
        }

        [SuppressMessage ("Gendarme.Rules.Concurrency", "WriteStaticFieldFromInstanceMethodRule")]
        void InitUI ()
        {
            // Layout extensions
            GUILayoutExtensions.Init (gameObject);

            // Disconnect client dialog
            clientDisconnectDialog = gameObject.AddComponent<ClientDisconnectDialog> ();

            // Info window
            infoWindow = gameObject.AddComponent<InfoWindow> ();
            infoWindow.Closable = true;
            infoWindow.Visible = config.InfoWindowVisible;
            infoWindow.Position = config.InfoWindowPosition;

            // Main window
            mainWindow = gameObject.AddComponent<MainWindow> ();
            mainWindow.Config = config;
            mainWindow.Server = server;
            mainWindow.Visible = config.MainWindowVisible;
            mainWindow.Position = config.MainWindowPosition;
            mainWindow.ClientDisconnectDialog = clientDisconnectDialog;
            mainWindow.InfoWindow = infoWindow;

            // New connection dialog
            clientConnectingDialog = gameObject.AddComponent<ClientConnectingDialog> ();

            // Set up events
            InitEvents ();

            // Add button to the applauncher
            mainWindow.Closable = true;
            textureOnline = GameDatabase.Instance.GetTexture ("kRPC/icons/applauncher-online", false);
            textureOffline = GameDatabase.Instance.GetTexture ("kRPC/icons/applauncher-offline", false);
            GameEvents.onGUIApplicationLauncherReady.Add (OnGUIApplicationLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add (OnGUIApplicationLauncherDestroyed);
            server.OnStarted += (s, e) => {
                if (applauncherButton != null) {
                    applauncherButton.SetTexture (textureOnline);
                }
            };
            server.OnStopped += (s, e) => {
                if (applauncherButton != null) {
                    applauncherButton.SetTexture (textureOffline);
                }
            };
        }

        void InitEvents ()
        {
            // Main window events
            mainWindow.OnStartServerPressed += (s, e) => StartServer ();
            mainWindow.OnStopServerPressed += (s, e) => {
                server.Stop ();
                clientConnectingDialog.Close ();
            };
            mainWindow.OnHide += (s, e) => {
                config.Load ();
                config.MainWindowVisible = false;
                config.Save ();
            };
            mainWindow.OnShow += (s, e) => {
                config.Load ();
                config.MainWindowVisible = true;
                config.Save ();
            };
            mainWindow.OnMoved += (s, e) => {
                config.Load ();
                var window = s as MainWindow;
                config.MainWindowPosition = window.Position;
                config.Save ();
            };

            // Info window events
            infoWindow.OnHide += (s, e) => {
                config.Load ();
                config.InfoWindowVisible = false;
                config.Save ();
            };
            infoWindow.OnShow += (s, e) => {
                config.Load ();
                config.InfoWindowVisible = true;
                config.Save ();
            };
            infoWindow.OnMoved += (s, e) => {
                config.Load ();
                var window = s as InfoWindow;
                config.InfoWindowPosition = window.Position;
                config.Save ();
            };

            // Server events
            server.OnClientRequestingConnection += (s, e) => {
                if (config.AutoAcceptConnections)
                    e.Request.Allow ();
                else
                    clientConnectingDialog.OnClientRequestingConnection (s, e);
            };
        }

        void OnGUIApplicationLauncherReady ()
        {
            applauncherButton = ApplicationLauncher.Instance.AddModApplication (
                () => mainWindow.Visible = !mainWindow.Visible,
                () => mainWindow.Visible = !mainWindow.Visible,
                null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS,
                server.Running ? textureOnline : textureOffline);
        }

        void OnGUIApplicationLauncherDestroyed ()
        {
            ApplicationLauncher.Instance.RemoveModApplication (applauncherButton);
            applauncherButton = null;
        }

        void StartServer ()
        {
            config.Load ();
            server.RPCPort = config.RPCPort;
            server.StreamPort = config.StreamPort;
            server.Address = config.Address;
            core.OneRPCPerUpdate = config.OneRPCPerUpdate;
            core.MaxTimePerUpdate = config.MaxTimePerUpdate;
            core.AdaptiveRateControl = config.AdaptiveRateControl;
            core.BlockingRecv = config.BlockingRecv;
            core.RecvTimeout = config.RecvTimeout;
            try {
                server.Start ();
            } catch (ServerException e) {
                mainWindow.Errors.Add (e.Message);
            }
        }

        /// <summary>
        /// Destroy the UI.
        /// </summary>
        public void OnDestroy ()
        {
            if (!ServicesChecker.OK)
                return;

            // Destroy the UI
            if (applauncherButton != null)
                OnGUIApplicationLauncherDestroyed ();
            GameEvents.onGUIApplicationLauncherReady.Remove (OnGUIApplicationLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove (OnGUIApplicationLauncherDestroyed);
            Object.Destroy (mainWindow);
            Object.Destroy (clientConnectingDialog);
            GUILayoutExtensions.Destroy ();
        }

        /// <summary>
        /// Stop the server if running
        /// </summary>
        [SuppressMessage ("Gendarme.Rules.Correctness", "MethodCanBeMadeStaticRule")]
        public void OnApplicationQuit ()
        {
            if (server.Running)
                server.Stop ();
        }

        /// <summary>
        /// GUI update
        /// </summary>
        [SuppressMessage ("Gendarme.Rules.Correctness", "MethodCanBeMadeStaticRule")]
        public void OnGUI ()
        {
            GUILayoutExtensions.OnGUI ();
        }

        /// <summary>
        /// Trigger server update
        /// </summary>
        [SuppressMessage ("Gendarme.Rules.Correctness", "MethodCanBeMadeStaticRule")]
        public void FixedUpdate ()
        {
            if (!ServicesChecker.OK)
                return;
            if (server != null && server.Running)
                core.Update ();
        }
    }
}
