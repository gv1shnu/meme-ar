using System.Collections.Generic;
using MemeAR.Events;
using MemeAR.Memes;

namespace MemeAR.Retrieval
{
    /// <summary>Local, allocation-light retrieval: event-type support + people-count gate.</summary>
    public sealed class LocalMemeRetriever : IMemeRetriever
    {
        public void Retrieve(SceneEvent e, IReadOnlyList<MemeDefinition> catalog, List<MemeDefinition> output)
        {
            output.Clear();
            if (catalog == null)
            {
                return;
            }

            int peopleInEvent = e.Participants.Count;
            for (int i = 0; i < catalog.Count; i++)
            {
                MemeDefinition m = catalog[i];
                if (m == null)
                {
                    continue;
                }

                if (!m.SupportsEvent(e.Type))
                {
                    continue;
                }

                if (!m.PeopleCountAllowed(peopleInEvent))
                {
                    continue;
                }

                output.Add(m);
            }
        }
    }
}
