import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.MediaTypeWithQualityHeaderValue..ctor(System.String)'] = function (ctx, $this, mediaType) {
    const header = unwrap($this);
    const mediaTypeValue = unwrap(mediaType);
    if (mediaTypeValue === null || mediaTypeValue === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Media type cannot be null.');
    }
    const normalized = String(mediaTypeValue);
    if (!/^[!#$%&'*+\-.^_`|~0-9A-Za-z]+\/[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/.test(normalized)) {
        throwManagedException(ctx, 'System.FormatException', 'The media type is invalid.');
    }

    header._mediaType = wrap(normalized, 'System.String');
    header.$fecsHeaderValue = normalized;
};
