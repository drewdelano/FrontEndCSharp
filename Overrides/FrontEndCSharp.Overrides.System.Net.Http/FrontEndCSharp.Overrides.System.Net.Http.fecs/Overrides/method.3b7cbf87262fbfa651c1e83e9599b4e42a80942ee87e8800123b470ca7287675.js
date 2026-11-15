import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.BrowserHttpReadStream.Read(System.Byte[], System.Int32, System.Int32)'] = function (ctx) {
    throwManagedException(
        ctx,
        'System.PlatformNotSupportedException',
        'Synchronous reads are not supported for browser response streams.');
};
