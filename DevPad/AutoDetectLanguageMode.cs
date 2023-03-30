using DevPad.Resources;

namespace DevPad
{
    public enum AutoDetectLanguageMode
    {
        [LocalizedDescription(nameof(AutoDetect))]
        AutoDetect,

        [LocalizedDescription(nameof(DontAutoDetect))]
        DontAutoDetect,

        [LocalizedDescription(nameof(PromptIfLanguageChanges))]
        PromptIfLanguageChanges,
    }
}
