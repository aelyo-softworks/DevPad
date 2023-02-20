namespace DevPad.MonacoModel
{
    // https://microsoft.github.io/monaco-editor/docs.html#interfaces/languages.ILanguageExtensionPoint.html
    public class LanguageExtensionPoint
    {
        public string Id { get; set; }
        public string Configuration { get; set; }
        public string[] Extensions { get; set; }
        public string[] FilenamePatterns { get; set; }
        public string[] Filenames { get; set; }
        public string FirstLine { get; set; }
        public string[] Aliases { get; set; }
        public string[] MimeTypes { get; set; }

        public override string ToString() => Id;

        public string Name
        {
            get
            {
                if (Aliases == null || Aliases.Length == 0)
                    return Id;

                return Aliases[0];
            }
        }
    }
}
