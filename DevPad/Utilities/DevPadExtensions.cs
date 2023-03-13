using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using DevPad.MonacoModel;

namespace DevPad.Utilities
{
    public static class DevPadExtensions
    {
        public static string Nullify(this string text)
        {
            if (text == null)
                return null;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var t = text.Trim();
            return t.Length == 0 ? null : t;
        }

        public static bool EqualsIgnoreCase(this string thisString, string text) => EqualsIgnoreCase(thisString, text, false);
        public static bool EqualsIgnoreCase(this string thisString, string text, bool trim)
        {
            if (trim)
            {
                thisString = thisString.Nullify();
                text = text.Nullify();
            }

            if (thisString == null)
                return text == null;

            if (text == null)
                return false;

            if (thisString.Length != text.Length)
                return false;

            return string.Compare(thisString, text, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static string GetAllMessages(this Exception exception) => GetAllMessages(exception, Environment.NewLine);
        public static string GetAllMessages(this Exception exception, string separator)
        {
            if (exception == null)
                return null;

            var sb = new StringBuilder();
            AppendMessages(sb, exception, separator);
            return sb.ToString().Replace("..", ".");
        }

        private static void AppendMessages(StringBuilder sb, Exception e, string separator)
        {
            if (e == null)
                return;

            // this one is not interesting...
            if (!(e is TargetInvocationException))
            {
                if (sb.Length > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(e.Message);
            }
            AppendMessages(sb, e.InnerException, separator);
        }

        public static string GetInterestingExceptionMessage(this Exception exception) => GetInterestingException(exception)?.Message;
        public static Exception GetInterestingException(this Exception exception)
        {
            if (exception is TargetInvocationException tie && tie.InnerException != null)
                return GetInterestingException(tie.InnerException);

            return exception;
        }

        private static readonly ConcurrentDictionary<string, Timer> _doWhenIdleTimers = new ConcurrentDictionary<string, Timer>();

        // dueTime = 0 do it it was requested before ("flush if any")
        // duetime = -1 do it anyway
        public static void DoWhenIdle(Action action, int dueTime, [CallerMemberName] string actionUniqueKey = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (actionUniqueKey == null)
                throw new ArgumentNullException(nameof(actionUniqueKey));

            if (dueTime <= 0)
            {
                if (_doWhenIdleTimers.TryRemove(actionUniqueKey, out var t))
                {
                    try
                    {
                        t.Dispose();
                    }
                    catch
                    {
                        // continue
                    }
                }
                else if (dueTime == 0)
                    return;

                action();
                return;
            }

            if (!_doWhenIdleTimers.TryGetValue(actionUniqueKey, out var timer))
            {
                timer = new Timer(state =>
                {
                    action();
                    if (_doWhenIdleTimers.TryRemove(actionUniqueKey, out var t))
                    {
                        try
                        {
                            t.Dispose();
                        }
                        catch
                        {
                            // continue
                        }
                    }
                }, null, Timeout.Infinite, Timeout.Infinite);
                timer = _doWhenIdleTimers.AddOrUpdate(actionUniqueKey, timer, (k, o) =>
                {
                    try
                    {
                        o.Dispose();
                    }
                    catch
                    {
                        // continue;
                    }
                    return timer;
                });
            }
            timer.Change(dueTime, Timeout.Infinite);
        }

        public static async Task DoWhenIdle(Func<Task> action, int dueTime, [CallerMemberName] string actionUniqueKey = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (actionUniqueKey == null)
                throw new ArgumentNullException(nameof(actionUniqueKey));

            if (dueTime <= 0)
            {
                if (_doWhenIdleTimers.TryRemove(actionUniqueKey, out var t))
                {
                    try
                    {
                        t.Dispose();
                    }
                    catch
                    {
                        // continue
                    }
                }
                else if (dueTime == 0)
                    return;

                await action();
                return;
            }

            if (!_doWhenIdleTimers.TryGetValue(actionUniqueKey, out var timer))
            {
                timer = new Timer(async state =>
                {
                    await action();
                    if (_doWhenIdleTimers.TryRemove(actionUniqueKey, out var t))
                    {
                        try
                        {
                            t.Dispose();
                        }
                        catch
                        {
                            // continue
                        }
                    }
                }, null, Timeout.Infinite, Timeout.Infinite);
                timer = _doWhenIdleTimers.AddOrUpdate(actionUniqueKey, timer, (k, o) =>
                {
                    try
                    {
                        o.Dispose();
                    }
                    catch
                    {
                        // continue;
                    }
                    return timer;
                });
            }
            timer.Change(dueTime, Timeout.Infinite);
        }

        public static void SetImage(this LanguageExtensionPoint lang, MenuItem item, SHIL shil = SHIL.SHIL_SMALL)
        {
            if (lang?.Extensions == null)
                return;

            foreach (var ext in lang.Extensions)
            {
                var image = IconUtilities.GetExtensionIconAsImageSource(ext, shil);
                if (image != null)
                {
                    item.Icon = new Image { Source = image };
                    break;
                }
            }
        }
    }
}
