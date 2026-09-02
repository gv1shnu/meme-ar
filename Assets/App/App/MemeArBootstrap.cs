using UnityEngine;
using MemeAR.Capture;
using MemeAR.DebugTools;
using MemeAR.Infrastructure;
using MemeAR.Memes;

namespace MemeAR.AppRoot
{
    /// <summary>
    /// Zero-asset bootstrap. If the loaded scene has no <see cref="AppController"/>, this
    /// builds a runnable Simulation-mode rig at startup (camera, controller, renderer,
    /// screenshot service, debug HUD). This lets the full pipeline be demonstrated without a
    /// hand-authored .unity scene — useful for CI/headless review and for a first run — while
    /// a production AR scene can still wire everything explicitly and this stays out of the
    /// way.
    ///
    /// Config/catalog are loaded from Resources ("MemeArConfig"/"MemeCatalog") when present,
    /// otherwise sensible runtime defaults are created.
    /// </summary>
    public static class MemeArBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<AppController>() != null)
            {
                return; // an authored scene is in control
            }

            MemeArConfig config = Resources.Load<MemeArConfig>("MemeArConfig");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MemeArConfig>();
            }

            MemeCatalog catalog = Resources.Load<MemeCatalog>("MemeCatalog");

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            }

            // Build the rig inactive so component fields are assigned BEFORE Awake runs
            // (AddComponent on an active GameObject would call Awake immediately).
            var root = new GameObject("MemeAR");
            root.SetActive(false);

            var controller = root.AddComponent<AppController>();
            controller.config = config;
            controller.catalog = catalog;
            controller.arCamera = cam;

            // AppController ensures its own MemeRenderer in Awake; do not add a second one.
            var screenshots = root.AddComponent<ScreenshotService>();

            var hud = root.AddComponent<DebugHud>();
            hud.controller = controller;
            hud.screenshots = screenshots;

            Object.DontDestroyOnLoad(root);
            root.SetActive(true);
        }
    }
}
