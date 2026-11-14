import { BaseUiToastHost } from '../runtime-assets/Interop.JS.Shadcn.BaseUI.fecs/toast-adapter.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Shadcn.BaseUI.BaseUiReactImports.BaseUiToastToaster()'] = function () {
    return wrap(BaseUiToastHost, 'Interop.JS.React.ImportedReactComponent');
};
