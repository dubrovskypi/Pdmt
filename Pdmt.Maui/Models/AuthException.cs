using System.Net;

namespace Pdmt.Maui.Models;

public sealed class AuthException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
