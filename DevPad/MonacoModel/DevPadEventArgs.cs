using System;
using System.Text.Json;

namespace DevPad.MonacoModel
{
    public class DevPadEventArgs : EventArgs
    {
        private readonly Lazy<JsonDocument> _document;

        public DevPadEventArgs(DevPadEventType type, string json = null)
        {
            EventType = type;
            Json = json;
            _document = new Lazy<JsonDocument>(() =>
            {
                if (json == null)
                    return null;

                return JsonSerializer.Deserialize<JsonDocument>(json);
            });
        }

        public DevPadEventType EventType { get; }
        public string Json { get; }
        public JsonDocument Document => _document.Value;
        public JsonElement RootElement => (Document?.RootElement).GetValueOrDefault();

        public override string ToString()
        {
            var str = EventType.ToString();
            if (Json != null)
            {
                str += " " + Json;
            }
            return str;
        }
    }
}
