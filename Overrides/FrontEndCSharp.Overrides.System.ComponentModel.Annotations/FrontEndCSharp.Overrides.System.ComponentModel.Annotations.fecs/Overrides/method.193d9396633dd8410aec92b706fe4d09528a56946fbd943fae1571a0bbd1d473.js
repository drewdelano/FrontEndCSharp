import { throwManagedException } from '../managed-exceptions.js';
import { validateProperty } from './method.d1a7884ad63b8d433d6bb63dcb84c4923e170e7a93bac3f4fce39fc6db12d7ac.js';

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.Validator.ValidateProperty(System.Object, System.ComponentModel.DataAnnotations.ValidationContext)'] = function (ctx, value, validationContext) {
    if (!validateProperty(ctx, value, validationContext, null)) {
        throwManagedException(ctx, 'System.ComponentModel.DataAnnotations.ValidationException', 'The property is invalid.');
    }
};
