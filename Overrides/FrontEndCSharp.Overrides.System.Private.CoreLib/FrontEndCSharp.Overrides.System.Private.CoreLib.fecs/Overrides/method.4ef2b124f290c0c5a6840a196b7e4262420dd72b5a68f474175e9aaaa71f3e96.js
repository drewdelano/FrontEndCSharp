import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"] = function (ctx, handle) {
    const type = unwrap(handle);
    const typeName = type.typeName;
    const genericTypeArguments = type.genericTypeArguments;
    return wrap(typeName, 'System.Type', genericTypeArguments);
};

/*

OLD:

const h = unwrap(handle);
// Extract the full type name from the RuntimeTypeHandle
// Handle structure is { tokenType: 'Type', tokenValue: 'Fully.Qualified.TypeName`1', ... }
const typeName =
    (h && typeof h === 'object' && 'tokenValue' in h) ? h.tokenValue  // From ldtoken
        : (h && typeof h === 'object' && 'typeName' in h) ? h.typeName     // Legacy fallback
            : (typeof h === 'string') ? h                                       // String fallback
                : 'System.Object';

// Store the full type name directly (not in a nested object)
// This makes it compatible with Type.ToString() which expects either a string or { typeName }
return wrap(typeName, 'System.Type');

*/

// TODO: the above seems questionable