﻿#if IOS || MACCATALYST
using CoreGraphics;
using MauiShellFix.Library.Common;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using ObjCRuntime;
using System.Reflection;
using UIKit;

namespace MauiShellFix
{
    public class ShellWorkarounds : ShellRenderer
    {
        public static IShellPageRendererTracker Tracker { get; set; }

        protected override IShellPageRendererTracker CreatePageRendererTracker()
        {
            if (Tracker != null)
                throw new InvalidOperationException("This should have been cleared out by CustomShellSectionRenderer");

            return (Tracker = new CustomShellPageRendererTracker(this));
        }

        protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection)
        {
            return new CustomShellSectionRenderer(this);
        }
    }

    public class CustomShellSectionRootRenderer : ShellSectionRootRenderer
    {
        readonly CustomShellSectionRenderer _customShellSectionRenderer;
        public CustomShellSectionRootRenderer(ShellSection shellSection, IShellContext shellContext, CustomShellSectionRenderer customShellSectionRenderer) : base(shellSection, shellContext)
        {
            _customShellSectionRenderer = customShellSectionRenderer;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            _customShellSectionRenderer.SnagTracker();
        }
    }

    public class CustomShellSectionRenderer : ShellSectionRenderer
    {
        readonly Dictionary<Element, IShellPageRendererTracker> _trackers =
            new();
        readonly UINavigationControllerDelegate _navDelegate;
        public CustomShellSectionRenderer(IShellContext context) : base(context)
        {
            _navDelegate = Delegate as UINavigationControllerDelegate;
            Delegate = new NavDelegate(_navDelegate, this);
            Context = context;
        }

        public IShellContext Context { get; }

        protected override IShellSectionRootRenderer CreateShellSectionRootRenderer(ShellSection shellSection, IShellContext shellContext)
        {
            return new CustomShellSectionRootRenderer(shellSection, shellContext, this);
        }

        public void SnagTracker()
        {
            if (ShellWorkarounds.Tracker is null)
                return;

            _trackers[ShellWorkarounds.Tracker.Page] = ShellWorkarounds.Tracker;
            ShellWorkarounds.Tracker = null;
        }

        protected override void OnNavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            SnagTracker();
            base.OnNavigationRequested(sender, e);
            SnagTracker();
        }

        protected override void OnPushRequested(NavigationRequestedEventArgs e)
        {
            SnagTracker();
            base.OnPushRequested(e);
            SnagTracker();
        }

        protected override void OnInsertRequested(NavigationRequestedEventArgs e)
        {
            SnagTracker();
            base.OnInsertRequested(e);
            SnagTracker();
        }

        public override void PushViewController(UIViewController viewController, bool animated)
        {
            SnagTracker();
            base.PushViewController(viewController, animated);
            SnagTracker();
        }

        class NavDelegate : UINavigationControllerDelegate
        {
            readonly UINavigationControllerDelegate navDelegate;
            readonly CustomShellSectionRenderer _self;

            public NavDelegate(UINavigationControllerDelegate navDelegate, CustomShellSectionRenderer customShellSectionRenderer)
            {
                this.navDelegate = navDelegate;
                _self = customShellSectionRenderer;
            }

            public override void DidShowViewController(UINavigationController navigationController, [Transient] UIViewController viewController, bool animated)
            {
                navDelegate.DidShowViewController(navigationController, viewController, animated);
            }

            public override void WillShowViewController(UINavigationController navigationController, [Transient] UIViewController viewController, bool animated)
            {
                navDelegate.WillShowViewController(navigationController, viewController, animated);

                // Because the back button title needs to be set on the previous VC
                // We want to set the BackButtonItem as early as possible so there is no flickering
                var currentPage = _self.Context?.Shell?.CurrentPage;
                var trackers = _self._trackers;
                if (currentPage?.Handler is IPlatformViewHandler pvh &&
                    pvh.ViewController == viewController &&
                    trackers.TryGetValue(currentPage, out var tracker) &&
                    tracker is CustomShellPageRendererTracker shellRendererTracker)
                {
                    shellRendererTracker.UpdateToolbarItemsInternal(false);
                }
            }
        }
    }

    public class CustomShellPageRendererTracker : ShellPageRendererTracker
    {
        public CustomShellPageRendererTracker(IShellContext context) : base(context)
        {
            Context = context;
        }

        protected override void UpdateTitleView()
        {
            if (!ToolbarReady())
                return;

            if (ViewController?.NavigationItem is null)
            {
                return;
            }

            var titleView = Shell.GetTitleView(Page) ?? Shell.GetTitleView(Context.Shell);
            if (titleView == null)
            {
                var view = ViewController.NavigationItem.TitleView;
                ViewController.NavigationItem.TitleView = null;
                view?.Dispose();
            }
            else
            {
                var view = new CustomTitleViewContainer(titleView);
                ViewController.NavigationItem.TitleView = view;
            }
        }

        internal void UpdateToolbarItemsInternal(bool updateWhenLoaded = true)
        {
            if (updateWhenLoaded && Page.IsLoaded || !updateWhenLoaded)
                UpdateToolbarItems();
        }

        protected override void UpdateToolbarItems()
        {
            base.UpdateToolbarItems();

            if (ViewController?.NavigationItem is null)
            {
                return;
            }

            UpdateBackButtonTitle();
        }

        protected override void UpdateTitle()
        {
            if (!ToolbarReady())
                return;

            base.UpdateTitle();
        }


        Page ToolbarCurrentPage
        {
            get
            {
                var toolBar = (Context.Shell as IToolbarElement).Toolbar;
                var t = toolBar.GetType();
                var property = t.GetField("_currentPage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var result = (Page)property.GetValue(toolBar);
                return result;

            }
        }

        public IShellContext Context { get; }

        bool ToolbarReady()
        {
            return ToolbarCurrentPage == Page;
        }

        void UpdateBackButtonTitle()
        {
            var behavior = Shell.GetBackButtonBehavior(Page);
            var text = behavior.GetPropertyIfSet<string>(BackButtonBehavior.TextOverrideProperty, null);

            var navController = ViewController?.NavigationController;

            if (navController != null)
            {
                var viewControllers = ViewController.NavigationController.ViewControllers;
                var count = viewControllers.Length;

                if (count > 1 && viewControllers[count - 1] == ViewController)
                {
                    var previousNavItem = viewControllers[count - 2].NavigationItem;
                    if (previousNavItem != null)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var barButtonItem = (previousNavItem.BackBarButtonItem ??= new UIBarButtonItem());
                            barButtonItem.Title = text;
                        }
                        else if (previousNavItem.BackBarButtonItem != null)
                        {
                            previousNavItem.BackBarButtonItem = null;
                        }
                    }
                }
            }
        }
    }

    public class CustomTitleViewContainer : UIContainerView
    {
        public CustomTitleViewContainer(View view) : base(view)
        {
            MatchHeight = true;

            if (OperatingSystem.IsIOSVersionAtLeast(11) || OperatingSystem.IsTvOSVersionAtLeast(11))
            {
                TranslatesAutoresizingMaskIntoConstraints = false;
            }
            else
            {
                TranslatesAutoresizingMaskIntoConstraints = true;
                AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            }
        }

        public override CGRect Frame
        {
            get => base.Frame;
            set
            {
                if (!(OperatingSystem.IsIOSVersionAtLeast(11) || OperatingSystem.IsTvOSVersionAtLeast(11)) && Superview != null)
                {
                    value.Y = Superview.Bounds.Y;
                    value.Height = Superview.Bounds.Height;
                }

                base.Frame = value;
            }
        }

        public override void LayoutSubviews()
        {
            if (Height == null || Height == 0)
            {
                UpdateFrame(Superview);
            }

            base.LayoutSubviews();
        }

        public override void WillMoveToSuperview(UIView newSuper)
        {
            UpdateFrame(newSuper);
            base.WillMoveToSuperview(newSuper);
        }

        void UpdateFrame(UIView newSuper)
        {
            if (newSuper is not null && newSuper.Bounds != CGRect.Empty)
            {
                if (!(OperatingSystem.IsIOSVersionAtLeast(11) || OperatingSystem.IsTvOSVersionAtLeast(11)))
                    Frame = new CGRect(Frame.X, newSuper.Bounds.Y, Frame.Width, newSuper.Bounds.Height);

                Height = newSuper.Bounds.Height;
            }
        }

        public override CGSize IntrinsicContentSize => UILayoutFittingExpandedSize;

        public override CGSize SizeThatFits(CGSize size)
        {
            return size;
        }
    }

    public class UIContainerView : UIView
    {
        readonly View _view;
        IPlatformViewHandler _renderer;
        UIView _platformView;
        bool _disposed;
        private double _measuredHeight;

        internal event EventHandler HeaderSizeChanged;

        public UIContainerView(View view)
        {
            _view = view;

            UpdatePlatformView();
            ClipsToBounds = true;
            MeasuredHeight = double.NaN;
            Margin = new Thickness(0);
        }

        internal void UpdatePlatformView()
        {
            _renderer = _view.ToHandler(_view.FindMauiContext());
            _platformView = _renderer.ContainerView ?? _renderer.PlatformView;

            if (_platformView.Superview != this)
                AddSubview(_platformView);
        }

        bool IsPlatformViewValid()
        {
            if (View == null || _platformView == null || _renderer == null)
                return false;

            return _platformView.Superview == this;
        }

        internal View View => _view;

        internal bool MatchHeight { get; set; }

        internal double MeasuredHeight
        {
            get
            {
                if (MatchHeight && Height != null)
                    return Height.Value;

                return _measuredHeight;
            }

            private set => _measuredHeight = value;
        }

        internal double? Height
        {
            get;
            set;
        }

        internal double? Width
        {
            get;
            set;
        }

        public virtual Thickness Margin
        {
            get;
        }

        private protected void OnHeaderSizeChanged()
        {
            HeaderSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        public override CGSize SizeThatFits(CGSize size)
        {
            var measuredSize = (_view as IView).Measure(size.Width, size.Height);

            if (Height != null && MatchHeight)
            {
                MeasuredHeight = Height.Value;
            }
            else
            {
                MeasuredHeight = measuredSize.Height;
            }

            return new CGSize(size.Width, MeasuredHeight);
        }

        public override void WillRemoveSubview(UIView uiview)
        {
            base.WillRemoveSubview(uiview);
        }

        public override void LayoutSubviews()
        {
            if (!IsPlatformViewValid())
                return;

            var height = Height ?? MeasuredHeight;
            var width = Width ?? Frame.Width;

            if (double.IsNaN(height))
                return;

            var platformFrame = new Rect(0, 0, width, height);


            if (MatchHeight)
            {
                (_view as IView).Measure(width, height);
            }

            (_view as IView).Arrange(platformFrame);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_platformView.Superview == this)
                    _platformView.RemoveFromSuperview();

                _renderer = null;
                _platformView = null;
                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
#endif
