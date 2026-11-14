import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_MaxConnectionsPerServer(System.Int32)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Browser connection pooling is not application-controlled.');
};
