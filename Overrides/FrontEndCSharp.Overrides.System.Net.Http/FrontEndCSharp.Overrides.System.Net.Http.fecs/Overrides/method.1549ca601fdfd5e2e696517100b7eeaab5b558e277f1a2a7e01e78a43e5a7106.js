import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.set_AutomaticDecompression(System.Net.DecompressionMethods)'] = function (ctx) {
    throwManagedException(ctx, 'System.PlatformNotSupportedException', 'Automatic decompression is controlled by the browser Fetch implementation.');
};
