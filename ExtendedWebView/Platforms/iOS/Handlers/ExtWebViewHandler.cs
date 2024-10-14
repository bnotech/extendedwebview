using CoreGraphics;

using Foundation;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

using WebKit;

using UIKit;
using ObjCRuntime;

namespace Com.Bnotech.ExtendedWebView.Platforms.iOS.Handlers;
    public class ExtWebViewHandler : ViewHandler<IExtWebView, WKWebView>
    {
        public static PropertyMapper<IExtWebView, ExtWebViewHandler> ExtWebViewMapper = new PropertyMapper<IExtWebView, ExtWebViewHandler>(ViewHandler.ViewMapper);

        const string JavaScriptFunction = "function invokeCSharpAction(data){window.webkit.messageHandlers.invokeAction.postMessage(data);}";

        private WKUserContentController? _userController;
        private JSBridge? _jsBridgeHandler;
        static SynchronizationContext? _sync;
        private WebViewSource? _currentSource;
        private IDisposable? _observer;
        
        public ExtWebViewHandler() : base(ExtWebViewMapper)
        {
            _sync = SynchronizationContext.Current;
        }

        private void VirtualView_SourceChanged(object sender, SourceChangedEventArgs e)
        {
            var source = e.Source;
            if ((source is HtmlWebViewSource htmla) && (_currentSource is HtmlWebViewSource htmlb))
            {
                if (htmla.Html.Equals(htmlb.Html))
                    return;
            }
            else if ((source is UrlWebViewSource urla) && (_currentSource is UrlWebViewSource urlb))
            {
                if (urla.Url.Equals(urlb.Url))
                    return;
            }

            _currentSource = e.Source;
            LoadSource(e.Source, PlatformView);
        }

        protected override WKWebView CreatePlatformView()
        {
            _sync = _sync ?? SynchronizationContext.Current;
            _jsBridgeHandler = new JSBridge(this);
            _userController = new WKUserContentController();

            var script = new WKUserScript(new NSString(JavaScriptFunction), WKUserScriptInjectionTime.AtDocumentEnd, false);

            _userController.AddUserScript(script);
            _userController.AddScriptMessageHandler(_jsBridgeHandler, "invokeAction");
            
            WKWebViewConfiguration config;
            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            {
                config = new WKWebViewConfiguration { LimitsNavigationsToAppBoundDomains = true, UserContentController = _userController };
                if (config is { DefaultWebpagePreferences: not null })
                    config.DefaultWebpagePreferences.AllowsContentJavaScript = true;
            }
            else
            {
                var prefs = new WKPreferences();
                prefs.JavaScriptEnabled = true;
                config = new WKWebViewConfiguration { UserContentController = _userController, Preferences = prefs };
            }
            config.WebsiteDataStore = WKWebsiteDataStore.DefaultDataStore;
            var webView = new WKWebView(CGRect.Empty, config);

            if (NSHttpCookieStorage.SharedStorage.AcceptPolicy != NSHttpCookieAcceptPolicy.Always)
                NSHttpCookieStorage.SharedStorage.AcceptPolicy = NSHttpCookieAcceptPolicy.Always;

            return webView;
        }

        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);
            PlatformView.NavigationDelegate = new NavigationDelegate(VirtualView);
            _observer = PlatformView.AddObserver(new Foundation.NSString("URL"), Foundation.NSKeyValueObservingOptions.OldNew, (o) =>
            {
                switch (o.NewValue)
                {
                    case NSUrl url:
                        VirtualView.SendUrlChanged(new UrlChangedEventArgs(url: url.ToString()));
                        break;
                    case NSString urlStr:
                        VirtualView.SendUrlChanged(new UrlChangedEventArgs(url: urlStr));
                        break;
                }
            });
            
            if (VirtualView.Source != null)
            {
                _currentSource = VirtualView.Source;
                LoadSource(VirtualView.Source, PlatformView);
            }

            VirtualView.SourceChanged += VirtualView_SourceChanged!;
            VirtualView.RequestEvaluateJavaScript += VirtualView_RequestEvaluateJavaScript!;
        }

        private void VirtualView_RequestEvaluateJavaScript(object sender, EvaluateJavaScriptAsyncRequest e)
        {
            if (_sync == null) return;
            _sync.Post((o) =>
            {
                PlatformView.EvaluateJavaScript(e);
            }, null);
        }

        protected override void DisconnectHandler(WKWebView platformView)
        {
            base.DisconnectHandler(platformView);

            VirtualView.SourceChanged -= VirtualView_SourceChanged!;
            _observer?.Dispose();
            
            _userController?.RemoveAllUserScripts();
            _userController?.RemoveScriptMessageHandler("invokeAction");

            _jsBridgeHandler?.Dispose();
            _jsBridgeHandler = null;
        }


        private static void LoadSource(WebViewSource source, WKWebView control)
        {
            if (source is HtmlWebViewSource html)
            {
                System.Diagnostics.Debug.WriteLine("ExtWebViewHandler: Load HTML");
                control.LoadHtmlString(html.Html, new NSUrl(html.BaseUrl ?? "http://localhost", true));
            }
            else if (source is UrlWebViewSource url)
            {
                System.Diagnostics.Debug.WriteLine($"ExtWebViewHandler: Load Url {url.Url}");
                control.LoadRequest(new NSUrlRequest(new NSUrl(url.Url)));
            }

        }

    }

    public class JSBridge : NSObject, IWKScriptMessageHandler
    {
        readonly WeakReference<ExtWebViewHandler> _extWebViewRenderer;

        internal JSBridge(ExtWebViewHandler hybridRenderer)
        {
            _extWebViewRenderer = new WeakReference<ExtWebViewHandler>(hybridRenderer);
        }

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            if (_extWebViewRenderer.TryGetTarget(out var hybridRenderer))
            {
                hybridRenderer.VirtualView?.InvokeAction(message.Body.ToString());
            }
        }
    }

    public class NavigationDelegate : WKNavigationDelegate
    {
        NSMutableArray multiCookieArr = new NSMutableArray();
        IExtWebView VirtualView;

        bool runTimer = true;

        public NavigationDelegate(IExtWebView virtualView)
            : base()
        {
            VirtualView = virtualView;
        }

        public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            System.Diagnostics.Debug.WriteLine("DidFinishNavigation: " + webView?.Url?.ToString());
            VirtualView.SendWebViewNavigated(new WebNavigatingEventArgs(WebNavigationEvent.NewPage, VirtualView.Source,
                webView?.Url?.ToString()));
            VirtualView.SendNavigated(new WebNavigatedEventArgs(WebNavigationEvent.NewPage, VirtualView.Source,
                webView?.Url?.ToString(), WebNavigationResult.Success));

            Dispatcher.GetForCurrentThread()?.StartTimer(TimeSpan.FromMilliseconds(500), () =>
            {
                webView?.EvaluateJavaScript("1+1;", null!);
                return runTimer;
            });
        }

        public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
        {
            VirtualView.SendNavigating(new WebNavigatingEventArgs(WebNavigationEvent.NewPage, VirtualView.Source,
                webView?.Url?.ToString()));
        }

        public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            System.Diagnostics.Debug.WriteLine(
                $"{error.Code} {error.Domain} {error.HelpAnchor} {error.LocalizedFailureReason}");
        }

        [Foundation.Export("webView:decidePolicyForNavigationResponse:decisionHandler:")]
        public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse,
            [BlockProxy(typeof(Action))] Action<WKNavigationResponsePolicy> decisionHandler)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(12, 0))
            {
                Uri url = navigationResponse.Response.Url!;
                var wKHttpCookieStore = webView.Configuration.WebsiteDataStore.HttpCookieStore;
                System.Diagnostics.Debug.WriteLine("wKHttpCookieStore is :" + wKHttpCookieStore.GetDebugDescription());
                wKHttpCookieStore.GetAllCookies(cookies =>
                {
                    if (cookies.Length > 0)
                    {
                        foreach (NSHttpCookie cookie in cookies)
                        {
                            NSHttpCookieStorage.SharedStorage.SetCookie(cookie);
                        }
                    }
                });
            }
            else
            {
                if (navigationResponse.Response is not NSHttpUrlResponse response) return;
                var cookiesAll =
                    NSHttpCookie.CookiesWithResponseHeaderFields(response.AllHeaderFields, response.Url);
                foreach (var cookie in cookiesAll)
                {
                    var cookieArr = NSArray.FromObjects(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
                    multiCookieArr.Add(cookieArr);
                }

                System.Diagnostics.Debug.WriteLine("cookie is :" + cookiesAll);
            }

            decisionHandler(WKNavigationResponsePolicy.Allow);
        }
    }

