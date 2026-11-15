import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { JavaScriptTaskCompletionSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Net.Http.HttpContent.ReadAsStringAsync()'] = function (ctx, $this) {
    const content = unwrap($this);
    if (content.$fecsDisposed) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access disposed HTTP content.');
    }

    const bytes = getBytes(content);
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(wrap(decode(ctx, content, bytes), 'System.String'));
    return wrap(
        source.task,
        'System.Threading.Tasks.Task`1[TResult]',
        { TResult: 'System.String' });
};

function getBytes(content) {
    if (content.$fecsBytes instanceof Uint8Array) {
        return content.$fecsBytes;
    }
    const managedBytes = unwrap(content._content);
    return Array.isArray(managedBytes)
        ? Uint8Array.from(managedBytes, value => Number(unwrap(value)))
        : new Uint8Array();
}

function decode(ctx, content, bytes) {
    const contentType = content.$fecsHeaders?.get('content-type')?.[0] ?? '';
    const match = /(?:^|;)\s*charset\s*=\s*(?:"([^"]+)"|([^;\s]+))/i.exec(contentType);
    const charset = match?.[1] ?? match?.[2] ?? 'utf-8';
    try {
        return new TextDecoder(charset).decode(bytes);
    } catch {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            `The character set '${charset}' is not supported.`);
    }
}
