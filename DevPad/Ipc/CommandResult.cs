namespace DevPad.Ipc
{
    public sealed class CommandResult
    {
        internal CommandResult(int hresult, object output)
        {
            HResult = hresult;
            Output = output;
        }

        public int HResult { get; }
        public object Output { get; }
        public override string ToString() => HResult.ToString() + " " + Output;
    }
}
