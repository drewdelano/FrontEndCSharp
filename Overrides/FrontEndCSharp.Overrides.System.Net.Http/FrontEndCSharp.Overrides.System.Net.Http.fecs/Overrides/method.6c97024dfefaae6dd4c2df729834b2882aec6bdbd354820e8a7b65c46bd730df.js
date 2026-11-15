import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_CheckCertificateRevocationList(System.Boolean)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Browser certificate validation is not application-controlled.');
};
