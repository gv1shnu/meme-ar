using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MemeAR.Memes
{
    /// <summary>
    /// Discovers user-provided meme packs under StreamingAssets/MemePacks/&lt;pack&gt;/pack.json
    /// and parses them into runtime meme definitions. Loading is best-effort and never throws:
    /// if the folder is absent or unreadable it simply returns nothing and the app runs on its
    /// bundled placeholder catalog.
    ///
    /// Note: direct file reads work in the Editor and on desktop/iOS. On Android StreamingAssets
    /// lives inside the compressed APK, so a production Android build should stream these via
    /// UnityWebRequest instead — that path is a documented follow-up; the manifest/parser are
    /// platform-independent.
    /// </summary>
    public static class MemePackLoader
    {
        public const string PacksRoot = "MemePacks";

        public static List<MemeDefinition> LoadAll()
        {
            var all = new List<MemeDefinition>();
            string root = Path.Combine(Application.streamingAssetsPath, PacksRoot);

            try
            {
                if (!Directory.Exists(root))
                {
                    return all;
                }

                foreach (string packDir in Directory.GetDirectories(root))
                {
                    string manifestPath = Path.Combine(packDir, "pack.json");
                    if (!File.Exists(manifestPath))
                    {
                        continue;
                    }

                    string json = File.ReadAllText(manifestPath);
                    string subfolder = PacksRoot + "/" + Path.GetFileName(packDir);
                    all.AddRange(MemePackParser.Parse(json, subfolder));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MemePackLoader] Skipped pack loading: {ex.Message}");
            }

            return all;
        }
    }
}
