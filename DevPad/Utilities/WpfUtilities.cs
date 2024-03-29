﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace DevPad.Utilities
{
    public static class WpfUtilities
    {
        public static ColorConverter ColorConverter { get; } = new ColorConverter();

        public static string ApplicationName => AssemblyUtilities.GetTitle();
        public static string ApplicationVersion => AssemblyUtilities.GetFileVersion();
        public static string ApplicationTitle => ApplicationName + " V" + ApplicationVersion;

        public static void ShowMessage(this Window owner, string text) => TaskDialog.ShowMessage(owner, text, ApplicationTitle);
        public static MessageBoxResult ShowConfirm(this Window owner, string text) => TaskDialog.ShowConfirm(owner, text, ApplicationTitle + " - " + Resources.Resources.Confirmation);
        public static MessageBoxResult ShowQuestion(this Window owner, string text) => TaskDialog.ShowQuestion(owner, text, ApplicationTitle + " - " + Resources.Resources.Confirmation);

        public static void SafeInvoke(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action, priority);
        }

        public static Task SafeInvoke(this Dispatcher dispatcher, Func<Task> action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (dispatcher.CheckAccess())
                return action();

            var tcs = new TaskCompletionSource<object>();
            dispatcher.Invoke(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            return tcs.Task;
        }

        public static Task<T> SafeInvoke<T>(this Dispatcher dispatcher, Func<Task<T>> action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (dispatcher.CheckAccess())
                return action();

            var tcs = new TaskCompletionSource<T>();
            dispatcher.Invoke(async () =>
            {
                try
                {
                    var item = await action();
                    tcs.SetResult(item);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            return tcs.Task;
        }

        public static void MinimizeToScreen(this Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                handle = WindowsUtilities.GetDesktopWindow();
            }

            var wa = Monitor.FromWindow(handle)?.WorkingArea;
            if (wa.HasValue)
            {
                if (window.Width > wa.Value.Width)
                {
                    window.Width = wa.Value.Width;
                }

                if (window.Height > wa.Value.Height)
                {
                    window.Height = wa.Value.Height;
                }
            }
        }

        public static T GetSelectedDataContext<T>(this TreeView treeView)
        {
            if (treeView == null)
                return default;

            if (treeView.SelectedItem == null)
                return default;

            if (typeof(T).IsAssignableFrom(treeView.SelectedItem.GetType()))
                return (T)treeView.SelectedItem;

            object context = null;
            if (treeView.SelectedItem is FrameworkElement fe)
            {
                context = fe.DataContext;
            }

            if (context != null && typeof(T).IsAssignableFrom(context.GetType()))
                return (T)context;

            return default;
        }

        public static IEnumerable<DependencyObject> GetVisualParents(this DependencyObject source)
        {
            if (source == null)
                yield break;

            var parent = VisualTreeHelper.GetParent(source);
            if (parent != null)
            {
                yield return parent;
                foreach (var grandParent in GetVisualParents(parent))
                {
                    yield return grandParent;
                }
            }
        }

        public static DependencyObject GetVisualParent(this DependencyObject source) => source != null ? VisualTreeHelper.GetParent(source) : null;
        public static T GetVisualSelfOrParent<T>(this DependencyObject source) where T : DependencyObject
        {
            if (source == null)
                return default;

            if (source is T t)
                return t;

            if (!(source is Visual) && !(source is Visual3D))
                return default;

            return VisualTreeHelper.GetParent(source).GetVisualSelfOrParent<T>();
        }

        public static T FindFocusableVisualChild<T>(this DependencyObject obj, string name) where T : FrameworkElement
        {
            foreach (var item in obj.EnumerateVisualChildren(true, true).OfType<T>())
            {
                if (item.Focusable && (item.Name == name || name == null))
                    return item;
            }
            return null;
        }

        public static IEnumerable<DependencyProperty> EnumerateMarkupDependencyProperties(object element)
        {
            if (element != null)
            {
                var markupObject = MarkupWriter.GetMarkupObjectFor(element);
                if (markupObject != null)
                {
                    foreach (var mp in markupObject.Properties)
                    {
                        if (mp.DependencyProperty != null)
                            yield return mp.DependencyProperty;
                    }
                }
            }
        }

        public static T FindVisualChild<T>(this DependencyObject obj, string name) where T : FrameworkElement
        {
            foreach (var item in obj.EnumerateVisualChildren(true, true).OfType<T>())
            {
                if (name == null)
                    return item;

                if (item.Name == name)
                    return item;
            }
            return null;
        }

        public static IEnumerable<DependencyObject> EnumerateVisualChildren(this DependencyObject obj, bool recursive = true, bool sameLevelFirst = true)
        {
            if (obj == null)
                yield break;

            if (sameLevelFirst)
            {
                var count = VisualTreeHelper.GetChildrenCount(obj);
                var list = new List<DependencyObject>(count);
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child == null)
                        continue;

                    yield return child;
                    if (recursive)
                    {
                        list.Add(child);
                    }
                }

                foreach (var child in list)
                {
                    foreach (var grandChild in child.EnumerateVisualChildren(recursive, true))
                    {
                        yield return grandChild;
                    }
                }
            }
            else
            {
                var count = VisualTreeHelper.GetChildrenCount(obj);
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child == null)
                        continue;

                    yield return child;
                    if (recursive)
                    {
                        foreach (var dp in child.EnumerateVisualChildren(true, false))
                        {
                            yield return dp;
                        }
                    }
                }
            }
        }

        public static IEnumerable<T> GetChildren<T>(this DependencyObject obj)
        {
            if (obj == null)
                yield break;

            foreach (var item in LogicalTreeHelper.GetChildren(obj))
            {
                if (item == null)
                    continue;

                if (item is T t)
                    yield return t;

                if (item is DependencyObject dep)
                {
                    foreach (var child in dep.GetChildren<T>())
                    {
                        yield return child;
                    }
                }
            }
        }

        public static T GetDataContext<T>(this RoutedEventArgs source) where T : class
        {
            if (source.OriginalSource is FrameworkElement element)
                return element.DataContext as T;

            return default;
        }

        public static T GetSelfOrParent<T>(this FrameworkElement source) where T : FrameworkElement
        {
            while (true)
            {
                if (source == null)
                    return default;

                if (source is T t)
                    return t;

                source = source.Parent as FrameworkElement;
            }
        }
    }
}
