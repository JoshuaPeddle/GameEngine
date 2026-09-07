import { installKeyboard } from './keyboardInterop.js';

try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet.withDiagnosticTracing(false).withApplicationArgumentsFromQuery().create();
    const config = runtime.getConfig();
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    installKeyboard(exports.KeyboardInterop);
    await runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    document.getElementById('loading').remove();
} catch (error) {
    console.error(error);
    document.getElementById('loading').textContent = 'The game could not start. Please reload to retry.';
}
