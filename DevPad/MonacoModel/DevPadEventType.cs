namespace DevPad.MonacoModel
{
    public enum DevPadEventType
    {
        Unknown = 0,
        Load = 1,
        ContentChanged = 2,
        KeyUp = 3,
        KeyDown = 4,
        EditorCreated = 5,
        EditorLostFocus = 6,
        EditorGotFocus = 7,
        CursorPositionChanged = 8,
        CursorSelectionChanged = 9,
        ModelLanguageChanged = 10,
    }
}
