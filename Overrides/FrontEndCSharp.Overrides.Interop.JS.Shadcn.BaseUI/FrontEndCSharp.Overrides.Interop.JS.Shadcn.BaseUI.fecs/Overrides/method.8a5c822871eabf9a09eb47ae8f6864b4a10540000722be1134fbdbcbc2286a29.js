import { addToast } from '../runtime-assets/FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.fecs/toast-adapter.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Shadcn.BaseUI.BaseUiReactImports.ShowToast(System.String, System.String, System.String)'] = function (ctx, title, description, type) {
    addToast(unwrap(title), unwrap(description), unwrap(type));
};
