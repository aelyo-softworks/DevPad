using DevPad.Resources;

namespace DevPad
{
    public enum PasteAction
    {
        [LocalizedDescription(nameof(DoNothing))]
        DoNothing,

        [LocalizedDescription(nameof(AutoDetectLanguage))]
        AutoDetectLanguage,

        [LocalizedDescription(nameof(AutoDetectLanguageAndFormat))]
        AutoDetectLanguageAndFormat,
    }
}
