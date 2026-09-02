using System;
using System.IO;
using UnityEngine;

namespace MemeAR.Capture
{
    /// <summary>
    /// Captures the composited frame (camera feed + rendered meme overlays) to a PNG in the
    /// app's persistent data path. Because meme overlays render into a ScreenSpaceOverlay
    /// canvas, a full-screen capture naturally includes them. Platform share sheets are a
    /// later, platform-specific step; this keeps the capture path in place without coupling
    /// it to the core architecture.
    /// </summary>
    public sealed class ScreenshotService : MonoBehaviour
    {
        public event Action<string> ScreenshotSaved;

        /// <summary>Captures at end of frame and writes a timestamped PNG. Returns the path.</summary>
        public string Capture()
        {
            string fileName = $"memear_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            ScreenCapture.CaptureScreenshot(fileName);
            // CaptureScreenshot writes relative to persistentDataPath on device.
            ScreenshotSaved?.Invoke(path);
            return path;
        }
    }
}
