using Compiler.Write;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Compiler.Tests;

public sealed class Wtf8CodecTests
{
    private static readonly string[] RoundTripValues =
    [
        "",
        "\0\u007f\u0080\u07ff\u0800\ufffd\uffff",
        "\ud800",
        "\udfff",
        "\ud800x\udc00",
        "\ud800\ud800",
        "\udc00\udc00",
        "😀",
        "before😀after",
        new string('a', 127),
        new string('b', 128),
    ];

    private static readonly (byte[] Bytes, string Message)[] MalformedValues =
    [
        (new byte[] { 0x80 }, "noncanonical"),
        (new byte[] { 0xc0, 0x80 }, "noncanonical"),
        (new byte[] { 0xe0, 0x80, 0x80 }, "noncanonical"),
        (new byte[] { 0xf0, 0x80, 0x80, 0x80 }, "noncanonical"),
        (new byte[] { 0xf4, 0x90, 0x80, 0x80 }, "noncanonical"),
        (new byte[] { 0xf5, 0x80, 0x80, 0x80 }, "noncanonical"),
        (new byte[] { 0xe2, 0x82 }, "truncated"),
        (new byte[] { 0xe2, 0x28, 0xa1 }, "continuation"),
        (
            new byte[] { 0xed, 0xa0, 0x80, 0xed, 0xb0, 0x80 },
            "surrogate pair noncanonically"),
    ];

    [Test]
    public void RoundTripsEveryManagedUtf16Shape()
    {
        foreach (var value in RoundTripValues)
        {
            var first = Wtf8Codec.Encode(value);
            var second = Wtf8Codec.Encode(value);

            Assert.Equal(first, second);
            Assert.Equal(value, Wtf8Codec.Decode(first));
        }
    }

    [Test]
    public void MatchesUtf8ForUnicodeScalarValues()
    {
        const string value = "\0\u007f\u0080\u07ff\u0800\ufffd😀";

        Assert.Equal(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetBytes(value),
            Wtf8Codec.Encode(value));
    }

    [Test]
    public void EncodesIsolatedSurrogatesWithoutReplacement()
    {
        Assert.Equal(
            new byte[] { 0xed, 0xa0, 0x80, 0x78, 0xed, 0xb0, 0x80 },
            Wtf8Codec.Encode("\ud800x\udc00"));
        Assert.Equal(
            "\ud800x\udc00",
            Wtf8Codec.Decode(new byte[] { 0xed, 0xa0, 0x80, 0x78, 0xed, 0xb0, 0x80 }));
    }

    [Test]
    public void RejectsMalformedAndNoncanonicalSequences()
    {
        foreach (var (bytes, message) in MalformedValues)
        {
            var exception = Assert.Throws<InvalidDataException>(() => Wtf8Codec.Decode(bytes));

            Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    public void PackageReadersRejectMalformedAndNoncanonicalStringEncodings()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp_Wtf8_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var packagePaths = new List<string>();
            var expectedMessages = new List<string>();
            foreach (var (bytes, expectedMessage) in MalformedValues)
            {
                var packagePath = Path.Combine(
                    directory,
                    $"invalid-{packagePaths.Count}.bin");
                File.WriteAllBytes(packagePath, CreateStringOnlyPackage(bytes));
                packagePaths.Add(packagePath);
                expectedMessages.Add(expectedMessage);

                var exception = Assert.Throws<InvalidDataException>(() =>
                    MsilBinaryPackage.ReadMethodBody(packagePath, "missing"));
                Assert.Contains(
                    expectedMessage,
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            }

            var scriptPath = Path.Combine(directory, "decode-invalid.mjs");
            File.WriteAllText(scriptPath,
                """
                import { readFile } from "node:fs/promises";
                import { pathToFileURL } from "node:url";
                const { decodeMsilPackage } = await import(pathToFileURL(process.argv[2]));
                const messages = [];
                for (const path of process.argv.slice(3)) {
                    try {
                        decodeMsilPackage(await readFile(path));
                        messages.push(null);
                    } catch (error) {
                        messages.push(error.message);
                    }
                }
                process.stdout.write(JSON.stringify(messages));
                """);
            var arguments = new[]
            {
                scriptPath,
                Path.Combine(AppContext.BaseDirectory, "JsRuntime", "msil-binary.js"),
            }.Concat(packagePaths).ToArray();
            var (output, error) = TestProcess.Run(
                "node",
                directory,
                TimeSpan.FromSeconds(10),
                arguments);
            Assert.True(string.IsNullOrWhiteSpace(error), error);
            var messages = JArray.Parse(output).Values<string?>().ToArray();
            Assert.Equal(expectedMessages.Count, messages.Length);
            for (var index = 0; index < messages.Length; index++)
            {
                Assert.NotNull(messages[index]);
                Assert.Contains(
                    expectedMessages[index],
                    messages[index]!,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateStringOnlyPackage(byte[] stringBytes)
    {
        Assert.InRange(stringBytes.Length, 0, 0x7f);
        return new byte[]
        {
            0x46, 0x45, 0x43, 0x53, 0x49, 0x4c, 0x0d, 0x0a,
            7,
            1,
            0,
            (byte)stringBytes.Length,
        }
        .Concat(stringBytes)
        .Concat(new byte[]
        {
            0,
            0,
            0,
        })
        .ToArray();
    }
}
