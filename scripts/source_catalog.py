"""Reconstruct current Isekai-owned display identities without executing game code."""
from collections import defaultdict
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]

def resolve(source, pack):
    constants=defaultdict(list)
    for r in source['Constants']:constants[r['Text']].append(r['Source'])
    by_text=defaultdict(list)
    for r in pack:by_text[r['enGB']].append(r)
    result={}
    def add(text, process, origin, name=None, derivation='constant'):
        if not isinstance(text,str) or not text:return
        # TTT first reuses an existing exact English entry, including its template flag.
        existing=by_text.get(text,[])
        flags={r['ProcessTemplates'] for r in existing} or {process}
        for flag in flags:
            identity=(text,flag)
            row=result.setdefault(identity,dict(English=text,ProcessTemplates=flag,SimpleName=name or (existing[0]['SimpleName'] if existing else Path(origin['File']).stem+'.'+str(origin['Line'])),
                                               Keys=[r['Key'] for r in existing if r['ProcessTemplates']==flag],Sources=[]))
            record=dict(**origin,Derivation=derivation)
            if record not in row['Sources']:row['Sources'].append(record)
    # Loaded catalog records whose full English is also a current source constant.
    for row in pack:
        for location in constants.get(row['enGB'],[]):add(row['enGB'],row['ProcessTemplates'],location,row['SimpleName'])
    background=next(t for t in constants if t.startswith('\nIf the character already has the class skill,'))
    summon=next(t for t in constants if t.startswith(' Summoned monsters appear where you designate'))
    for call in source['Calls']:
        if not call['Source']['File'].startswith('IsekaiMod/Content/'):continue
        args=call['Arguments'];method=call['Method'];index=2 if method=='CreateString' else 1
        if len(args)<=index:continue
        text=args[index]['Constant']
        if not isinstance(text,str):continue
        name=args[1]['Constant'] if method=='CreateString' else None
        process=method in ('SetDescription','SetBackgroundDescription','SetSummonDescription')
        if method=='CreateString':
            for arg in args[3:]:
                if arg['Expression'].startswith('shouldProcess:') or arg['Constant'] is True:process=bool(arg['Constant']);break
        derivation='constant'
        if method=='SetBackgroundDescription':text+=background;derivation='SetBackgroundDescription + BackgroundAppendix'
        if method=='SetSummonDescription':text+=summon;derivation='SetSummonDescription + SummonAppendix'
        add(text,process,call['Source'],name,derivation)
    def spaced(name):return ''.join((' '+c) if c.isupper() else c for c in name).lstrip()
    for call in source['Factories']:
        args=call['Arguments'];method=call['Method'];origin=call['Source']
        if not args or not isinstance(args[0]['Constant'],str):continue
        name=args[0]['Constant'];named={a.get('Name'):a['Constant'] for a in args if a.get('Name')}
        def get(key,index,default=None):return named.get(key,args[index]['Constant'] if len(args)>index else default)
        if method=='CreateStartingWeapon':add(get('description',1),True,origin,name+'.Description','CreateStartingWeapon');continue
        if method in ('CreateImmunity','CreateExceptionalWeapon'):
            add(spaced(name),False,origin,name+'.Name',method+' generated name')
            add(get('description',1),method=='CreateImmunity',origin,name+'.Description',method)
        elif method=='CreateSummonBuffToggleFeature':
            add(spaced(name),False,origin,name+'.Name',method+' generated name')
            add(get('description',1),False,origin,name+'.Description',method)
            add(get('descriptionBuff',2),False,origin,name+'SummonBuff.Description',method)
        elif method=='CreateToggleAuraBuffFeature':
            add(spaced(name),False,origin,name+'.Name',method+' generated name')
            add(get('description',1),False,origin,name+'.Description',method)
            add(get('descriptionBuff',2),False,origin,name+'Buff.Description',method)
        elif method=='CreateToggleBuffFeature':
            if named:
                desc=named.get('description');display=named.get('displayName',spaced(name))
                buffdesc=named.get('descriptionBuff',desc);buffdisplay=named.get('displayNameBuff',display)
            else:
                # Main callsites use named arguments; this covers the simple three-argument overload.
                desc=get('description',1);display=spaced(name);buffdesc=desc;buffdisplay=display
            add(display,False,origin,name+'.Name',method)
            add(buffdisplay,False,origin,name+'Buff.Name',method)
            add(desc,False,origin,name+'.Description',method)
            add(buffdesc,False,origin,name+'Buff.Description',method)
    # readonly strings passed by identifier instead of literal: explicitly bounded source fields.
    for text,locations in constants.items():
        for location in locations:
            if location['File'].endswith('/Gifted.cs') and (text=='Gifted' or text.startswith('You are talented in all skills')):
                add(text,text!='Gifted',location,'Gifted.Name' if text=='Gifted' else 'Gifted.Description','readonly initializer -> SetName/SetDescription')
    return sorted(result.values(),key=lambda r:(r['SimpleName'],r['English'],r['ProcessTemplates']))
