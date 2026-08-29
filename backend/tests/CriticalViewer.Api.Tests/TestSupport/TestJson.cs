using System.Text.Json;

namespace CriticalViewer.Api.Tests.TestSupport;

// The API serializes responses camelCase (ASP.NET Core's AddControllers
// default). HttpContent.ReadFromJsonAsync<T>() without explicit options
// does NOT enable case-insensitive property matching, so deserializing
// into our PascalCase C# records needs this passed explicitly.
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
