#!/usr/bin/env python3
"""Allowlisted local release ZIP; no installation or network operations."""
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def payload_paths(root=ROOT):
    files = {
        'Info.json': root/'IsekaiMod/Info.json',
        'IsekaiMod.dll': root/'bin/Release/net481/IsekaiMod.dll',
        'Localization/LocalizationPack.json': root/'IsekaiMod/Localization/LocalizationPack.json',
        'LICENSE': root/'LICENSE',
        'README-ko.md': root/'docs/README-ko.md',
    }
    assets = subprocess.check_output(['git', 'ls-files', 'IsekaiMod/Assets'], cwd=root, text=True).splitlines()
    if not assets:
        raise ValueError('No tracked runtime assets')
    for name in assets:
        path = Path(name)
        if path.suffix.lower() != '.png' or '..' in path.parts:
            raise ValueError('Unexpected runtime asset')
        files[path.relative_to('IsekaiMod').as_posix()] = root/path
    return {'IsekaiMod/'+name: path for name, path in sorted(files.items())}


def payloads(root=ROOT):
    result = {}
    for name, path in payload_paths(root).items():
        if path.is_symlink() or not path.is_file():
            raise ValueError('Missing/symlinked payload: '+name)
        result[name] = path.read_bytes()
    return result


def validate_archive(path, expected):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(expected):
            raise ValueError('ZIP allowlist/duplicate mismatch')
        for item in archive.infolist():
            if item.file_size > 20_000_000 or item.external_attr >> 16 & 0o170000 == 0o120000:
                raise ValueError('Unexpected ZIP size/symlink')
            if archive.read(item) != expected[item.filename]:
                raise ValueError('ZIP payload mismatch')


def rebuild(root=ROOT):
    # A clean tree does not prove the ignored bin output belongs to HEAD.
    subprocess.check_call([str(root/'scripts/dotnet.sh'), 'build', str(root/'IsekaiMod.Local.csproj'),
                           '-t:Rebuild', '-c', 'Release', '--nologo'], cwd=root)


def validate_metadata(info, metadata):
    if info['Id'] != 'IsekaiMod' or info['AssemblyName'] != 'IsekaiMod.dll' or info['EntryMethod'] != 'IsekaiMod.Main.Load' or metadata['Name'] != 'IsekaiMod' or metadata['Version'] != info['Version']+'.0':
        raise ValueError('Mod/assembly identity or version mismatch')
    types = {row['Name']: row for row in metadata['Types']}
    if not any(m['Name'] == 'Load' and m['Public'] and m['Static'] and m.get('EntrySignature') == 'bool(UnityModManagerNet.UnityModManager+ModEntry)' for m in types['IsekaiMod.Main']['Methods']):
        raise ValueError('Missing public static entry point')
    if not {'IsekaiMod.Localization.KoreanLocalization', 'IsekaiMod.Localization.KoreanTranslationData'} <= set(types):
        raise ValueError('Missing built-in Korean localization types')
    required = {'IsekaiMod.Config.Blueprints.json', 'IsekaiMod.Config.AddedContent.json', 'IsekaiMod.Localization.LocalizationPack.json'}
    if not required <= set(metadata['Resources']):
        raise ValueError('Missing embedded defaults')


def main():
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True):
        raise ValueError('Commit reviewed source before packaging')
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    rebuild()
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True) or commit != subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip():
        raise ValueError('Source changed while rebuilding')
    files = payloads()
    info = json.loads(files['IsekaiMod/Info.json'])
    metadata = json.loads(subprocess.check_output([
        str(ROOT/'scripts/dotnet.sh'), 'run', '--project', str(ROOT/'scripts/LocalizationInventory'),
        '-c', 'Release', '--', '--mod-metadata', str(ROOT/'bin/Release/net481/IsekaiMod.dll')], cwd=ROOT, text=True))
    validate_metadata(info, metadata)
    catalog = json.loads((ROOT/'localization/ko-kr.json').read_text())
    dll = files['IsekaiMod/IsekaiMod.dll']
    for text in [entry['Korean'] for entry in catalog['Entries']] + list(catalog['UI'].values()):
        if text.encode('utf-16le') not in dll:
            raise ValueError('Compiled Korean text missing')
    out = ROOT/'dist'; out.mkdir(exist_ok=True)
    archive = out/f"IsekaiMod-{info['Version']}-ko-KR.zip"
    with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as z:
        for name, data in files.items():
            entry = zipfile.ZipInfo(name, (2026, 10, 21, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED; entry.external_attr = 0o100644 << 16
            z.writestr(entry, data)
    validate_archive(archive, files)
    manifest = {'Version': info['Version'], 'Commit': commit,
                'Archive': archive.name, 'SHA256': digest(archive.read_bytes()), 'FileCount': len(files),
                'GameVerified': False, 'Prerelease': True,
                'Files': {name: digest(data) for name, data in files.items()}, 'AssemblyMetadata': metadata}
    archive.with_suffix('.manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    archive.with_suffix('.sha256').write_text(manifest['SHA256']+'  '+archive.name+'\n')
    print(json.dumps({key: manifest[key] for key in ('Archive', 'Commit', 'SHA256', 'FileCount', 'GameVerified')}, indent=2))


if __name__ == '__main__':
    main()
