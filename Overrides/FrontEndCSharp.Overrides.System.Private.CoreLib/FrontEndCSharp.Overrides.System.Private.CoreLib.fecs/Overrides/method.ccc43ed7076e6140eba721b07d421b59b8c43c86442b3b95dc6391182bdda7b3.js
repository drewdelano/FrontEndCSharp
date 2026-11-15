import { formatManagedTypeReference, wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Type.ToString()"] = function (ctx, thisArg) {
    return wrap(formatManagedTypeReference({
        typeName: unwrap(thisArg),
        genericParams: thisArg.$typeArgs
    }), 'System.String');
};
