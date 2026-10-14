#if !SHOWDOWN_DOTNET_HARNESS
// Records with init-only properties need this marker type. Unity's .NET Standard 2.1
// profile does not ship it, so the Domain assembly carries its own copy. (The standalone
// dotnet test harness targets net8.0, where the real one exists, and defines
// SHOWDOWN_DOTNET_HARNESS to leave this out.)
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
