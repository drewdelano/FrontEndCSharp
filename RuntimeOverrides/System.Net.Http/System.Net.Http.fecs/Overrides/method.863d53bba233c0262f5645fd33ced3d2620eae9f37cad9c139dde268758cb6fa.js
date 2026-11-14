import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.Send(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)'] = function () {
    throwManagedException(
        arguments[0],
        'System.PlatformNotSupportedException',
        'Synchronous HTTP is not supported by the browser Fetch transport.');
};
