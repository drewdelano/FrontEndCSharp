import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_SslProtocols(System.Security.Authentication.SslProtocols)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Browser TLS protocol negotiation is not application-controlled.');
};
