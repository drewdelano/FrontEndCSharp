using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.VisualStudio;

internal static class DapHarnessTrace
{
    internal const string EnvironmentVariableName = "FRONTENDCSHARP_DEBUGGER_HARNESS_TRACE";

    private static readonly ConcurrentQueue<string> Pending = new();
    private static readonly Regex SensitiveAssignment = new(
        @"(?ix)(?<prefix>(?:password|passwd|pwd|secret|client[_-]?secret|api[_-]?key|access[_-]?token|refresh[_-]?token|id[_-]?token|session[_-]?token|authorization|cookie)\s*[:=]\s*)(?<value>[^\s;&,]+)",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveHeader = new(
        @"(?im)(?<prefix>\b(?:authorization|proxy-authorization|cookie|set-cookie)\s*:\s*).+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveQuery = new(
        @"(?ix)(?<prefix>[?&](?:password|secret|client_secret|api_key|access_token|refresh_token|id_token|session_token|authorization|signature|sig|code)=)[^&#\s]+",
        RegexOptions.CultureInvariant);
    private static int flushScheduled;

    internal static void Write(
        Guid launchId,
        string launchRole,
        string direction,
        string payload)
    {
        var path = GetTracePath();
        if (path == null || !ShouldRecord(direction, payload))
        {
            return;
        }

        try
        {
            var record = new JObject
            {
                ["timestampUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["launchId"] = launchId.ToString("D", CultureInfo.InvariantCulture),
                ["launchRole"] = launchRole,
                ["direction"] = direction,
                ["payloadBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    SanitizePayload(payload)))
            };
            Pending.Enqueue(record.ToString(Formatting.None));
            if (Interlocked.Exchange(ref flushScheduled, 1) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Flush(path));
            }
        }
        catch (Exception exception) when (
            exception is JsonException
            || exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException)
        {
            BrowserCompatibilityTrace.Write(
                "harness-dap-trace-failed",
                exception.GetType().Name);
        }
    }

    private static string? GetTracePath()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        try
        {
            var path = Path.GetFullPath(configured);
            var allowedRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "FrontEndCSharp",
                "DebuggerIntegration"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return path.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetExtension(path), ".jsonl", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is IOException
            || exception is NotSupportedException
            || exception is System.Security.SecurityException)
        {
            BrowserCompatibilityTrace.Write(
                "harness-dap-trace-path-rejected",
                exception.GetType().Name);
            return null;
        }
    }

    private static bool ShouldRecord(string direction, string payload)
    {
        try
        {
            var message = JObject.Parse(payload);
            var type = message.Value<string>("type");
            var name = string.Equals(type, "event", StringComparison.Ordinal)
                ? message.Value<string>("event")
                : message.Value<string>("command");
            if (string.Equals(direction, "client-to-adapter", StringComparison.Ordinal))
            {
                return string.Equals(type, "request", StringComparison.Ordinal)
                    && IsObservedRequest(name);
            }
            if (!string.Equals(direction, "adapter-to-client", StringComparison.Ordinal))
            {
                return false;
            }
            return string.Equals(type, "response", StringComparison.Ordinal)
                ? IsObservedRequest(name)
                : string.Equals(type, "event", StringComparison.Ordinal)
                    && IsObservedEvent(name);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsObservedRequest(string? command) =>
        string.Equals(command, "configurationDone", StringComparison.Ordinal)
        || string.Equals(command, "setBreakpoints", StringComparison.Ordinal)
        || string.Equals(command, "stackTrace", StringComparison.Ordinal)
        || string.Equals(command, "scopes", StringComparison.Ordinal)
        || string.Equals(command, "variables", StringComparison.Ordinal)
        || string.Equals(command, "setVariable", StringComparison.Ordinal)
        || string.Equals(command, "evaluate", StringComparison.Ordinal)
        || string.Equals(command, "exceptionInfo", StringComparison.Ordinal);

    private static bool IsObservedEvent(string? eventName) =>
        string.Equals(eventName, "breakpoint", StringComparison.Ordinal)
        || string.Equals(eventName, "continued", StringComparison.Ordinal)
        || string.Equals(eventName, "exited", StringComparison.Ordinal)
        || string.Equals(eventName, "loadedSource", StringComparison.Ordinal)
        || string.Equals(eventName, "output", StringComparison.Ordinal)
        || string.Equals(eventName, "process", StringComparison.Ordinal)
        || string.Equals(eventName, "stopped", StringComparison.Ordinal)
        || string.Equals(eventName, "terminated", StringComparison.Ordinal)
        || string.Equals(eventName, "thread", StringComparison.Ordinal);

    private static string SanitizePayload(string payload)
    {
        var message = JObject.Parse(payload);
        SanitizeToken(message);
        if (string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message["arguments"] is JObject arguments
            && arguments.Property("expression") is { } expression)
        {
            expression.Value = "[REDACTED]";
        }
        if (string.Equals(message.Value<string>("command"), "setVariable", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && message["body"] is JObject body
            && body.Property("value") is { } setVariableValue)
        {
            // A setVariable response does not repeat the variable name, so it cannot
            // be correlated safely with the request by this per-message sanitizer.
            setVariableValue.Value = "[REDACTED]";
        }
        return message.ToString(Formatting.None);
    }

    private static void SanitizeToken(JToken token)
    {
        if (token is JObject obj)
        {
            var namedSensitiveValue = IsSensitiveName(obj.Value<string>("name"));
            foreach (var property in obj.Properties().ToArray())
            {
                if (IsSensitiveName(property.Name)
                    || (namedSensitiveValue
                        && (string.Equals(property.Name, "value", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(property.Name, "evaluateName", StringComparison.OrdinalIgnoreCase))))
                {
                    property.Value = "[REDACTED]";
                    continue;
                }
                SanitizeToken(property.Value);
            }
            return;
        }
        if (token is JArray array)
        {
            foreach (var child in array)
            {
                SanitizeToken(child);
            }
            return;
        }
        if (token is JValue { Type: JTokenType.String } value)
        {
            value.Value = SanitizeString(value.Value<string>() ?? string.Empty);
        }
    }

    private static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        var normalized = new string(name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized is "authorization"
            or "proxyauthorization"
            or "cookie"
            or "setcookie"
            or "password"
            or "passwd"
            or "pwd"
            or "secret"
            or "clientsecret"
            or "apikey"
            or "token"
            or "accesstoken"
            or "refreshtoken"
            or "idtoken"
            or "sessiontoken"
            or "connectionstring";
    }

    private static string SanitizeString(string value)
    {
        var sanitized = SensitiveHeader.Replace(
            value,
            match => match.Groups["prefix"].Value + "[REDACTED]");
        sanitized = SensitiveAssignment.Replace(
            sanitized,
            match => match.Groups["prefix"].Value + "[REDACTED]");
        sanitized = SensitiveQuery.Replace(
            sanitized,
            match => match.Groups["prefix"].Value + "[REDACTED]");
        foreach (var path in new[]
        {
            new PathReplacement(
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                "%TEMP%"),
            new PathReplacement(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "%LOCALAPPDATA%"),
            new PathReplacement(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "%APPDATA%"),
            new PathReplacement(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "%USERPROFILE%")
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value))
         .OrderByDescending(item => item.Value.Length))
        {
            sanitized = Regex.Replace(
                sanitized,
                Regex.Escape(path.Value).Replace(@"\\", @"[\\/]"),
                path.Replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return sanitized;
    }

    private static void Flush(string path)
    {
        try
        {
            var lines = new StringBuilder();
            while (Pending.TryDequeue(out var line))
            {
                lines.AppendLine(line);
            }
            if (lines.Length > 0)
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    path,
                    lines.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException)
        {
            BrowserCompatibilityTrace.Write(
                "harness-dap-trace-flush-failed",
                exception.GetType().Name);
        }
        finally
        {
            Interlocked.Exchange(ref flushScheduled, 0);
            if (!Pending.IsEmpty
                && Interlocked.Exchange(ref flushScheduled, 1) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Flush(path));
            }
        }
    }

    private sealed class PathReplacement
    {
        public PathReplacement(string value, string replacement)
        {
            Value = value;
            Replacement = replacement;
        }

        public string Value { get; }
        public string Replacement { get; }
    }
}
