using System.Collections.Generic;
using MemeAR.Events;
using MemeAR.Memes;
using MemeAR.Prediction;
using MemeAR.Temporal;

namespace MemeAR.Retrieval
{
    /// <summary>
    /// Small "ammunition" cache of ranked candidates prepared BEFORE an event confirms.
    /// When a potential event is published (from prediction), candidates are retrieved and
    /// ranked and stored under the correlation id. When the real event fires, the cached
    /// ranking is used immediately — the punchline path never waits on retrieval/ranking,
    /// and (later) never on a remote service.
    ///
    /// Entries expire so the cache stays bounded and never serves stale predictions.
    /// </summary>
    public sealed class ReactionCandidateCache
    {
        private sealed class Entry
        {
            public readonly List<MemeCandidate> Candidates = new List<MemeCandidate>(8);
            public double PreparedAt;
            public EventType Type;
        }

        private readonly IMemeRetriever _retriever;
        private readonly IMemeRanker _ranker;
        private readonly double _ttlSeconds;

        private readonly Dictionary<long, Entry> _byCorrelation = new Dictionary<long, Entry>();
        private readonly Stack<Entry> _pool = new Stack<Entry>();
        private readonly List<MemeDefinition> _retrieveScratch = new List<MemeDefinition>(16);
        private readonly List<long> _expireScratch = new List<long>();

        public int Count => _byCorrelation.Count;

        public ReactionCandidateCache(IMemeRetriever retriever, IMemeRanker ranker, double ttlSeconds = 2.0)
        {
            _retriever = retriever;
            _ranker = ranker;
            _ttlSeconds = ttlSeconds;
        }

        /// <summary>Prepares candidates for a predicted event and caches them by correlation id.</summary>
        public void Prepare(
            PotentialEvent potential,
            IReadOnlyList<MemeDefinition> catalog,
            SessionState session,
            double now)
        {
            if (_byCorrelation.ContainsKey(potential.CorrelationId))
            {
                // Refresh timestamp so an ongoing anticipation stays warm.
                _byCorrelation[potential.CorrelationId].PreparedAt = now;
                return;
            }

            // Build a synthetic event so retrieval/ranking see the predicted type/participants.
            var proxy = new SceneEvent(potential.CorrelationId, potential.Type, now, EventPhase.Anticipation, potential.Probability);
            if (potential.PrimaryParticipant != null)
            {
                proxy.Participants.Add(potential.PrimaryParticipant);
            }

            if (potential.PrimaryObject != null)
            {
                proxy.Objects.Add(potential.PrimaryObject);
            }

            Entry entry = _pool.Count > 0 ? _pool.Pop() : new Entry();
            entry.Candidates.Clear();
            entry.PreparedAt = now;
            entry.Type = potential.Type;

            _retriever.Retrieve(proxy, catalog, _retrieveScratch);
            _ranker.Rank(proxy, _retrieveScratch, session, now, entry.Candidates);

            _byCorrelation[potential.CorrelationId] = entry;
        }

        /// <summary>
        /// Returns cached candidates for a confirmed event's correlation id, or null if none
        /// were prefetched (caller should then rank on the spot).
        /// </summary>
        public IReadOnlyList<MemeCandidate> Consume(long correlationId, double now)
        {
            if (correlationId != 0 && _byCorrelation.TryGetValue(correlationId, out Entry entry))
            {
                if ((now - entry.PreparedAt) <= _ttlSeconds)
                {
                    _byCorrelation.Remove(correlationId);
                    var result = entry.Candidates;
                    // Note: entry is not pooled here because we hand out its list; pooled on Expire.
                    return result;
                }

                _byCorrelation.Remove(correlationId);
                Recycle(entry);
            }

            return null;
        }

        public void Expire(double now)
        {
            _expireScratch.Clear();
            foreach (var kvp in _byCorrelation)
            {
                if ((now - kvp.Value.PreparedAt) > _ttlSeconds)
                {
                    _expireScratch.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _expireScratch.Count; i++)
            {
                Entry e = _byCorrelation[_expireScratch[i]];
                _byCorrelation.Remove(_expireScratch[i]);
                Recycle(e);
            }
        }

        private void Recycle(Entry e)
        {
            e.Candidates.Clear();
            _pool.Push(e);
        }

        public void Clear()
        {
            _byCorrelation.Clear();
        }
    }
}
