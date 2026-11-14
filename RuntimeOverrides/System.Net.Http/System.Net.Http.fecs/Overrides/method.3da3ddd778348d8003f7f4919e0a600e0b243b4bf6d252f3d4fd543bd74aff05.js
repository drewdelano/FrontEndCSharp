import { throwManagedException } from '../managed-exceptions.js';
import { isManagedNull, unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.StringContent..ctor(System.String, System.Text.Encoding, System.String)'] = function (ctx, $this, content, encoding, mediaType) {
    const value = unwrap(content);
    if (value === null || value === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'content\')');
    }
    const normalizedMediaType = unwrap(mediaType) ?? 'text/plain';
    if (!/^[!#$%&'*+\-.^_`|~0-9A-Za-z]+\/[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/.test(normalizedMediaType)) {
        throwManagedException(ctx, 'System.FormatException', 'The media type is invalid.');
    }
    const webName = isManagedNull(encoding)
        ? 'utf-8'
        : unwrap(encoding).$fecsWebName ?? 'utf-8';
    const bytes = encode(String(value), webName);
    const instance = unwrap($this);
    instance.$fecsBytes = bytes;
    instance.$fecsHeaders = new Map([
        ['content-type', [`${normalizedMediaType}; charset=${webName}`]]
    ]);
    instance._content = wrap([], 'System.Byte[]');
    instance._offset = wrap(0, 'System.Int32');
    instance._count = wrap(bytes.length, 'System.Int32');
};

function encode(value, webName) {
    const normalized = webName.toLowerCase();
    if (normalized === 'utf-8') return new TextEncoder().encode(value);
    if (normalized === 'utf-16' || normalized === 'utf-16le') {
        const bytes = new Uint8Array(value.length * 2);
        for (let index = 0; index < value.length; index++) {
            const code = value.charCodeAt(index);
            bytes[index * 2] = code & 0xff;
            bytes[index * 2 + 1] = code >>> 8;
        }
        return bytes;
    }
    throw new RangeError(`Unsupported text encoding '${webName}'.`);
}
