import { installHost } from './hostInterop.js';
import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
installHost(exports.BrowserHost);
await runMain(config.mainAssemblyName, []);
