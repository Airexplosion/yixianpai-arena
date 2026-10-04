using Yx.ModSdk;

namespace YxArena
{
    // Use the release manager's language (captured by ArenaMod) for all labels.
    // The public SDK reference lacks the instance T helper.
    public static class ModText
    {
        public static string T(this ModContext ctx, string zh, string en) { return Loc.T(zh, en); }
    }
}
