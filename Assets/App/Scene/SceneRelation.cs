namespace MemeAR.Scene
{
    /// <summary>Kinds of spatial/social relation between scene entities.</summary>
    public enum RelationType
    {
        Near = 0,
        LookingAt,
        ReachingToward,
        ApproachingPerson,
        AttentionOn
    }

    /// <summary>
    /// A directed relation between two entities, e.g. (P1 ReachingToward O1). Subject and
    /// object are entity IDs; the boolean flags disambiguate person vs object references
    /// so consumers never have to parse the ID string.
    /// </summary>
    public readonly struct SceneRelation
    {
        public readonly RelationType Type;
        public readonly string SubjectId;
        public readonly string ObjectId;
        public readonly bool SubjectIsPerson;
        public readonly bool ObjectIsPerson;
        public readonly float Confidence;

        public SceneRelation(
            RelationType type,
            string subjectId,
            bool subjectIsPerson,
            string objectId,
            bool objectIsPerson,
            float confidence)
        {
            Type = type;
            SubjectId = subjectId;
            SubjectIsPerson = subjectIsPerson;
            ObjectId = objectId;
            ObjectIsPerson = objectIsPerson;
            Confidence = confidence;
        }

        public override string ToString()
        {
            return $"{SubjectId} {Type} {ObjectId} ({Confidence:0.00})";
        }
    }
}
