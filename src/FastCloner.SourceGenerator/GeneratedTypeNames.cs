namespace FastCloner.SourceGenerator;

/// <summary>
/// Fully-qualified names emitted into generated source. Using global:: names means
/// generated files never depend on a using directive or a nested type that may not exist.
/// </summary>
internal static class GeneratedTypeNames
{
    /// <summary>
    /// Identity-tracking state used by generated clone methods and collection helpers.
    /// Always the public Shared type — never a per-file nested class.
    /// </summary>
    public const string CloneState = "global::FastCloner.SourceGenerator.Shared.FcGeneratedCloneState";

    /// <summary>
    /// State meaning "explicitly do not preserve identity". Emitted for members marked
    /// <c>[FastClonerPreserveIdentity(false)]</c>; <c>null</c> cannot express that because it
    /// means "no state supplied, use the target type's own default". A fresh instance is produced
    /// per opted-out invocation because the state has to carry that subgraph's in-flight mappings.
    /// </summary>
    public static string SuppressIdentity(string stateVar) =>
        $"global::FastCloner.SourceGenerator.Shared.FcGeneratedCloneState.SuppressIdentity({stateVar})";
}
