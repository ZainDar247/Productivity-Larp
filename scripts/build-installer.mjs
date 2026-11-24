// Builds dist/productivity.larp-setup.exe: a single installer that carries the self-contained app
// (no .NET install needed on the target PC). Usage: npm run installer
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync, statSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const stage = resolve(root, "build");
const appDir = resolve(stage, "app");
const zip = resolve(stage, "payload.zip");
const outDir = resolve(root, "dist");

function run(cmd, args, label) {
  console.log(`\n> ${label}`);
  const r = spawnSync(cmd, args, { cwd: root, stdio: "inherit", shell: true });
  if (r.status !== 0) {
    console.error(`\n${label} failed.`);
    process.exit(r.status ?? 1);
  }
}

rmSync(stage, { recursive: true, force: true });
mkdirSync(stage, { recursive: true });

run("npm", ["run", "build"], "Build the UI");
run("dotnet", ["publish", "app/Larp.App", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-o", `"${appDir}"`, "-nologo", "-v", "q"], "Publish the app (self-contained)");

run("powershell", ["-NoProfile", "-Command", `"Compress-Archive -Path '${appDir}\\*' -DestinationPath '${zip}' -CompressionLevel Optimal -Force"`], "Zip the app");

run(
  "dotnet",
  ["publish", "app/Larp.Setup", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", `-p:PayloadZip="${zip}"`, "-o", `"${outDir}"`, "-nologo", "-v", "q"],
  "Build the installer",
);

const exe = resolve(outDir, "productivity.larp-setup.exe");
if (!existsSync(exe)) {
  console.error("The installer was not produced.");
  process.exit(1);
}
console.log(`\nDone: ${exe} (${(statSync(exe).size / 1048576).toFixed(0)} MB)`);
