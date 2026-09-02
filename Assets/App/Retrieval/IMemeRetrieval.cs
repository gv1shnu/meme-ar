using System.Collections.Generic;
using MemeAR.Events;
using MemeAR.Memes;
using MemeAR.Temporal;

namespace MemeAR.Retrieval
{
    /// <summary>
    /// Filters the catalog down to memes eligible for an event (supported event type,
    /// people-count constraints). Local and synchronous for the MVP; a future
    /// vector/semantic retriever can implement the same interface.
    /// </summary>
    public interface IMemeRetriever
    {
        void Retrieve(SceneEvent e, IReadOnlyList<MemeDefinition> catalog, List<MemeDefinition> output);
    }

    /// <summary>
    /// Scores and orders retrieved memes for an event. Deterministic given the same seed so
    /// selection is testable; jitter keeps runtime output from feeling repetitive.
    /// </summary>
    public interface IMemeRanker
    {
        void Rank(
            SceneEvent e,
            IReadOnlyList<MemeDefinition> retrieved,
            SessionState session,
            double now,
            List<MemeCandidate> output);
    }
}
