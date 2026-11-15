import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.Write(System.String)'] = function(ctx, str) {
  const output = unwrap(str);
  console.log(output);
};
