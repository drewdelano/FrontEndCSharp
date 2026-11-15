import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.String.Trim()"] = function (ctx, thisArg) {
    const s = unwrap(thisArg);
    let start = 0;
    let end = s.length - 1;

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
            charCode === 0x0085   // Next Line
        );
    }

    while (start <= end && isWhiteSpace(s.charCodeAt(start))) {
        start++;
    }

    while (end >= start && isWhiteSpace(s.charCodeAt(end))) {
        end--;
    }

    return {
        t: 'System.String',
        v: s.substring(start, end + 1)
    };
};
