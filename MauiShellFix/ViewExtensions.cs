#nullable enable

namespace MauiShellFix.Library.Common
{
    internal static partial class Extensions
    {
        internal static IMauiContext? FindMauiContext(this Element element, bool fallbackToAppMauiContext = false)
        {
            if (element is IElement fe && fe.Handler?.MauiContext != null)
                return fe.Handler.MauiContext;

            foreach (var parent in element.GetParentsPath())
            {
                if (parent is IElement parentView && parentView.Handler?.MauiContext != null)
                    return parentView.Handler.MauiContext;
            }

            return fallbackToAppMauiContext ? Application.Current?.FindMauiContext() : default;
        }

        internal static IEnumerable<Element> GetParentsPath(this Element self)
        {
            Element current = self;

            while (!IsApplicationOrNull(current.RealParent))
            {
                current = current.RealParent;
                yield return current;
            }
        }

        internal static bool IsApplicationOrNull(object? element) =>
            element == null || element is IApplication;
    }
}