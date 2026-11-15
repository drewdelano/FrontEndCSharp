import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)'] = function(ctx, arr, field) {
    const arrObj = unwrap(arr);

    const fieldObj = unwrap(field);
    if (!fieldObj || !arrObj || !fieldObj.fieldData || !fieldObj.fieldData.arr) {
        throw "not impl";
    }
    const fieldData = fieldObj.fieldData;

    if (arrObj.length != fieldData.arr.length || !arrObj.length) {
        throw "not impl";
    }

    if (arrObj[0].t != fieldData.typeName) {
        throw "not impl";
    }

    for (let i = 0; i < arrObj.length; i++) {
        // arrObj is a array of typed elements
        // fieldObj.arr is an array of UN-typed elements
        arrObj[i] = wrap(fieldData.arr[i], fieldData.typeName);
    }
};
