﻿using Android.Graphics;
using AWebKit = Android.Webkit;

using Java.Interop;

using Microsoft.Maui.Handlers;

using Android.OS;

namespace Com.Bnotech.ExtendedWebView.Platforms.Android.Handlers;
    public class ExtWebViewHandler : ViewHandler<IExtWebView, AWebKit.WebView>
    {
        public static PropertyMapper<IExtWebView, ExtWebViewHandler> ExtWebViewMapper = new PropertyMapper<IExtWebView, ExtWebViewHandler>(ViewHandler.ViewMapper);

        const string JavascriptFunction = "function invokeCSharpAction(data){jsBridge.invokeAction(data);}";

        private JSBridge? _jsBridgeHandler;
        static SynchronizationContext? _sync;

        public ExtWebViewHandler() : base(ExtWebViewMapper)
        {
            _sync = SynchronizationContext.Current;
        }

        private void VirtualView_SourceChanged(object sender, SourceChangedEventArgs e)
        {
            LoadSource(e.Source, PlatformView);
        }

        protected override AWebKit.WebView CreatePlatformView()
        {
            _sync = _sync ?? SynchronizationContext.Current;

            var webView = new AWebKit.WebView(Context);
            _jsBridgeHandler = new JSBridge(this);

            webView.Settings.JavaScriptEnabled = true;

            webView.SetWebViewClient(new JavascriptWebViewClient($"javascript: {JavascriptFunction}", VirtualView));
            webView.AddJavascriptInterface(_jsBridgeHandler, "jsBridge");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                AWebKit.CookieManager.Instance?.SetAcceptThirdPartyCookies(webView, true);
            }
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
            {
#if DEBUG
                AWebKit.WebView.SetWebContentsDebuggingEnabled(true);
#else
                AWebKit.WebView.SetWebContentsDebuggingEnabled(false);
#endif
            }

            return webView;
        }

        protected override void ConnectHandler(AWebKit.WebView platformView)
        {
            base.ConnectHandler(platformView);

            if (VirtualView.Source != null)
            {
                LoadSource(VirtualView.Source, PlatformView);
            }

            VirtualView.SourceChanged += VirtualView_SourceChanged!;
            VirtualView.RequestEvaluateJavaScript += VirtualView_RequestEvaluateJavaScript!;
        }

        private void VirtualView_RequestEvaluateJavaScript(object sender, EvaluateJavaScriptAsyncRequest e)
        {
            _sync?.Post((o) => PlatformView.EvaluateJavascript(e.Script, null), null);
        }

        protected override void DisconnectHandler(AWebKit.WebView platformView)
        {
            base.DisconnectHandler(platformView);

            VirtualView.SourceChanged -= VirtualView_SourceChanged!;
            VirtualView.Cleanup();

            _jsBridgeHandler?.Dispose();
            _jsBridgeHandler = null;
        }

        private static void LoadSource(WebViewSource source, AWebKit.WebView control)
        {
            try
            {
                if (source is HtmlWebViewSource html)
                {
                    control.LoadDataWithBaseURL(html.BaseUrl, html.Html, null, "charset=UTF-8", null);
                }
                else if (source is UrlWebViewSource url)
                {
                    control.LoadUrl(url.Url);
                }
            }
            catch { }
        }
    }

    public class JavascriptWebViewClient : AWebKit.WebViewClient
    {
        private readonly string _javascript;
        private object _virtualView;
        
        public JavascriptWebViewClient(string javascript, object virtualView)
        {
            _javascript = javascript;
            _virtualView = virtualView;
        }

        public override void OnPageStarted(AWebKit.WebView? view, string? url, Bitmap? favicon)
        {
            base.OnPageStarted(view, url, favicon);
            view?.EvaluateJavascript(_javascript, null);
        }

        public override void DoUpdateVisitedHistory(AWebKit.WebView? view, string? url, bool isReload)
        {
            base.DoUpdateVisitedHistory(view, url, isReload);
            if (_virtualView is IExtWebView virtualView)
            {
                virtualView.SendUrlChanged(new UrlChangedEventArgs(url: url ?? ""));
            }
        }
    }

    public class JSBridge : Java.Lang.Object
    {
        readonly WeakReference<ExtWebViewHandler> _extWebViewRenderer;

        internal JSBridge(ExtWebViewHandler hybridRenderer)
        {
            _extWebViewRenderer = new WeakReference<ExtWebViewHandler>(hybridRenderer);
        }

        [AWebKit.JavascriptInterface]
        [Export("invokeAction")]
        public void InvokeAction(string data)
        {
            if (_extWebViewRenderer != null && _extWebViewRenderer.TryGetTarget(out var hybridRenderer))
            {
                hybridRenderer.VirtualView.InvokeAction(data);
            }
        }
    }
