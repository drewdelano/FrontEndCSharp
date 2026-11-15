import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpResponseMessage.Dispose()'] = function (ctx, $this) {
    const response = unwrap($this);
    response._disposed = wrap(true, 'System.Boolean');
    const content = unwrap(response._content);
    if (content) {
        content.$fecsDisposed = true;
        const reader = content.$fecsReadableStreamReader;
        if (reader !== null && reader !== undefined) {
            Promise.resolve(reader.cancel()).catch(() => {});
        }
        const stream = unwrap(content.$fecsReadStream);
        if (stream) {
            stream.$fecsDisposed = true;
        }
    }
};
