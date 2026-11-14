using System;
using System.IO;

namespace Compiler.Write
{
    internal static class Wtf8Codec
    {
        public static byte[] Encode(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var byteCount = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var codeUnit = value[index];
                byteCount = checked(byteCount + GetEncodedLength(codeUnit));
                if (char.IsHighSurrogate(codeUnit)
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1]))
                {
                    byteCount = checked(byteCount + 1);
                    index++;
                }
            }

            var result = new byte[byteCount];
            var offset = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var codePoint = (int)value[index];
                if (char.IsHighSurrogate((char)codePoint)
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1]))
                {
                    codePoint = char.ConvertToUtf32((char)codePoint, value[++index]);
                }
                WriteCodePoint(result, ref offset, codePoint);
            }
            return result;
        }

        public static string Decode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            var characters = new char[bytes.Length];
            var characterCount = 0;
            var offset = 0;
            var previousWasEncodedHighSurrogate = false;
            while (offset < bytes.Length)
            {
                var first = bytes[offset++];
                int codePoint;
                if (first <= 0x7f)
                {
                    codePoint = first;
                }
                else if (first is >= 0xc2 and <= 0xdf)
                {
                    var second = ReadContinuation(bytes, ref offset);
                    codePoint = ((first & 0x1f) << 6) | (second & 0x3f);
                }
                else if (first is >= 0xe0 and <= 0xef)
                {
                    var second = ReadContinuation(bytes, ref offset);
                    var third = ReadContinuation(bytes, ref offset);
                    if (first == 0xe0 && second < 0xa0)
                    {
                        throw NonCanonicalSequence();
                    }
                    codePoint = ((first & 0x0f) << 12)
                        | ((second & 0x3f) << 6)
                        | (third & 0x3f);
                }
                else if (first is >= 0xf0 and <= 0xf4)
                {
                    var second = ReadContinuation(bytes, ref offset);
                    var third = ReadContinuation(bytes, ref offset);
                    var fourth = ReadContinuation(bytes, ref offset);
                    if (first == 0xf0 && second < 0x90
                        || first == 0xf4 && second > 0x8f)
                    {
                        throw NonCanonicalSequence();
                    }
                    codePoint = ((first & 0x07) << 18)
                        | ((second & 0x3f) << 12)
                        | ((third & 0x3f) << 6)
                        | (fourth & 0x3f);
                }
                else
                {
                    throw NonCanonicalSequence();
                }

                if (previousWasEncodedHighSurrogate
                    && codePoint is >= 0xdc00 and <= 0xdfff)
                {
                    throw new InvalidDataException(
                        "WTF-8 encodes a surrogate pair noncanonically as two surrogate code points.");
                }

                if (codePoint <= 0xffff)
                {
                    characters[characterCount++] = (char)codePoint;
                    previousWasEncodedHighSurrogate =
                        codePoint is >= 0xd800 and <= 0xdbff;
                    continue;
                }

                codePoint -= 0x10000;
                characters[characterCount++] = (char)(0xd800 + (codePoint >> 10));
                characters[characterCount++] = (char)(0xdc00 + (codePoint & 0x3ff));
                previousWasEncodedHighSurrogate = false;
            }
            return new string(characters, 0, characterCount);
        }

        private static int GetEncodedLength(char codeUnit)
        {
            if (codeUnit <= 0x7f)
            {
                return 1;
            }
            if (codeUnit <= 0x7ff)
            {
                return 2;
            }
            return 3;
        }

        private static void WriteCodePoint(Span<byte> target, ref int offset, int codePoint)
        {
            if (codePoint <= 0x7f)
            {
                target[offset++] = (byte)codePoint;
            }
            else if (codePoint <= 0x7ff)
            {
                target[offset++] = (byte)(0xc0 | (codePoint >> 6));
                target[offset++] = (byte)(0x80 | (codePoint & 0x3f));
            }
            else if (codePoint <= 0xffff)
            {
                target[offset++] = (byte)(0xe0 | (codePoint >> 12));
                target[offset++] = (byte)(0x80 | ((codePoint >> 6) & 0x3f));
                target[offset++] = (byte)(0x80 | (codePoint & 0x3f));
            }
            else
            {
                target[offset++] = (byte)(0xf0 | (codePoint >> 18));
                target[offset++] = (byte)(0x80 | ((codePoint >> 12) & 0x3f));
                target[offset++] = (byte)(0x80 | ((codePoint >> 6) & 0x3f));
                target[offset++] = (byte)(0x80 | (codePoint & 0x3f));
            }
        }

        private static byte ReadContinuation(ReadOnlySpan<byte> bytes, ref int offset)
        {
            if (offset >= bytes.Length)
            {
                throw new InvalidDataException("WTF-8 sequence is truncated.");
            }
            var value = bytes[offset++];
            if ((value & 0xc0) != 0x80)
            {
                throw new InvalidDataException(
                    $"WTF-8 sequence contains invalid continuation byte 0x{value:X2}.");
            }
            return value;
        }

        private static InvalidDataException NonCanonicalSequence() =>
            new("WTF-8 sequence is malformed, overlong, or noncanonical.");
    }
}
