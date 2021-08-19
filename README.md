# TinyServ

TinyServer is a small simple threaded solution to make dotnet functions available to the web.

You just define an endpoint url and a handler. See the example project for a working console application.

## Features:

* Return plain text
* Return an object (automatically serialized as json w/System.Text.Json)
* Serve a local directory
* Automatic free port choosing

## Usage:

```
var ts = new TinyServer(8080);
ts.Serve("/empty", HttpMethod.Get, request => Console.WriteLine("This does not give content"));
ts.Serve("/json", HttpMethod.Get, request => new { Message = "this is json content" });
```

## Troubleshooting

When binding to any available host (set with constructor parameter) it is required to run either as administrator or permit your application  and port like:
    `netsh http add urlacl url=http://+:8080/ user=DOMAIN\\user`

