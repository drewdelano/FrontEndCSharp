import { isTypeAssignableTo, unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Type.IsAssignableFrom(System.Type)'] = function (_ctx, target, source) {
    return wrap(isTypeAssignableTo(unwrap(source), unwrap(target)), 'System.Boolean');
};
