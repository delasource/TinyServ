using System;
using System.Text.Json;

namespace TinyServ
{
    public record VoidResponse() : TinyResponse("", null);

    public record JsonResponse(object Obj) : TinyResponse(JsonSerializer.Serialize(Obj), null);

    public record TinyResponse(string    ResponseContent,
                               Exception Exception);
}
