import { initializeObject } from '../msil.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpResponseMessage.get_Content()'] = function (ctx, $this) {
    const response = unwrap($this);
    if (response._content !== null && response._content !== undefined) {
        return response._content;
    }

    const content = initializeObject('System.Net.Http.ByteArrayContent');
    const constructorSignature = 'System.Net.Http.ByteArrayContent..ctor(System.Byte[])';
    const constructor = globalThis.DotNet.Methods[constructorSignature];
    constructor(ctx, content, wrap([], 'System.Byte[]'));
    response._content = content;
    return content;
};
