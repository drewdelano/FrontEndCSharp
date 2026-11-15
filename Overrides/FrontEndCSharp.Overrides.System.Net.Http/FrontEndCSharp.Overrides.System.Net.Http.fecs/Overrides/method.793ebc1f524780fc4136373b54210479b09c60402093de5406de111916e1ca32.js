import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_ClientCertificateOptions(System.Net.Http.ClientCertificateOption)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Browser client certificates are not application-controlled.');
};
