import { noCancellationToken } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption)'] = function (ctx, $this, request, completionOption) {
    return requireGeneralSend()(ctx, $this, request, completionOption, noCancellationToken);
};

function requireGeneralSend() {
    const method = globalThis.DotNet.Methods[
        'System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)'];
    if (typeof method !== 'function') throw new Error('The general HttpClient.SendAsync overload is unavailable.');
    return method;
}
