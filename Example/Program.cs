using System;
using System.Net.Http;
using TinyServ;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var tinyServer = new TinyServer(8444);
            tinyServer.Serve("/", HttpMethod.Get, request => Console.WriteLine("This does not give content"));
            tinyServer.Serve("/1", HttpMethod.Get, request => "this is plain content");
            tinyServer.Serve("/2", HttpMethod.Get, request => new { Message = "this is json content" });
            tinyServer.Serve("/3", HttpMethod.Get, request => request.Query);
            tinyServer.ServeFolder("/dir", "C:\\temp");

            Console.WriteLine("Press enter to stop");
            Console.ReadLine();
            tinyServer.Stop();
        }
    }
}
