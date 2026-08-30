import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
const config = getConfig();
await getAssemblyExports(config.mainAssemblyName);
await runMain(config.mainAssemblyName, []);
