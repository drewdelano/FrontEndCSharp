import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.ByteArrayContent..ctor(System.Byte[])'] = function (ctx, $this, content) {
    const instance = unwrap($this);
    const managedBytes = unwrap(content);
    instance._content = content;
    instance._offset = wrap(0, 'System.Int32');
    instance._count = wrap(Array.isArray(managedBytes) ? managedBytes.length : 0, 'System.Int32');
    instance.$fecsBytes = Array.isArray(managedBytes)
        ? Uint8Array.from(managedBytes, value => Number(unwrap(value)))
        : new Uint8Array();
};
