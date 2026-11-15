import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_Proxy(System.Net.IWebProxy)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Browser proxy configuration is not application-controlled.');
};
