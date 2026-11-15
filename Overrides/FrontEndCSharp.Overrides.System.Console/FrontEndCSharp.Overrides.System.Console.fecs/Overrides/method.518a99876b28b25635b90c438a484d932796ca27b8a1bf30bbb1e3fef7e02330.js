import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Object)'] = function(ctx, obj) {
  console.log(unwrap(obj));
};
