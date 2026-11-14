import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.String)'] = function(ctx, str) {
  console.log(unwrap(str));
};
