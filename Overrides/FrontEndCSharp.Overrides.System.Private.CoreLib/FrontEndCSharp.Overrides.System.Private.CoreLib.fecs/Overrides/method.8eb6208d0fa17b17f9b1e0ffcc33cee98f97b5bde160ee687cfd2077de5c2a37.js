import { wrap, unwrap } from '../msil-type-helpers.js';
import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods["System.String.get_Chars(System.Int32)"] = function (ctx, strRaw, indexRaw) {
    const str = unwrap(strRaw);
    const index = unwrap(indexRaw);
    if (!Number.isInteger(index) || index < 0 || index >= str.length) {
        throwManagedException(ctx, 'System.IndexOutOfRangeException');
    }

    // CLI Char values are UTF-16 code units represented as integers on the
    // evaluation stack. JavaScript indexing would return a one-code-unit string.
    return wrap(str.charCodeAt(index), 'System.Char');
};
