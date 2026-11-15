import { initializeObject } from '../msil.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Net.Http.HttpContent.ReadAsStreamAsync()'] = function (ctx, $this) {
    return getStreamTask(ctx, $this);
};

function getStreamTask(ctx, contentReference) {
    const content = unwrap(contentReference);
    if (content.$fecsDisposed) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access disposed HTTP content.');
    }
    content.$fecsReadStream ??= createStream(ctx, content);
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(content.$fecsReadStream);
    return wrap(
        source.task,
        'System.Threading.Tasks.Task`1[TResult]',
        { TResult: 'System.IO.Stream' });
}

function createStream(ctx, content) {
    if (!Object.hasOwn(content, '$fecsReadableStreamReader')) {
        return createBufferedStream(ctx, content.$fecsBytes);
    }
    const stream = initializeObject('System.Net.Http.BrowserHttpReadStream');
    const streamValue = unwrap(stream);
    streamValue.$fecsReadableStreamReader = content.$fecsReadableStreamReader;
    streamValue.$fecsDisposed = false;
    return stream;
}

function createBufferedStream(ctx, bytes) {
    const stream = initializeObject('System.IO.MemoryStream');
    const managedBytes = wrap(
        Array.from(bytes instanceof Uint8Array ? bytes : [], value => wrap(value, 'System.Byte')),
        'System.Byte[]');
    const methods = globalThis.DotNet.Methods;
    methods['System.IO.MemoryStream..ctor(System.Byte[], System.Boolean)'](
        ctx,
        stream,
        managedBytes,
        wrap(false, 'System.Boolean'));
    const streamValue = unwrap(stream);
    streamValue.$fecsMemoryBytes = managedBytes;
    streamValue.$fecsMemoryPosition = 0;
    streamValue.$fecsMemoryLength = unwrap(managedBytes).length;
    return stream;
}
