import re,glob,os
def rd(p): return open(p,encoding='utf-8-sig').read()
problems=0
# 1. models vs rules
rules=rd('firestore.rules')
lists=[]
for m in re.finditer(r"hasOnly\(\[(.*?)\]\)",rules,re.S):
    items=set(re.findall(r"'(\w+)'",m.group(1)))
    if len(items)>=2: lists.append(items)
print("== 1. Firestore model fields vs rules (a field the app writes but the rules do not allow = PERMISSION_DENIED)")
for f in sorted(glob.glob('Models/*.cs')):
    t=rd(f)
    if 'IFirestoreObject' not in t: continue
    props=set(re.findall(r'\[Firestore(?:Property|ServerTimestamp)\("(\w+)"\)\]',t))
    if not props: continue
    best=None
    for L in lists:
        if props<=L:
            extra=L-props
            if best is None or len(extra)<len(best[1]): best=(L,extra)
    if best is None:
        # find closest list
        c=min(lists,key=lambda L:len(props-L))
        print("  FAIL",os.path.basename(f),"not allowed by any rule list; missing in closest:",sorted(props-c)); problems+=1
    else:
        print("  ok  ",os.path.basename(f),len(props),"fields" + (", rules also allow "+str(sorted(best[1])) if best[1] else ""))
# 2. DI
print("== 2. Dependency injection (every constructor parameter must be registered)")
mp=rd('MauiProgram.cs')
reg=set(); 
for m in re.finditer(r'Add(?:Singleton|Transient|Scoped)<([\w<>]+)(?:,\s*([\w<>]+))?>',mp):
    reg.add(m.group(1))
    if m.group(2): reg.add(m.group(2))
ctor_bad=0
skip={'string','int','bool'}
for f in glob.glob('Services/*.cs')+glob.glob('Repositories/*.cs')+glob.glob('ViewModels/*.cs')+glob.glob('Views/*.xaml.cs'):
    t=rd(f)
    cls=re.search(r'public (?:sealed |partial )*class (\w+)',t)
    if not cls: continue
    name=cls.group(1)
    if name not in reg: continue
    m=re.search(r'public '+name+r'\(([^)]*)\)',t,re.S)
    if not m: continue
    for par in [p.strip() for p in m.group(1).split(',') if p.strip()]:
        typ=par.split()[0].replace('?','')
        if typ in skip: continue
        if typ.startswith('IFirebase') or typ in ('IFirebaseFirestore','IFirebaseAuth'): continue
        if typ not in reg:
            print("  FAIL",name,"needs",typ,"which is not registered"); problems+=1; ctor_bad+=1
print("  checked, unregistered parameters:",ctor_bad)
# 3. XAML bindings
print("== 3. XAML bindings (every {Binding X} must exist on the view model / item class)")
classes={}
for f in glob.glob('ViewModels/*.cs')+glob.glob('Models/*.cs')+glob.glob('Services/*.cs')+glob.glob('Helpers/*.cs'):
    t=rd(f)
    for m in re.finditer(r'(?:class|record|struct) (\w+)',t): classes.setdefault(m.group(1),'')
    for m in re.finditer(r'(?:class) (\w+)',t): classes[m.group(1)]=classes.get(m.group(1),'')+t
def members(cls):
    t=classes.get(cls,'')
    names=set(re.findall(r'public\s+[\w<>\[\]?,\s\.\(\)]+?\s+(\w+)\s*(?:\{|=>|;|=)',t))
    names|=set(re.findall(r'public\s+[\w<>\[\]?,\s\.]+?\s+(\w+)\s*\(',t))
    for fld in re.findall(r'\[ObservableProperty\][^;]*?private\s+[\w<>\[\]?,\.]+\s+_(\w+)',t,re.S):
        names.add(fld[0].upper()+fld[1:])
    for meth in re.findall(r'\[RelayCommand[^\]]*\]\s*(?:private|public|internal)\s+(?:async\s+)?[\w<>]+\s+(\w+)\(',t):
        n=meth[:-5] if meth.endswith('Async') else meth
        names.add(n+'Command')
    return names
bad=0
for f in sorted(glob.glob('Views/*.xaml')+glob.glob('Controls/*.xaml')):
    t=rd(f)
    types=set(re.findall(r'x:DataType="(?:\w+):(\w+)"',t))
    if not types: continue
    allm=set()
    for ty in types: allm|=members(ty)
    # include base class members
    allm|=members('BaseViewModel')
    for b in set(re.findall(r'\{Binding\s+(?:Path=)?([A-Za-z_][\w\.]*)',t)):
        first=b.split('.')[0]
        if first in allm or first in ('Source','Path','.'): continue
        # bindings inside templates of other types / relative sources are ignored if the name exists in any known class
        print("  ?",os.path.basename(f),"binding",b,"not found on",sorted(types)); bad+=1
print("  unresolved bindings:",bad)
# 4. braces
print("== 4. Brace / bracket balance in C# and XAML")
import xml.dom.minidom as md
nb=0
for f in glob.glob('**/*.cs',recursive=True):
    if '/obj/' in f or '/bin/' in f: continue
    t=rd(f)
    if t.count('{')!=t.count('}'): print("  FAIL braces",f); nb+=1
for f in glob.glob('**/*.xaml',recursive=True):
    if '/obj/' in f or '/bin/' in f or 'Raw' in f: continue
    try: md.parse(f)
    except Exception as e: print("  FAIL xml",f,e); nb+=1
print("  problems:",nb)
print("== leftovers of removed items")
for pat in ['RebuildDirectoryCommand','RebuildFeedsCommand','RebuildStatsCommand','usersStale','_userRepository.GetAllAsync']:
    hits=[f for f in glob.glob('**/*.*',recursive=True) if f.endswith(('.cs','.xaml')) and '/obj/' not in f and pat in rd(f)]
    print("  ",pat,"->",hits if hits else "none")
