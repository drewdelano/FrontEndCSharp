import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.StringContent..ctor(System.String)'] = function (ctx, $this, content) {
    const value = unwrap(content);
    const bytes = new TextEncoder().encode(value ?? '');
    const instance = unwrap($this);
    instance.$fecsBytes = bytes;
    instance.$fecsHeaders = new Map([
        ['content-type', ['text/plain; charset=utf-8']]
    ]);
    instance._content = wrap([], 'System.Byte[]');
    instance._offset = wrap(0, 'System.Int32');
    instance._count = wrap(bytes.length, 'System.Int32');
};
