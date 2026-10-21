import copy
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import unittest
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"scripts"))

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('localization',ROOT/'scripts/localization.py')
loc=importlib.util.module_from_spec(spec);spec.loader.exec_module(loc)

class LocalizationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog=loc.read(loc.CATALOG)
        cls.source=loc.inventory()
        cls.constants={r['English'] for r in loc.target_catalog(cls.source)}

    def test_catalog_and_generation(self):
        loc.validate(self.catalog,self.constants)
        loc.coverage(self.catalog,loc.target_catalog(self.source))
        self.assertEqual(loc.DATA.read_text(),loc.render(self.catalog))
        self.assertEqual(loc.read(loc.AUDIT),loc.audit(self.source,self.catalog))

    def test_markup_mutations_rejected(self):
        for field, transform in [('Korean',lambda s:s.replace('{/g}','',1)),
                                 ('English',lambda s:s+' updated')]:
            c=copy.deepcopy(self.catalog)
            row=next(r for r in c['Entries'] if '{/g}' in r['Korean'])
            row[field]=transform(row[field])
            with self.assertRaises(ValueError):loc.validate(c,self.constants)
        c=copy.deepcopy(self.catalog)
        c['UI']['Profit {0}']='수익 {1}'
        with self.assertRaises(ValueError):loc.validate(c,self.constants)

    def test_nesting_rejected(self):
        with self.assertRaises(ValueError):loc.validate_nesting('{n}{g|X}text{/n}{/g}')
        with self.assertRaises(ValueError):loc.validate_nesting('<b><color=red>text</b></color>')

    def test_coverage_mutations_rejected(self):
        c=copy.deepcopy(self.catalog);c['Entries'].pop()
        with self.assertRaises(ValueError):loc.coverage(c,loc.target_catalog(self.source))
        self.assertEqual([],self.source['SyntaxErrors'])

    def test_terminology(self):
        byname={r['SimpleName']:r['Korean'] for r in self.catalog['Entries']}
        self.assertIn('확산',byname['AutoBolsterBuff.Name'])
        self.assertIn('강화',byname['AutoEmpowerBuff.Name'])
        self.assertEqual('구토 면역',byname['NauseatedImmunity.Name'])
        self.assertEqual('고통 면역',byname['SickenedImmunity.Name'])
        self.assertIn('한철',byname['StartingWeaponDagger.Description'])
        self.assertNotIn('신황는',byname['GodEmperorProficiencies.Description'])
        self.assertIn('피해',byname['FriendlyAuraBuff.Description'])
        for r in self.catalog['Entries']:
            self.assertNotIn(chr(92)+'n',r['Korean'])

    def test_duplicate_rejected(self):
        c=copy.deepcopy(self.catalog);c['Entries'].append(c['Entries'][0])
        with self.assertRaises(ValueError):loc.validate(c,self.constants)

    def test_english_pack_and_gameplay_preserved(self):
        baseline=subprocess.check_output(['git','show','e4d5ac3:IsekaiMod/Localization/LocalizationPack.json'],cwd=ROOT)
        self.assertEqual(baseline,(ROOT/'IsekaiMod/Localization/LocalizationPack.json').read_bytes())
        baseline=subprocess.check_output(['git','show','e4d5ac3:IsekaiMod/Config/AddedContent.json'],cwd=ROOT)
        self.assertEqual(baseline,(ROOT/'IsekaiMod/Config/AddedContent.json').read_bytes())
        # The only content-source edit is wrapping merchant display messages.
        path='IsekaiMod/Content/Features/IsekaiProtagonist/OverpoweredAbility/MerchantsGamble.cs'
        old=subprocess.check_output(['git','show','e4d5ac3:'+path],cwd=ROOT).decode('utf-8-sig')
        new=(ROOT/path).read_text()
        prefix='IsekaiMod.Localization.KoreanLocalization.'
        new=re.sub(re.escape(prefix)+r'Format\("([^"\n]*)", goldAmount\)',lambda m:'$"'+m[1].replace('{0}','{goldAmount}')+'"',new)
        new=re.sub(re.escape(prefix)+r'Text\(("[^"\n]*")\)',r'\1',new)
        self.assertEqual(old,new)

    def test_no_gameplay_source_changes(self):
        changed=subprocess.check_output(['git','diff','--name-only','e4d5ac3','--',
            'IsekaiMod/Content','IsekaiMod/Components','IsekaiMod/Utilities','IsekaiMod/Config'],cwd=ROOT,text=True).splitlines()
        self.assertEqual(['IsekaiMod/Content/Features/IsekaiProtagonist/OverpoweredAbility/MerchantsGamble.cs'],changed)

    def test_settings_labels_complete(self):
        config=loc.read(ROOT/'IsekaiMod/Config/AddedContent.json')
        for group in ('Isekai','Other'):
            for key,data in config[group]['Settings'].items():
                self.assertIn(key,self.catalog['UI'])
                self.assertIn(data['Description'],self.catalog['UI'])
        s=(ROOT/'IsekaiMod/UMMSettingsUI.cs').read_text()
        self.assertNotIn('SetttingUI.SettingGroup(',s)
        self.assertNotIn('entry.Value.Description =',s)
        self.assertIn('group.ChangeSetting(entry.Key, enabled)',s)

    def test_player_message_coverage(self):
        s=(ROOT/'IsekaiMod/Content/Features/IsekaiProtagonist/OverpoweredAbility/MerchantsGamble.cs').read_text()
        section=s[s.index('string[] failureMessages'):]
        calls=re.findall(r'KoreanLocalization\.(?:Text|Format)\(("[^"\n]*")',section)
        self.assertEqual(44,len(calls))
        for literal in calls:self.assertIn(json.loads(literal),self.catalog['UI'])

    def test_version_and_safe_build(self):
        info=loc.read(ROOT/'IsekaiMod/Info.json')
        self.assertEqual('IsekaiMod',info['Id']);self.assertEqual('JohN100x1',info['Author'])
        self.assertEqual('5.4.4',info['Version'])
        self.assertIn('"5.4.4.0"',(ROOT/'IsekaiMod/Properties/AssemblyInfo.cs').read_text())
        sdk=(ROOT/'IsekaiMod.Local.csproj').read_text()
        self.assertNotIn('PostBuildEvent',sdk);self.assertNotIn('robocopy',sdk.lower().replace('publicise/robocopy',''))
        self.assertIn('<Private>false</Private>',sdk)
        self.assertIn('Exclude="$(AdditionalGameReferencesPath)/Assembly-CSharp.dll;$(AdditionalGameReferencesPath)/TabletopTweaks-Core.dll"',sdk)
        publicizer=(ROOT/'scripts/PublicizeReferences/Program.cs').read_text()
        self.assertIn('FileMode.CreateNew',publicizer)
        self.assertIn('Input changed during reference preparation',publicizer)

if __name__=='__main__':unittest.main()
