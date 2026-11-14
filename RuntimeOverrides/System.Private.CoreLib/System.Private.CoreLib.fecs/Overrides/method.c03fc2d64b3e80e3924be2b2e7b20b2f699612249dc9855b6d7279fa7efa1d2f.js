import { areRuntimeTypeObjectsEqual, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Type.op_Inequality(System.Type, System.Type)'] = function (ctx, left, right) {
    return wrap(!areRuntimeTypeObjectsEqual(left, right), 'System.Boolean');
};
