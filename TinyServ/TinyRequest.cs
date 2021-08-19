using System.Collections.Generic;
using System.Collections.Specialized;

namespace TinyServ
{
    public record TinyRequest(string              Url,
                              NameValueCollection Query,
                              string              RemoteIp);
}
