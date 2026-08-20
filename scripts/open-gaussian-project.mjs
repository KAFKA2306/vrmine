import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const projectVersionPath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
const projectVersion = fs.readFileSync(projectVersionPath, 'utf8').match(/^m_EditorVersion:\s*(.+)$/m)?.[1]?.trim();
if (!projectVersion) throw new Error(`Could not read m_EditorVersion from ${projectVersionPath}`);

const isWsl = process.platform === 'linux' && (Boolean(process.env.WSL_DISTRO_NAME) || fs.existsSync('/proc/sys/fs/binfmt_misc/WSLInterop'));
const hasWindowsHost = process.platform === 'win32' || isWsl;
const dryRun = process.argv.includes('--dry-run');

function toWindowsPath(value) {
    if (process.platform === 'win32') return value;
    const result = spawnSync('wslpath', ['-w', value], { encoding: 'utf8' });
    if (result.status !== 0) throw new Error(`wslpath failed for ${value}: ${result.stderr || 'unknown error'}`);
    return result.stdout.trim();
}

function toHostPath(value) {
    if (!isWsl || !/^[A-Za-z]:[\\/]/.test(value)) return value;
    const drive = value[0].toLowerCase();
    return `/mnt/${drive}/${value.slice(3).replaceAll('\\', '/')}`;
}

function commandPath(command) {
    const lookup = process.platform === 'win32' ? 'where.exe' : 'which';
    const result = spawnSync(lookup, [command], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
    if (result.status !== 0) return null;
    return result.stdout.trim().split(/\r?\n/)[0] || null;
}

function resolveUnityExecutable() {
    const configured = process.env.UNITY_EXE;
    if (configured) {
        const hostPath = toHostPath(configured);
        if (!fs.existsSync(hostPath)) throw new Error(`UNITY_EXE does not exist: ${configured}`);
        return hostPath;
    }

    if (!hasWindowsHost) throw new Error(`Unity ${projectVersion} was not found. Set UNITY_EXE to the exact Unity executable path.`);
    const programFiles = process.env.ProgramFiles ?? process.env.PROGRAMFILES ?? (isWsl ? '/mnt/c/Program Files' : 'C:\\Program Files');
    const candidates = [
        path.join(programFiles, 'Unity', 'Hub', 'Editor', projectVersion, 'Editor', 'Unity.exe'),
        path.join(isWsl ? '/mnt/c/Program Files (x86)' : 'C:\\Program Files (x86)', 'Unity', 'Hub', 'Editor', projectVersion, 'Editor', 'Unity.exe'),
    ];
    const match = candidates.find((candidate) => fs.existsSync(candidate));
    if (!match) throw new Error(`Unity ${projectVersion} was not found. Set UNITY_EXE to the exact Unity executable path.`);
    return match;
}

function resolveVpmExecutable() {
    const configured = process.env.VPM_EXE ?? process.env.VCC_CLI;
    if (configured) {
        const hostPath = toHostPath(configured);
        if (!fs.existsSync(hostPath)) throw new Error(`VPM_EXE/VCC_CLI does not exist: ${configured}`);
        return hostPath;
    }
    return isWsl ? commandPath('vpm.exe') : commandPath('vpm');
}

function runVpm(vpm, args) {
    const result = spawnSync(vpm, args, { cwd: projectRoot, stdio: 'inherit' });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`VCC CLI failed with exit code ${result.status}: ${args.join(' ')}`);
}

function launchUnity(unityExecutable, unityProjectPath) {
    if (!hasWindowsHost) {
        const child = spawn(unityExecutable, ['-projectPath', unityProjectPath], {
            cwd: projectRoot,
            detached: true,
            stdio: 'ignore',
        });
        child.unref();
        return;
    }

    const powershell = process.env.POWERSHELL_EXE ?? commandPath('powershell.exe');
    if (!powershell) throw new Error('PowerShell was not found. Set POWERSHELL_EXE to launch Windows Unity from WSL.');
    const windowsUnityExecutable = toWindowsPath(unityExecutable);
    const windowsLauncher = toWindowsPath(path.join(projectRoot, 'scripts', 'open-unity-windows.ps1'));
    const result = spawnSync(powershell, [
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        windowsLauncher,
        '-UnityPath',
        windowsUnityExecutable,
        '-ProjectPath',
        unityProjectPath,
    ], {
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
    });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`PowerShell failed to launch Unity: ${result.stderr || 'unknown error'}`);
    console.log(result.stdout.trim());
}

const unityExecutable = resolveUnityExecutable();
const unityProjectPath = hasWindowsHost ? toWindowsPath(projectRoot) : projectRoot;
const vpm = resolveVpmExecutable();

console.log(`Project: ${projectRoot}`);
console.log(`Unity: ${unityExecutable}`);
console.log(`Unity version: ${projectVersion}`);
if (vpm) {
    console.log(`VCC CLI: ${vpm}`);
    console.log(`VCC project path: ${unityProjectPath}`);
} else {
    console.log('VCC CLI: not found; Unity will still be opened directly. Set VPM_EXE to enable VCC registration and package resolve.');
}

if (dryRun) {
    console.log(`DRY RUN: ${vpm ? 'vpm add project + vpm resolve project, then ' : ''}Unity -projectPath ${unityProjectPath}`);
    process.exit(0);
}

if (vpm) {
    runVpm(vpm, ['add', 'project', unityProjectPath]);
    runVpm(vpm, ['resolve', 'project', unityProjectPath]);
}

launchUnity(unityExecutable, unityProjectPath);
console.log('Unity launch requested. The prepared marker will be consumed on editor load and GaussianSplatExhibition.unity will be opened.');
