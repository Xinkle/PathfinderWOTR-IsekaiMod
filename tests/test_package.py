import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('release_package', ROOT/'scripts/package.py')
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class PackageTests(unittest.TestCase):
    def test_runtime_allowlist(self):
        payload = package.payload_paths()
        self.assertEqual(64, len(payload))  # 59 assets plus five runtime/documentation files.
        self.assertEqual(['IsekaiMod/IsekaiMod.dll'], [n for n in payload if n.endswith('.dll')])
        self.assertFalse(any('Settings' in n or 'AddedContent.json' in n or '.references' in n for n in payload))
        self.assertTrue(all(n.startswith('IsekaiMod/') and '..' not in Path(n).parts for n in payload))

    def test_archive_roundtrip(self):
        payload = {'IsekaiMod/Info.json': b'{}'}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'test.zip'
            with zipfile.ZipFile(path, 'w') as archive:
                for name, data in payload.items(): archive.writestr(name, data)
            package.validate_archive(path, payload)
            with self.assertRaises(ValueError):
                package.validate_archive(path, {'IsekaiMod/Info.json': b'changed'})

    def test_stale_output_is_rebuilt(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            dll = root/'old.dll'; dll.write_bytes(b'stale')
            def build(command, **kwargs):
                self.assertIn('-t:Rebuild', command)
                dll.write_bytes(b'rebuilt')
            with patch.object(package.subprocess, 'check_call', side_effect=build) as invoke:
                package.rebuild(root)
                invoke.assert_called_once()
            self.assertEqual(b'rebuilt', dll.read_bytes())

    def test_invalid_identity_and_signature_rejected(self):
        info = {'Id': 'IsekaiMod', 'Version': '5.4.4', 'AssemblyName': 'IsekaiMod.dll', 'EntryMethod': 'IsekaiMod.Main.Load'}
        method = {'Name': 'Load', 'Public': True, 'Static': True, 'EntrySignature': 'bool(UnityModManagerNet.UnityModManager+ModEntry)'}
        metadata = {'Name': 'IsekaiMod', 'Version': '5.4.4.0', 'Types': [
            {'Name': 'IsekaiMod.Main', 'Methods': [method]},
            {'Name': 'IsekaiMod.Localization.KoreanLocalization', 'Methods': []},
            {'Name': 'IsekaiMod.Localization.KoreanTranslationData', 'Methods': []}],
            'Resources': ['IsekaiMod.Config.Blueprints.json', 'IsekaiMod.Config.AddedContent.json', 'IsekaiMod.Localization.LocalizationPack.json']}
        package.validate_metadata(info, metadata)
        for field in ('AssemblyName', 'EntryMethod'):
            with self.assertRaises(ValueError): package.validate_metadata({**info, field: 'wrong'}, metadata)
        method['EntrySignature'] = 'bool(wrong)'
        with self.assertRaises(ValueError): package.validate_metadata(info, metadata)

    def test_extra_file_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'test.zip'
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('IsekaiMod/Info.json', b'{}')
                archive.writestr('IsekaiMod/Owlcat.Runtime.Validation.dll', b'forbidden')
            with self.assertRaises(ValueError):
                package.validate_archive(path, {'IsekaiMod/Info.json': b'{}'})


if __name__ == '__main__': unittest.main()
