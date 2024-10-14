namespace Com.Bnotech.ExtendedWebView;

public class SourceChangedEventArgs(WebViewSource source) : EventArgs
{
    public WebViewSource Source
    {
        get;
        private set;
    } = source;
}

public class UrlChangedEventArgs(string url) : EventArgs
{
    public string Url
    {
        get;
        private set;
    } = url;
}

public class JavaScriptActionEventArgs : EventArgs
{
    public string Payload { get; private set; }

    public JavaScriptActionEventArgs(string payload)
    {
        Payload = payload;
    }
}

public interface IExtWebView : IView
{
    event EventHandler<SourceChangedEventArgs>? SourceChanged;
    event EventHandler<JavaScriptActionEventArgs>? JavaScriptAction;
    event EventHandler<EvaluateJavaScriptAsyncRequest>? RequestEvaluateJavaScript;
    event EventHandler<WebNavigationEventArgs>? WebViewNavigated;
    event EventHandler<WebNavigatingEventArgs>? Navigating;
    event EventHandler<WebNavigatedEventArgs>? Navigated;
    event EventHandler<UrlChangedEventArgs>? UrlChanged;
    
    void Refresh();

    Task EvaluateJavaScriptAsync(EvaluateJavaScriptAsyncRequest request);

    WebViewSource Source { get; set; }

    void Cleanup();

    void InvokeAction(string data);

    void SendWebViewNavigated(WebNavigatingEventArgs e);
    void SendNavigated(WebNavigatedEventArgs e);
    void SendNavigating(WebNavigatingEventArgs e);
    void SendUrlChanged(UrlChangedEventArgs e);
}


public class ExtWebView : View, IExtWebView
{
    public event EventHandler<SourceChangedEventArgs>? SourceChanged;
    public event EventHandler<JavaScriptActionEventArgs>? JavaScriptAction;
    public event EventHandler<EvaluateJavaScriptAsyncRequest>? RequestEvaluateJavaScript;
    public event EventHandler<WebNavigationEventArgs>? WebViewNavigated;
    public event EventHandler<WebNavigatingEventArgs>? Navigating;
    public event EventHandler<WebNavigatedEventArgs>? Navigated;
    public event EventHandler<UrlChangedEventArgs>? UrlChanged;
    
    public async Task EvaluateJavaScriptAsync(EvaluateJavaScriptAsyncRequest request)
    {
        await Task.Run(() =>
        {
            RequestEvaluateJavaScript?.Invoke(this, request);
        });
    }

    public void Refresh()
    {
        if (Source == null) return;
        var s = Source;
        Source = null!;
        Source = s;
    }

    public WebViewSource Source
    {
        get { return (WebViewSource)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
      propertyName: "Source",
      returnType: typeof(WebViewSource),
      declaringType: typeof(ExtWebView),
      defaultValue: new UrlWebViewSource() { Url = "about:blank" },
      propertyChanged: OnSourceChanged);

    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = bindable as ExtWebView;

        bindable.Dispatcher.Dispatch(() =>
        {
            view?.SourceChanged?.Invoke(view, new SourceChangedEventArgs((newValue as WebViewSource)!));
        });
    }

    public void Cleanup()
    {
        JavaScriptAction = null;
    }

    public void InvokeAction(string data)
    {
        JavaScriptAction?.Invoke(this, new JavaScriptActionEventArgs(data));
    }

    public void SendWebViewNavigated(WebNavigatingEventArgs e)
    {
        WebViewNavigated?.Invoke(this, e);
    }

    public void SendNavigated(WebNavigatedEventArgs e)
    {
        Navigated?.Invoke(this, e);
    }

    public void SendNavigating(WebNavigatingEventArgs e)
    {
        Navigating?.Invoke(this, e);
    }

    public void SendUrlChanged(UrlChangedEventArgs e)
    {
        UrlChanged?.Invoke(this, e);
    }
}

