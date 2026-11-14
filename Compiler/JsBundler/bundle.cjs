const fs = require("node:fs");
const path = require("node:path");
const process = require("node:process");
const { pathToFileURL } = require("node:url");
const webpack = require("webpack");
const TerserPlugin = require("terser-webpack-plugin");

const vendorPathPattern = /(?:^|[\\/])node_modules(?:[\\/]|$)/u;
const sourceMapDirectivePattern = /(?:\/\/[ \t]*[@#][ \t]*sourceMappingURL[ \t]*=[^\r\n]*|\/\*[ \t]*[@#][ \t]*sourceMappingURL[ \t]*=[\s\S]*?\*\/)/gu;
const managedSourceMapPluginName = "FrontEndCSharpManagedSourceMapPlugin";

class ManagedSourceMapPlugin {
    constructor(entryRoot) {
        this.entryRoot = entryRoot;
    }

    apply(compiler) {
        compiler.hooks.compilation.tap(managedSourceMapPluginName, compilation => {
            compilation.hooks.buildModule.tap(
                { name: managedSourceMapPluginName, stage: 100 },
                module => {
                    const include = typeof module.resource === "string"
                        && this.includes(module.resource);
                    module.useSourceMap = include;
                    module.useSimpleSourceMap = false;
                });
            compilation.hooks.runtimeModule.tap(
                { name: managedSourceMapPluginName, stage: 100 },
                module => {
                    module.useSourceMap = false;
                    module.useSimpleSourceMap = false;
                });
        });
    }

    includes(resource) {
        const virtualSeparator = resource.indexOf("|");
        const physicalResource = virtualSeparator < 0
            ? resource
            : resource.slice(0, virtualSeparator);
        const relativePath = path.relative(this.entryRoot, physicalResource);
        if (path.isAbsolute(relativePath)
            || relativePath === ".."
            || relativePath.startsWith(`..${path.sep}`)) {
            return false;
        }

        const normalizedPath = relativePath.replaceAll("\\", "/");
        return normalizedPath === "debug-build.js"
            || normalizedPath === "jit-engine/_executeInstruction.js"
            || normalizedPath.startsWith("runtime-assets/");
    }
}

function maskNestedVendorSourceMaps(outputPath) {
    const sourceMapPath = `${outputPath}.map`;
    if (!fs.existsSync(sourceMapPath)) {
        return;
    }

    const sourceMap = JSON.parse(fs.readFileSync(sourceMapPath, "utf8"));
    if (!Array.isArray(sourceMap.sources) || !Array.isArray(sourceMap.sourcesContent)) {
        return;
    }
    if (sourceMap.sources.length !== sourceMap.sourcesContent.length) {
        throw new Error(`Source map has ${sourceMap.sources.length} sources but ${sourceMap.sourcesContent.length} sourcesContent entries.`);
    }

    let changed = false;
    for (let index = 0; index < sourceMap.sources.length; index += 1) {
        const source = sourceMap.sources[index];
        const content = sourceMap.sourcesContent[index];
        if (typeof source !== "string"
            || typeof content !== "string"
            || !vendorPathPattern.test(source)) {
            continue;
        }

        const maskedContent = content.replace(
            sourceMapDirectivePattern,
            directive => directive.replace(/[^\r\n]/gu, " "));
        if (maskedContent !== content) {
            sourceMap.sourcesContent[index] = maskedContent;
            changed = true;
        }
    }

    if (changed) {
        fs.writeFileSync(sourceMapPath, JSON.stringify(sourceMap), "utf8");
    }
}

async function main() {
    const [
        entryArgument,
        outputArgument,
        sourceMapRootArgument,
        sourceMapModeArgument = "managed"
    ] = process.argv.slice(2);
    if (entryArgument === undefined || outputArgument === undefined || sourceMapRootArgument === undefined) {
        throw new Error("Usage: node bundle.cjs <entry-path> <output-path> <source-map-root> [managed|full]");
    }
    if (sourceMapModeArgument !== "managed" && sourceMapModeArgument !== "full") {
        throw new Error("The source-map mode must be 'managed' or 'full'.");
    }
    const entryPath = path.resolve(entryArgument);
    const outputPath = path.resolve(outputArgument);
    const sourceMapRoot = path.resolve(sourceMapRootArgument);
    const entryRoot = path.dirname(entryPath);

    const moduleFilenameTemplate = info => {
        const virtualSeparator = info.absoluteResourcePath.indexOf("|");
        const physicalResourcePath = virtualSeparator < 0
            ? info.absoluteResourcePath
            : info.absoluteResourcePath.slice(0, virtualSeparator);
        const virtualSuffix = virtualSeparator < 0
            ? ""
            : info.absoluteResourcePath.slice(virtualSeparator);
        const relativePath = path.relative(entryRoot, physicalResourcePath);
        const sourcePath = !path.isAbsolute(relativePath)
            && relativePath !== ".."
            && !relativePath.startsWith(`..${path.sep}`)
            ? path.join(sourceMapRoot, relativePath)
            : physicalResourcePath;
        return pathToFileURL(sourcePath + virtualSuffix).href;
    };

    const compiler = webpack({
        mode: "production",
        target: ["web", "es2022"],
        entry: entryPath,
        devtool: false,
        experiments: {
            topLevelAwait: true,
            outputModule: true
        },
        output: {
            path: path.dirname(outputPath),
            filename: path.basename(outputPath),
            module: true,
            publicPath: "auto",
            devtoolModuleFilenameTemplate: moduleFilenameTemplate
        },
        optimization: {
            minimize: true,
            minimizer: [
                new TerserPlugin({
                    extractComments: false,
                    parallel: false,
                    terserOptions: {
                        compress: {
                            drop_debugger: false,
                            keep_fnames: /^(ExecuteMSIL|executeInstruction)$/
                        },
                        mangle: {
                            keep_fnames: /^ExecuteMSIL$/,
                            reserved: ["opts"]
                        }
                    }
                })
            ],
            runtimeChunk: false,
            splitChunks: false
        },
        plugins: [
            new webpack.SourceMapDevToolPlugin({
                filename: "[file].map",
                moduleFilenameTemplate
            }),
            ...(sourceMapModeArgument === "managed"
                ? [new ManagedSourceMapPlugin(entryRoot)]
                : []),
            new webpack.BannerPlugin({
                banner: "const __frontEndCSharpBundleUrl = import.meta.url;",
                raw: true,
                entryOnly: true
            }),
            new webpack.optimize.LimitChunkCountPlugin({ maxChunks: 1 })
        ]
    });

    const stats = await new Promise((resolve, reject) => {
        compiler.run((error, result) => {
            compiler.close(closeError => {
                if (error !== null && error !== undefined) {
                    reject(error);
                    return;
                }
                if (closeError !== null && closeError !== undefined) {
                    reject(closeError);
                    return;
                }
                resolve(result);
            });
        });
    });

    if (stats === undefined || stats.hasErrors()) {
        const details = stats?.toString({ all: false, errors: true, errorDetails: true })
            ?? "Webpack did not return compilation statistics.";
        throw new Error(details);
    }

    maskNestedVendorSourceMaps(outputPath);
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
