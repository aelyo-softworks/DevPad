namespace DevPad.MonacoModel
{
    public class DevPadLoadEventArgs : DevPadEventArgs
    {
        public DevPadLoadEventArgs()
            : base(DevPadEventType.Load)
        {
        }

        public string DocumentText { get; set; }
    }
}
