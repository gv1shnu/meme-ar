using System.Collections.Generic;
using UnityEngine;

namespace MemeAR.Memes
{
    /// <summary>
    /// A bundled collection of meme definitions. Authored as a ScriptableObject so the set
    /// of reactions is data-driven and editable in the editor. If left empty at runtime the
    /// app falls back to <see cref="DefaultMemeCatalog"/> so it is always functional.
    /// </summary>
    [CreateAssetMenu(menuName = "MemeAR/Meme Catalog", fileName = "MemeCatalog")]
    public sealed class MemeCatalog : ScriptableObject
    {
        [Tooltip("Authored meme definitions. If empty, a generated placeholder set is used.")]
        public List<MemeDefinition> memes = new List<MemeDefinition>();

        public IReadOnlyList<MemeDefinition> Memes => memes;
    }
}
