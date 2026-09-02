namespace MemeAR.Memes
{
    /// <summary>
    /// Canonical semantic tags. Kept as string constants (not an enum) so meme content can
    /// be authored/extended in data without recompiling, while code that references common
    /// tags still gets a compile-time symbol.
    /// </summary>
    public static class MemeTags
    {
        public const string Awkward = "awkward";
        public const string Surprise = "surprise";
        public const string Food = "food";
        public const string Coding = "coding";
        public const string Group = "group";
        public const string Attention = "attention";
        public const string Reaction = "reaction";
        public const string Confusion = "confusion";
        public const string SideEye = "side_eye";
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Motion = "motion";
        public const string Entrance = "entrance";
        public const string Exit = "exit";
    }
}
