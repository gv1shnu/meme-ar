using MemeAR.Memes;

namespace MemeAR.Retrieval
{
    /// <summary>A meme paired with the score the ranker assigned it for a given event.</summary>
    public readonly struct MemeCandidate
    {
        public readonly MemeDefinition Meme;
        public readonly float Score;

        public MemeCandidate(MemeDefinition meme, float score)
        {
            Meme = meme;
            Score = score;
        }
    }
}
