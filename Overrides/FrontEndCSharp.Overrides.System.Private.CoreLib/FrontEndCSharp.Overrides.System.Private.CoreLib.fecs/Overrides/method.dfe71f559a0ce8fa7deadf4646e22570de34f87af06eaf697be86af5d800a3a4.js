import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Char.IsWhiteSpace(System.Char)"] = function (ctx, c) {
    const value = unwrap(c);
    const charCode = typeof value === 'string' ? value.charCodeAt(0) : value;
    
    // Based on https://learn.microsoft.com/en-us/dotnet/api/system.char.iswhitespace
    function isWhiteSpace(charCode) {
        return (
            // SpaceSeparator category
            charCode === 0x0020 || // Space
            charCode === 0x00A0 || // No-Break Space
            charCode === 0x1680 || // Ogham Space Mark
            (charCode >= 0x2000 && charCode <= 0x200A) || // En Quad through Hair Space
            charCode === 0x202F || // Narrow No-Break Space
            charCode === 0x205F || // Medium Mathematical Space
            charCode === 0x3000 || // Ideographic Space

            // LineSeparator category
            charCode === 0x2028 || // Line Separator

            // ParagraphSeparator category
            charCode === 0x2029 || // Paragraph Separator

            // Character 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x0085
            charCode === 0x0009 || // Character Tabulation
            charCode === 0x000A || // Line Feed
            charCode === 0x000B || // Line Tabulation
            charCode === 0x000C || // Form Feed
            charCode === 0x000D || // Carriage Return
            charCode === 0x0085);  // Next Line
    }

    return {
        t: 'System.Boolean',
        v: isWhiteSpace(charCode)
    };
};
