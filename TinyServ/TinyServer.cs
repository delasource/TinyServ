using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TinyServ
{
    public class TinyServer
    {
        private static readonly IDictionary<string, string> MimeTypeMappings =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                #region extension to MIME type list

                { ".asf", "video/x-ms-asf" },
                { ".asx", "video/x-ms-asf" },
                { ".avi", "video/x-msvideo" },
                { ".bin", "application/octet-stream" },
                { ".cco", "application/x-cocoa" },
                { ".crt", "application/x-x509-ca-cert" },
                { ".css", "text/css" },
                { ".deb", "application/octet-stream" },
                { ".der", "application/x-x509-ca-cert" },
                { ".dll", "application/octet-stream" },
                { ".dmg", "application/octet-stream" },
                { ".ear", "application/java-archive" },
                { ".eot", "application/octet-stream" },
                { ".exe", "application/octet-stream" },
                { ".flv", "video/x-flv" },
                { ".gif", "image/gif" },
                { ".hqx", "application/mac-binhex40" },
                { ".htc", "text/x-component" },
                { ".htm", "text/html" },
                { ".html", "text/html" },
                { ".ico", "image/x-icon" },
                { ".img", "application/octet-stream" },
                { ".iso", "application/octet-stream" },
                { ".jar", "application/java-archive" },
                { ".jardiff", "application/x-java-archive-diff" },
                { ".jng", "image/x-jng" },
                { ".jnlp", "application/x-java-jnlp-file" },
                { ".jpeg", "image/jpeg" },
                { ".jpg", "image/jpeg" },
                { ".js", "application/x-javascript" },
                { ".mml", "text/mathml" },
                { ".mng", "video/x-mng" },
                { ".mov", "video/quicktime" },
                { ".mp3", "audio/mpeg" },
                { ".mpeg", "video/mpeg" },
                { ".mpg", "video/mpeg" },
                { ".msi", "application/octet-stream" },
                { ".msm", "application/octet-stream" },
                { ".msp", "application/octet-stream" },
                { ".pdb", "application/x-pilot" },
                { ".pdf", "application/pdf" },
                { ".pem", "application/x-x509-ca-cert" },
                { ".pl", "application/x-perl" },
                { ".pm", "application/x-perl" },
                { ".png", "image/png" },
                { ".prc", "application/x-pilot" },
                { ".ra", "audio/x-realaudio" },
                { ".rar", "application/x-rar-compressed" },
                { ".rpm", "application/x-redhat-package-manager" },
                { ".rss", "text/xml" },
                { ".run", "application/x-makeself" },
                { ".sea", "application/x-sea" },
                { ".shtml", "text/html" },
                { ".sit", "application/x-stuffit" },
                { ".swf", "application/x-shockwave-flash" },
                { ".tcl", "application/x-tcl" },
                { ".tk", "application/x-tcl" },
                { ".txt", "text/plain" },
                { ".war", "application/java-archive" },
                { ".wbmp", "image/vnd.wap.wbmp" },
                { ".wmv", "video/x-ms-wmv" },
                { ".xml", "text/xml" },
                { ".xpi", "application/x-xpinstall" },
                { ".zip", "application/zip" },

                #endregion
            };

        private readonly string[] _indexFiles =
        {
            "index.html",
            "index.htm",
            "default.html",
            "default.htm"
        };

        private record FuncHandler(string                          Url,
                                   HttpMethod                      Method,
                                   Func<TinyRequest, TinyResponse> Handler);


        private readonly HttpListener               _listener;
        private readonly CancellationTokenSource    _cancellationTokenSource;
        private readonly Dictionary<string, string> _serveFolders = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly List<FuncHandler>          _serveHandler = new();

        private Thread _serverThread;

        public string Host { get; private set; }
        public int    Port { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="bindToAnyHost"></param>
        public TinyServer(int port = 0, bool bindToAnyHost = false)
        {
            _listener                = new HttpListener();
            _cancellationTokenSource = new CancellationTokenSource();

            if (port == 0)
            {
                // get a free port
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                Console.WriteLine("Note: We have automatically choosen a free TCP port. " +
                                  "This may give you a different port every time.");
            }

            if (bindToAnyHost)
            {
                Console.WriteLine("Note: We bind to every available interface. On Windows you have to run the " +
                                  "application as admin, or define a static port and allow access " +
                                  "via: (run as admin)");
                Console.WriteLine($"  `netsh http add urlacl url=http://+:{port}/ user=DOMAIN\\user`");
                Console.WriteLine("");
                Host = "*";
            }
            else
                Host = "localhost";

            Initialize(port);
        }

        /// <summary>
        /// Stop server and dispose all functions.
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();
        }

        private void Initialize(int port)
        {
            Port          = port;
            _serverThread = new Thread(Listen);
            _serverThread.Start();
        }

        private void Listen()
        {
            try
            {
                _listener.Prefixes.Add($"http://{Host}:{Port}/");
                _listener.Start();
                Console.WriteLine($"TinyServ listening to http://{Host}:{Port}/");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = _listener.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {
                    // ignored
                }
                finally
                {
                    context?.Response.OutputStream.Close();
                }
            }
        }

        private void Process(HttpListenerContext context)
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            string url = context.Request.Url?.AbsolutePath;
            Console.WriteLine($"{context.Request.HttpMethod}: {url}");

            for (int index = _serveHandler.Count - 1; index >= 0; index--)
            {
                (string urlPart, var httpMethod, var handler) = _serveHandler[index];

                if (url?.Equals(urlPart) == false) continue;
                if (!string.Equals(context.Request.HttpMethod, httpMethod.Method,
                    StringComparison.InvariantCultureIgnoreCase)) continue;

                var response = handler(new TinyRequest(url, context.Request.QueryString,
                    context.Request.RemoteEndPoint?.Address.ToString()));

                byte[] data;

                if (response.Exception != null)
                {
                    Console.WriteLine("500 EXCEPTION");
                    Console.WriteLine(response.Exception);
                    data                        = Encoding.UTF8.GetBytes(response.Exception.ToString());
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.OutputStream.Write(data);
                    context.Response.OutputStream.Flush();
                    return;
                }

                Console.WriteLine("200 " + urlPart);
                data                        = Encoding.UTF8.GetBytes(response.ResponseContent);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Write(data);
                context.Response.OutputStream.Flush();
                return;
            }

            foreach ((string urlPart, string localFolder) in _serveFolders)
            {
                if (url?.StartsWith(urlPart) == false) continue;

                string filename = url?.Replace(urlPart, "");

                // Deliver index.html ?
                if (string.IsNullOrWhiteSpace(filename))
                {
                    foreach (string indexFile in _indexFiles)
                    {
                        if (!File.Exists(Path.Combine(localFolder, indexFile))) continue;
                        filename = indexFile;
                        break;
                    }
                }

                // Deliver file
                if (string.IsNullOrWhiteSpace(filename) || !File.Exists(Path.Combine(localFolder, filename.Trim('/'))))
                {
                    Console.WriteLine("404");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                try
                {
                    filename = Path.Combine(localFolder, filename.Trim('/'));
                    Stream input = new FileStream(filename, FileMode.Open);

                    //Adding permanent http response headers
                    context.Response.ContentType =
                        MimeTypeMappings.TryGetValue(Path.GetExtension(filename), out string mime)
                            ? mime
                            : "application/octet-stream";
                    context.Response.ContentLength64 = input.Length;
                    context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                    context.Response.AddHeader("Last-Modified",
                        File.GetLastWriteTime(filename).ToString("r"));

                    byte[] buffer = new byte[1024 * 16];
                    int    nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        context.Response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();

                    Console.WriteLine("200");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Flush();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("500 EXCEPTION");
                    Console.WriteLine(ex);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return;
                }
            }

            Console.WriteLine("400 Url unknown");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        public void Serve(string url, HttpMethod method, Action<TinyRequest> handler)
        {
            TinyResponse ServeHandlerWrapper(TinyRequest request)
            {
                handler(request);
                return new VoidResponse();
            }

            Serve(url, method, ServeHandlerWrapper);
        }

        public void Serve(string url, HttpMethod method, Func<TinyRequest, string> handler)
        {
            TinyResponse ServeHandlerWrapper(TinyRequest request) =>
                new(handler(request), null);

            Serve(url, method, ServeHandlerWrapper);
        }

        public void Serve(string url, HttpMethod method, Func<TinyRequest, object> handler)
        {
            TinyResponse ServeHandlerWrapper(TinyRequest request) =>
                new JsonResponse(handler(request));

            Serve(url, method, ServeHandlerWrapper);
        }

        public void Serve(string url, HttpMethod method, Func<TinyRequest, TinyResponse> handler)
        {
            if (url.Contains("{"))
                throw new ArgumentException(
                    "URL Parameters are not supported. Please use query parameters. --> ?a=1&b=2");

            if (url.Contains("?"))
                throw new ArgumentException(
                    "Do not register URLs with query parameters. Just use them in your client.");

            if (_serveHandler.Any(s => s.Method == method && s.Url.StartsWith(url)))
                throw new ArgumentException($"An endpoint with the method and url '{url}' does already exist. " +
                                            "For cascading URLs, register the more generic one first.");

            if (_serveFolders.Any(s => s.Key.StartsWith(url)))
                throw new ArgumentException($"An endpoint with the method and url '{url}' does already exist. " +
                                            "For cascading URLs, register the more generic one first.");

            if (!url.StartsWith('/')) url = "/" + url;
            _serveHandler.Add(new FuncHandler(url, method, handler));
            Console.WriteLine($"New handler registered: {method.Method.ToUpperInvariant()} http://{Host}:{Port}{url}");
        }

        public void ServeFolder(string url, string localFolder)
        {
            if (url.Contains("{"))
                throw new ArgumentException(
                    "URL Parameters are not supported. Please use query parameters. --> ?a=1&b=2");

            if (url.Contains("?"))
                throw new ArgumentException(
                    "Do not register URLs with query parameters. Just use them in your client.");

            if (_serveHandler.Any(s => s.Method == HttpMethod.Get && s.Url.StartsWith(url)))
                throw new ArgumentException($"An endpoint with the method and url '{url}' does already exist. " +
                                            "For cascading URLs, register the more generic one first.");

            if (_serveFolders.Any(s => s.Key.StartsWith(url)))
                throw new ArgumentException($"An endpoint with the method and url '{url}' does already exist. " +
                                            "For cascading URLs, register the more generic one first.");

            if (!url.StartsWith('/')) url = "/" + url;
            _serveFolders.Add(url, localFolder);
            Console.WriteLine($"New handler registered: GET http://{Host}:{Port}{url}");
        }
    }
}
